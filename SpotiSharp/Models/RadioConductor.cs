using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public class RadioConductor
{
    private static RadioConductor _instance;
    public static RadioConductor Instance => _instance ??= new RadioConductor();

    private readonly object _lock = new object();

    private RadioTickState _state;

    // Lock-free views for the UI thread, refreshed under _lock on every state change. Reading
    // these never waits on _lock — which Tick holds across the blocking playback network call —
    // so opening the radio page never stalls behind an in-flight track transition.
    private volatile bool _activeSnapshot;
    private volatile RadioItem _currentItemSnapshot;
    private volatile List<RadioItem> _remainingSnapshot;

    internal event Action<RadioItem> ActiveItemChanged;

    internal bool IsActive => _activeSnapshot;

    internal RadioItem CurrentItem => _currentItemSnapshot;

    internal List<RadioItem> RemainingItems => _remainingSnapshot;

    // Must be called while holding _lock.
    private void CaptureState()
    {
        bool active = _state != null && _state.IsActive;
        _activeSnapshot = active;
        _currentItemSnapshot = active ? _state.ActiveItem as RadioItem : null;
        _remainingSnapshot = active ? _state.RemainingItems.Cast<RadioItem>().ToList() : null;
    }

    private RadioConductor()
    {
        UiLoop.Instance.OnRefreshUi += Tick;
    }

    private void RaiseActiveItem(RadioItem item)
    {
        var handler = ActiveItemChanged;
        if (handler == null) return;
        MainThread.BeginInvokeOnMainThread(() => handler(item));
    }

    // uri -> "Title / Subtitle" for every item in the radio, so log lines name what they're about.
    private static volatile Dictionary<string, string> _labels = new Dictionary<string, string>();

    internal static string Label(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return "nothing";
        return _labels.TryGetValue(uri, out var label) ? $"{uri} \"{label}\"" : uri;
    }

    private static void RememberLabels(List<RadioItem> radio)
    {
        var labels = new Dictionary<string, string>(_labels);
        foreach (var item in radio)
        {
            if (!string.IsNullOrEmpty(item.PlayUri)) labels[item.PlayUri] = $"{item.Title} / {item.Subtitle}";
        }
        _labels = labels;
    }

    internal void Start(List<RadioItem> radio, int startIndex)
    {
        if (radio == null || startIndex < 0 || startIndex >= radio.Count) return;

        RememberLabels(radio);
        DiagnosticLog.Write($"[Radio] conducting {radio.Count} items from index {startIndex} ({Label(radio[startIndex].PlayUri)})");

        lock (_lock)
        {
            _state = new RadioTickState(radio, startIndex, DateTime.UtcNow, alreadyIssued: true, RadioConfigModel.PodcastSegmentLengthMs);
            CaptureState();
        }

        RadioBackgroundService.Start();
        RaiseActiveItem(radio[startIndex]);
    }

    internal void Resync(List<RadioItem> radio, int activeIndex, string why)
    {
        RememberLabels(radio);
        DiagnosticLog.Write($"[Radio] resync after {why}: {radio.Count} items, active #{activeIndex} ({Label(radio[activeIndex].PlayUri)})");

        lock (_lock)
        {
            _state?.Resync(radio, activeIndex);
            CaptureState();
        }

        SyncSpotifyQueue(radio, activeIndex);
    }

    private static void SyncSpotifyQueue(List<RadioItem> radio, int activeIndex)
    {
        if (PlaybackCommands.QueueUri == null || radio == null || activeIndex < 0 || activeIndex >= radio.Count) return;

        var current = radio[activeIndex];
        if (current.IsPodcastSegment) return;

        var upcomingRun = radio
            .Skip(activeIndex + 1)
            .TakeWhile(item => !item.IsPodcastSegment)
            .Select(item => item.PlayUri)
            .ToList();

        _ = AppRemotePlayback.SyncQueueAsync(current.PlayUri, upcomingRun);
    }

    internal void Stop(string why)
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return;
            DiagnosticLog.Write($"[Radio] stopped by the app: {why}");
            _state.Stop();
            CaptureState();
        }

        RadioBackgroundService.Stop();
        RaiseActiveItem(null);
    }

    internal int? RemainingSegmentMs()
    {
        lock (_lock)
        {
            return _state?.RemainingSegmentMs(DateTime.UtcNow);
        }
    }

    internal bool AdvanceManually()
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return false;

            Apply(_state.AdvanceManually(DateTime.UtcNow));
            CaptureState();
            return true;
        }
    }

    private static readonly TimeSpan SnapshotMaxAge = TimeSpan.FromMilliseconds(RadioTuning.SNAPSHOT_STALE_MS);

    private bool _holdingForStaleSnapshot;
    private DateTime _holdingSinceUtc;

    private PlaybackSnapshot? _lastLoggedSample;

    private static bool SnapshotUsable =>
        PlaybackStateStore.HasActivePushSource?.Invoke() == true || PlaybackStateStore.Instance.IsFresh(SnapshotMaxAge);

    internal void Tick()
    {
        if (!SnapshotUsable)
        {
            if (!_holdingForStaleSnapshot && IsActive)
            {
                _holdingForStaleSnapshot = true;
                _holdingSinceUtc = DateTime.UtcNow;
                DiagnosticLog.Write("[Radio] snapshot stale (App Remote not connected, no fresh poll), radio holding");
            }
            return;
        }

        if (_holdingForStaleSnapshot)
        {
            _holdingForStaleSnapshot = false;
            if (IsActive) DiagnosticLog.Write($"[Radio] snapshot fresh again after {(DateTime.UtcNow - _holdingSinceUtc).TotalSeconds:F0}s, radio resuming");

            lock (_lock) _state?.NotifyObservationGap(DateTime.UtcNow);
        }

        var lockWait = System.Diagnostics.Stopwatch.StartNew();
        lock (_lock)
        {
            lockWait.Stop();
            if (lockWait.ElapsedMilliseconds > 200)
                DiagnosticLog.Write($"[Radio] tick: waited {lockWait.ElapsedMilliseconds}ms for the lock");

            if (_state == null || !_state.IsActive) return;

            var snapshot = PlaybackStateStore.Instance.Snapshot;
            LogSampleIfNew(snapshot);

            switch (AppRemotePlayback.CheckRemovedSong(snapshot.CurrentItemUri))
            {
                case AppRemotePlayback.RemovedSongCheck.SkipNow:
                    DiagnosticLog.Write($"[Radio] skipping removed {Label(snapshot.CurrentItemUri)}");
                    PlaybackCommands.SkipNext?.Invoke();
                    return;
                case AppRemotePlayback.RemovedSongCheck.SkipInFlight:
                    return;
            }

            Apply(_state.Tick(snapshot, DateTime.UtcNow));
            CaptureState();
        }
    }

    // One line per new playback sample the radio acts on (the UiLoop re-ticks the same sample
    // in between, which adds nothing). Together with the decision lines this is enough to replay
    // a session into RadioHarness.
    private void LogSampleIfNew(PlaybackSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, _lastLoggedSample)) return;
        _lastLoggedSample = snapshot;

        DiagnosticLog.Write(
            $"[Sample] active #{_state.ActiveIndex} | {(snapshot.IsPlaying ? "playing" : "paused")} " +
            $"{snapshot.CurrentItemUri ?? "nothing"} {snapshot.ProgressMs}/{snapshot.DurationMs}");
    }

    /// <summary>A plain-text dump of what the radio thinks is going on, for bug reports.</summary>
    internal string DescribeState()
    {
        var text = new System.Text.StringBuilder();
        var remaining = RemainingItems;

        text.AppendLine($"Radio active: {IsActive}");
        if (_holdingForStaleSnapshot) text.AppendLine($"Holding for a stale snapshot since {_holdingSinceUtc.ToLocalTime():HH:mm:ss}");

        lock (_lock)
        {
            if (_state != null)
            {
                text.AppendLine($"Active index: {_state.ActiveIndex}");
                text.AppendLine($"Last decision: {_state.LastReason ?? "none"}");
            }
        }

        if (remaining == null || remaining.Count == 0) return text.ToString();

        text.AppendLine("Radio list from the active item on:");
        for (int i = 0; i < remaining.Count; i++)
        {
            var item = remaining[i];
            var kind = item.IsPodcastSegment ? $"podcast from {item.PositionMs / 1000}s: " : "";
            text.AppendLine($"  {(i == 0 ? ">" : " ")} {kind}{Label(item.PlayUri)}");
        }
        return text.ToString();
    }


    private void Apply(RadioTickResult result)
    {
        while (true)
        {
            if (result.ActiveItemChanged)
            {
                DiagnosticLog.Write($"[Radio] advancing to #{_state.ActiveIndex} {Label(_state.ActiveItem?.PlayUri)}; why: {_state.LastReason}");
                RaiseActiveItem(_state.ActiveItem as RadioItem);
            }

            switch (result.Action)
            {
                case RadioTickAction.StartActive:
                    if (!result.ActiveItemChanged) DiagnosticLog.Write($"[Radio] {_state.LastReason}");
                    DiagnosticLog.Write($"[Radio] issuing playback for {Label(_state.ActiveItem?.PlayUri)}");
                    var outcome = IssuePlayback(_state.ActiveItem as RadioItem);
                    DiagnosticLog.Write($"[Radio] issued {_state.ActiveItem?.PlayUri}: {outcome}");
                    result = _state.ReportStartOutcome(outcome, DateTime.UtcNow);
                    continue;

                case RadioTickAction.SkipUnexpected:
                    DiagnosticLog.Write($"[Radio] skipping {Label(_state.SkippingUri)}; why: {_state.LastReason}");
                    PlaybackCommands.SkipNext?.Invoke();
                    return;

                case RadioTickAction.Stop:
                    DiagnosticLog.Write($"[Radio] stopping; why: {_state.LastReason}");
                    RadioBackgroundService.Stop();
                    RaiseActiveItem(null);
                    return;

                default:
                    return;
            }
        }
    }

    private PlaybackAttempt IssuePlayback(RadioItem item)
    {
        if (item == null) return PlaybackAttempt.Failed;

        if (PlaybackCommands.PlayUri != null)
        {
            var songRun = item.IsPodcastSegment ? null : SongRunFrom(item);
            _ = PlayViaAppRemoteAsync(item, songRun);
            return PlaybackAttempt.Success;
        }

        DiagnosticLog.Write("[Radio] App Remote path unavailable, falling back to Web API");
        return IssueViaWebApi(item, SongRunFrom(item));
    }

    private static async Task PlayViaAppRemoteAsync(RadioItem item, List<string> songRun)
    {
        if (PlaybackStateStore.Instance.ShuffleOn) PlaybackCommands.SetShuffle?.Invoke(false);

        var played = await AppRemotePlayback.TryPlayItemAsync(item, songRun);
        DiagnosticLog.Write(played
            ? $"[Radio] played {item.PlayUri} via App Remote"
            : $"[Radio] App Remote couldn't play {item.PlayUri}, leaving it to the retry window");
    }

    private static PlaybackAttempt IssueViaWebApi(RadioItem item, List<string> songRun)
    {
        var api = APICaller.Instance;
        if (api == null) return PlaybackAttempt.Failed;

        var deviceId = ResolveDeviceId(api);

        if (string.IsNullOrEmpty(deviceId))
        {
            DiagnosticLog.Write("[Radio] no phone device to target, waking Spotify and deferring");
            _ = PlaybackCommands.WakeSpotify?.Invoke();
            return PlaybackAttempt.Failed;
        }

        if (PlaybackStateStore.Instance.ShuffleOn) api.SetPlaybackShuffle(false);

        if (item.IsPodcastSegment)
        {
            var rewoundMs = Math.Max(0, item.PositionMs - RadioTuning.RESUME_REWIND_MS);
            DiagnosticLog.Write($"[Radio] Web API calling PlayUriAtPosition {item.PlayUri}@{rewoundMs}");
            var attempt = api.PlayUriAtPosition(item.PlayUri, rewoundMs, deviceId);
            DiagnosticLog.Write($"[Radio] Web API PlayUriAtPosition returned {attempt}");

            if (attempt == PlaybackAttempt.Success) PlaybackCommands.SeekTo?.Invoke(rewoundMs);

            return attempt;
        }

        DiagnosticLog.Write($"[Radio] Web API calling PlayUris ({songRun.Count} uris)");
        var result = api.PlayUris(songRun, deviceId);
        DiagnosticLog.Write($"[Radio] Web API PlayUris returned {result}");
        return result;
    }

    internal static string? ResolveDeviceId(APICaller api)
    {
        var selectedId = StorageHandler.SelectedDeviceId;

        DiagnosticLog.Write("[Radio] Web API calling GetDevices");
        var devices = api.GetDevices();
        DiagnosticLog.Write($"[Radio] Web API GetDevices returned {devices?.Count.ToString() ?? "null"}");
        string? deviceId;
        if (devices == null || devices.Count == 0)
        {
            deviceId = !string.IsNullOrEmpty(selectedId) ? selectedId : PlaybackDeviceLookup.LastKnownPhoneDeviceId;
        }
        else
        {
            deviceId = DeviceResolver.Resolve(devices, selectedId);
            DiagnosticLog.Write($"[Radio] devices: {PlaybackDeviceLookup.Describe(devices, selectedId)}");
        }

        DiagnosticLog.Write($"[Radio] resolved device {deviceId}");
        return deviceId;
    }

    private List<string> SongRunFrom(RadioItem item)
    {
        var run = new List<string>();
        foreach (var queued in _state.RemainingItems)
        {
            if (queued.IsPodcastSegment) break;
            run.Add(queued.PlayUri);
        }
        return run;
    }
}
