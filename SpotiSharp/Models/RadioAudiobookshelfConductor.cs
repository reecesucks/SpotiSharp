using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public class RadioAudiobookshelfConductor
{
    private static RadioAudiobookshelfConductor _instance;
    public static RadioAudiobookshelfConductor Instance => _instance ??= new RadioAudiobookshelfConductor();

    private readonly object _lock = new object();

    private RadioTickState _state;

    // Lock-free views for the UI thread, refreshed under _lock on every state change. Reading
    // these never waits on _lock — which Tick holds across the blocking playback network call —
    // so opening the radio page never stalls behind an in-flight track transition.
    private volatile bool _activeSnapshot;
    private volatile RadioAudiobookshelfItem _currentItemSnapshot;
    private volatile List<RadioAudiobookshelfItem> _remainingSnapshot;

    internal event Action<RadioAudiobookshelfItem> ActiveItemChanged;

    internal bool IsActive => _activeSnapshot;

    internal RadioAudiobookshelfItem CurrentItem => _currentItemSnapshot;

    internal List<RadioAudiobookshelfItem> RemainingItems => _remainingSnapshot;

    // Must be called while holding _lock.
    private void CaptureState()
    {
        bool active = _state != null && _state.IsActive;
        _activeSnapshot = active;
        _currentItemSnapshot = active ? _state.ActiveItem as RadioAudiobookshelfItem : null;
        _remainingSnapshot = active ? _state.RemainingItems.Cast<RadioAudiobookshelfItem>().ToList() : null;
    }

    private RadioAudiobookshelfConductor()
    {
        UiLoop.Instance.OnRefreshUi += Tick;
    }

    private void RaiseActiveItem(RadioAudiobookshelfItem item)
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

    private static void RememberLabels(List<RadioAudiobookshelfItem> radio)
    {
        var labels = new Dictionary<string, string>(_labels);
        foreach (var item in radio)
        {
            if (!string.IsNullOrEmpty(item.PlayUri)) labels[item.PlayUri] = $"{item.Title} / {item.Subtitle}";
        }
        _labels = labels;
    }

    internal void Start(List<RadioAudiobookshelfItem> radio, int startIndex)
    {
        if (radio == null || startIndex < 0 || startIndex >= radio.Count) return;

        RememberLabels(radio);
        DiagnosticLog.Write($"[Radio] conducting {radio.Count} items from index {startIndex} ({Label(radio[startIndex].PlayUri)})");

        lock (_lock)
        {
            _state = new RadioTickState(radio, startIndex, DateTime.UtcNow, alreadyIssued: true, RadioAudiobookshelfConfigModel.PodcastSegmentLengthMs);
            CaptureState();
        }

        RadioAudiobookshelfBackgroundService.Start();
        RaiseActiveItem(radio[startIndex]);
    }

    internal void Resync(List<RadioAudiobookshelfItem> radio, int activeIndex, string why)
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

    /// <summary>
    /// How many songs to put behind the one being started. In the mix a run only lasts until the
    /// next podcast segment, a few songs away - but music-only has no segments to stop at, so the
    /// run would be the whole rest of the radio, and every song in it is a separate App Remote
    /// queue call. Capping it trades a queue top-up every so often for not sending dozens at once.
    /// </summary>
    internal const int MAX_SONG_RUN = 15;

    private static void SyncSpotifyQueue(List<RadioAudiobookshelfItem> radio, int activeIndex)
    {
        if (PlaybackCommands.QueueUri == null || radio == null || activeIndex < 0 || activeIndex >= radio.Count) return;

        var current = radio[activeIndex];
        if (current.IsPodcastSegment) return;

        var upcomingRun = radio
            .Skip(activeIndex + 1)
            .TakeWhile(item => !item.IsPodcastSegment)
            .Take(MAX_SONG_RUN)
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

        RadioAudiobookshelfBackgroundService.Stop();
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
        // AudioBookshelf plays through its own ExoPlayer instance, not Spotify - the App Remote/poll
        // snapshot below says nothing about it (staleness included), so an AB-active item gets its own
        // tick path entirely, and never touches _state.Tick/the Spotify snapshot machinery.
        if (CurrentItem?.SourceKind == PodcastSourceKind.Audiobookshelf)
        {
            TickAudiobookshelf();
            return;
        }

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
                case RemovedSongCheck.SkipNow:
                    DiagnosticLog.Write($"[Radio] skipping removed {Label(snapshot.CurrentItemUri)}");
                    PlaybackCommands.SkipNext?.Invoke();
                    return;
                case RemovedSongCheck.SkipInFlight:
                    return;
            }

            Apply(_state.Tick(snapshot, DateTime.UtcNow));
            CaptureState();
        }
    }

    /// <summary>
    /// The AudioBookshelf equivalent of <see cref="RadioTickState.Tick"/>'s podcast-segment-end
    /// handling, but reading position/duration straight off <see cref="AudiobookshelfPlaybackCommands"/>
    /// instead of a Spotify snapshot - never calls <c>_state.Tick</c>, only the already-public
    /// <c>AdvanceManually</c> (the same one the skip button uses) once the active segment runs out.
    /// Everything else (song runs, Spotify's own segments, queue sync) is untouched by this.
    /// </summary>
    private void TickAudiobookshelf()
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return;

            var active = _state.ActiveItem as RadioAudiobookshelfItem;
            // The active item moved on since the lock-free check above (e.g. the Spotify branch beat
            // us to it) - next tick will pick the right path.
            if (active == null || active.SourceKind != PodcastSourceKind.Audiobookshelf) return;

            bool isPlaying = AudiobookshelfPlaybackCommands.GetIsPlaying?.Invoke() ?? false;
            int positionMs = AudiobookshelfPlaybackCommands.GetPositionMs?.Invoke() ?? 0;

            SaveAudiobookshelfProgress(active, isPlaying, positionMs);

            int? segmentEndMs = AudiobookshelfSegmentEndMs(active);
            if (segmentEndMs == null) return;

            if (isPlaying && positionMs >= segmentEndMs.Value - RadioTuning.END_TOLERANCE_MS)
            {
                DiagnosticLog.Write($"[Radio] AudioBookshelf segment reached its end ({positionMs} of {segmentEndMs}ms)");
                Apply(_state.AdvanceManually(DateTime.UtcNow));
                CaptureState();
            }
        }
    }

    private DateTime _lastAbProgressSaveUtc = DateTime.MinValue;
    private bool _wasAbPlaying;
    private string? _abProgressEpisodeId;
    private static readonly TimeSpan AbProgressSaveInterval = TimeSpan.FromSeconds(15);

    /// <summary>Mirrors <see cref="AudiobookshelfPodcastsViewModel"/>'s own polling save - without this,
    /// listening to an AudioBookshelf episode through Radio never tells the server anything at all, so
    /// an episode finished only through Radio never gets marked finished (or shows any progress) on the
    /// server. Saves periodically while playing and immediately on a pause, same cadence as the
    /// standalone Podcasts page.</summary>
    private void SaveAudiobookshelfProgress(RadioAudiobookshelfItem active, bool isPlaying, int positionMs)
    {
        var episodeId = AudiobookshelfPodcastPlaybackAdapter.ParseEpisodeId(active.PlayUri);
        if (episodeId == null) return;

        if (_abProgressEpisodeId != episodeId)
        {
            _abProgressEpisodeId = episodeId;
            _wasAbPlaying = false;
            _lastAbProgressSaveUtc = DateTime.MinValue;
        }

        var now = DateTime.UtcNow;
        var justPaused = _wasAbPlaying && !isPlaying;
        _wasAbPlaying = isPlaying;
        if (!justPaused && now - _lastAbProgressSaveUtc < AbProgressSaveInterval) return;
        _lastAbProgressSaveUtc = now;

        var episode = (AudiobookshelfPodcastsModel.GetDiskCachedEpisodes() ?? new List<AudiobookshelfEpisode>())
            .FirstOrDefault(candidate => candidate.EpisodeId == episodeId);
        if (episode == null) return;

        int durationMs = AudiobookshelfPlaybackCommands.GetDurationMs?.Invoke() ?? 0;
        if (durationMs <= 0) durationMs = episode.DurationMs;

        _ = AudiobookshelfProgressSyncModel.SaveOrQueueAsync(episode.LibraryItemId, episodeId, positionMs, durationMs);
    }

    /// <summary>Where the active AudioBookshelf segment ends: the episode's own duration for a final
    /// segment, or the next podcast segment's start otherwise - segments are built back-to-back (see
    /// <see cref="RadioAudiobookshelfModel.Generate"/>), so that start is exactly this segment's end.</summary>
    private int? AudiobookshelfSegmentEndMs(RadioAudiobookshelfItem active)
    {
        if (active.IsFinalPodcastSegment)
        {
            int durationMs = AudiobookshelfPlaybackCommands.GetDurationMs?.Invoke() ?? 0;
            return durationMs > 0 ? durationMs : null;
        }

        var nextSegment = _state.RemainingItems.Skip(1).FirstOrDefault(item => item.IsPodcastSegment);
        return nextSegment?.PositionMs;
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
                RaiseActiveItem(_state.ActiveItem as RadioAudiobookshelfItem);
            }

            switch (result.Action)
            {
                case RadioTickAction.StartActive:
                    if (!result.ActiveItemChanged) DiagnosticLog.Write($"[Radio] {_state.LastReason}");
                    DiagnosticLog.Write($"[Radio] issuing playback for {Label(_state.ActiveItem?.PlayUri)}");
                    var outcome = IssuePlayback(_state.ActiveItem as RadioAudiobookshelfItem);
                    DiagnosticLog.Write($"[Radio] issued {_state.ActiveItem?.PlayUri}: {outcome}");
                    result = _state.ReportStartOutcome(outcome, DateTime.UtcNow);
                    continue;

                case RadioTickAction.SkipUnexpected:
                    DiagnosticLog.Write($"[Radio] skipping {Label(_state.SkippingUri)}; why: {_state.LastReason}");
                    PlaybackCommands.SkipNext?.Invoke();
                    return;

                case RadioTickAction.Stop:
                    DiagnosticLog.Write($"[Radio] stopping; why: {_state.LastReason}");
                    RadioAudiobookshelfBackgroundService.Stop();
                    RaiseActiveItem(null);
                    return;

                default:
                    return;
            }
        }
    }

    private PlaybackAttempt IssuePlayback(RadioAudiobookshelfItem item)
    {
        if (item == null) return PlaybackAttempt.Failed;

        if (item.IsPodcastSegment)
        {
            return PodcastPlaybackAdapters.For(item.SourceKind).Play(item);
        }

        if (PlaybackCommands.PlayUri != null)
        {
            var songRun = SongRunFrom(item);
            _ = PlayViaAppRemoteAsync(item, songRun);
            return PlaybackAttempt.Success;
        }

        DiagnosticLog.Write("[Radio] App Remote path unavailable, falling back to Web API");
        return IssueViaWebApi(item, SongRunFrom(item));
    }

    internal static async Task PlayViaAppRemoteAsync(RadioAudiobookshelfItem item, List<string> songRun)
    {
        if (PlaybackStateStore.Instance.ShuffleOn) PlaybackCommands.SetShuffle?.Invoke(false);

        var played = await AppRemotePlayback.TryPlayItemAsync(item, songRun);
        DiagnosticLog.Write(played
            ? $"[Radio] played {item.PlayUri} via App Remote"
            : $"[Radio] App Remote couldn't play {item.PlayUri}, leaving it to the retry window");
    }

    internal static PlaybackAttempt IssueViaWebApi(RadioAudiobookshelfItem item, List<string> songRun)
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

    private List<string> SongRunFrom(RadioAudiobookshelfItem item)
    {
        var run = new List<string>();
        foreach (var queued in _state.RemainingItems)
        {
            if (queued.IsPodcastSegment) break;
            run.Add(queued.PlayUri);
            if (run.Count == MAX_SONG_RUN) break;
        }
        return run;
    }
}
