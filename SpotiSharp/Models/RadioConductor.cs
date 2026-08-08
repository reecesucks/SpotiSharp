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

    internal void Start(List<RadioItem> radio, int startIndex)
    {
        if (radio == null || startIndex < 0 || startIndex >= radio.Count) return;

        DiagnosticLog.Write($"[Radio] conducting {radio.Count} items from index {startIndex} ({radio[startIndex].PlayUri})");

        lock (_lock)
        {
            _state = new RadioTickState(radio, startIndex, DateTime.UtcNow, alreadyIssued: true);
            CaptureState();
        }

        RadioBackgroundService.Start();
        RaiseActiveItem(radio[startIndex]);
    }

    internal void Resync(List<RadioItem> radio, int activeIndex)
    {
        lock (_lock)
        {
            _state?.Resync(radio, activeIndex);
            CaptureState();
        }
    }

    internal void Stop()
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return;
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

    private static bool SnapshotUsable =>
        PlaybackStateStore.HasActivePushSource?.Invoke() == true || PlaybackStateStore.Instance.IsFresh(SnapshotMaxAge);

    internal void Tick()
    {
        if (!SnapshotUsable)
        {
            if (!_holdingForStaleSnapshot && IsActive)
            {
                _holdingForStaleSnapshot = true;
                DiagnosticLog.Write("[Radio] snapshot stale, radio holding");
            }
            return;
        }

        if (_holdingForStaleSnapshot)
        {
            _holdingForStaleSnapshot = false;
            if (IsActive) DiagnosticLog.Write("[Radio] snapshot fresh again, radio resuming");
        }

        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return;

            Apply(_state.Tick(PlaybackStateStore.Instance.Snapshot, DateTime.UtcNow));
            CaptureState();
        }
    }


    private void Apply(RadioTickResult result)
    {
        while (true)
        {
            if (result.ActiveItemChanged)
            {
                DiagnosticLog.Write($"[Radio] advancing to {_state.ActiveItem?.PlayUri}");
                RaiseActiveItem(_state.ActiveItem as RadioItem);
            }

            switch (result.Action)
            {
                case RadioTickAction.StartActive:
                    var outcome = IssuePlayback(_state.ActiveItem as RadioItem);
                    DiagnosticLog.Write($"[Radio] issued {_state.ActiveItem?.PlayUri}: {outcome}");
                    result = _state.ReportStartOutcome(outcome, DateTime.UtcNow);
                    continue;

                case RadioTickAction.Stop:
                    DiagnosticLog.Write("[Radio] stopping");
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
        var api = APICaller.Instance;
        if (item == null || api == null) return PlaybackAttempt.Failed;

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
            var attempt = api.PlayUriAtPosition(item.PlayUri, rewoundMs, deviceId);

            if (attempt == PlaybackAttempt.Success) PlaybackCommands.SeekTo?.Invoke(rewoundMs);

            return attempt;
        }

        return api.PlayUris(SongRunFrom(item), deviceId);
    }

    internal static string? ResolveDeviceId(APICaller api)
    {
        var selectedId = StorageHandler.SelectedDeviceId;

        var devices = api.GetDevices();
        string? deviceId;
        if (devices == null || devices.Count == 0)
        {
            deviceId = !string.IsNullOrEmpty(selectedId) ? selectedId : PlaybackDeviceLookup.LastKnownPhoneDeviceId;
        }
        else
        {
            deviceId = DeviceResolver.Resolve(devices, selectedId);
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
