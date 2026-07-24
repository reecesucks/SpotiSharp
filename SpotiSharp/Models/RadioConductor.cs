using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public class RadioConductor
{
    private static RadioConductor _instance;
    public static RadioConductor Instance => _instance ??= new RadioConductor();

    private readonly object _lock = new object();

    private RadioTickState _state;

    internal event Action<RadioItem> ActiveItemChanged;

    internal bool IsActive
    {
        get { lock (_lock) { return _state != null && _state.IsActive; } }
    }

    internal RadioItem CurrentItem
    {
        get { lock (_lock) { return _state?.ActiveItem as RadioItem; } }
    }

    internal List<RadioItem> RemainingItems
    {
        get
        {
            lock (_lock)
            {
                if (_state == null || !_state.IsActive) return null;
                return _state.RemainingItems.Cast<RadioItem>().ToList();
            }
        }
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

        lock (_lock)
        {
            _state = new RadioTickState(radio, startIndex, DateTime.UtcNow, alreadyIssued: true);
        }

        RadioBackgroundService.Start();
        RaiseActiveItem(radio[startIndex]);
    }

    internal void Resync(List<RadioItem> radio, int activeIndex)
    {
        lock (_lock)
        {
            _state?.Resync(radio, activeIndex);
        }
    }

    internal void Stop()
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return;
            _state.Stop();
        }

        RadioBackgroundService.Stop();
        RaiseActiveItem(null);
    }

    internal bool AdvanceManually()
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return false;

            Apply(_state.AdvanceManually(DateTime.UtcNow));
            return true;
        }
    }

    private void Tick()
    {
        lock (_lock)
        {
            if (_state == null || !_state.IsActive) return;

            Apply(_state.Tick(PlaybackStateStore.Instance.Snapshot, DateTime.UtcNow));
        }
    }


    private void Apply(RadioTickResult result)
    {
        while (true)
        {
            if (result.ActiveItemChanged) RaiseActiveItem(_state.ActiveItem as RadioItem);

            switch (result.Action)
            {
                case RadioTickAction.StartActive:
                    var outcome = IssuePlayback(_state.ActiveItem as RadioItem);
                    result = _state.ReportStartOutcome(outcome, DateTime.UtcNow);
                    continue;

                case RadioTickAction.Stop:
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

        if (PlaybackStateStore.Instance.ShuffleOn) api.SetPlaybackShuffle(false);

        if (item.IsPodcastSegment)
        {
            return api.PlayUriAtPosition(item.PlayUri, Math.Max(0, item.PositionMs - RadioTuning.RESUME_REWIND_MS), deviceId);
        }

        return api.PlayUris(SongRunFrom(item), deviceId);
    }

    private static string ResolveDeviceId(APICaller api)
    {
        var deviceId = PlaybackStateStore.Instance.ActiveDeviceId;
        if (!string.IsNullOrEmpty(deviceId)) return deviceId;

        var ids = api.GetDeviceIds();
        return ids.phone ?? ids.any;
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
