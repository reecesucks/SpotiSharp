using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class RadioConductor
{
    private const int END_TOLERANCE_MS = 5000;
    private const int RESUME_REWIND_MS = 10000;
    private const int TRANSITION_GRACE_TICKS = 5;
    private const int EMPTY_TICKS_TO_ADVANCE = 2;
    private const int MAX_UNAVAILABLE_SKIPS = 10;

    private static RadioConductor _instance;
    public static RadioConductor Instance => _instance ??= new RadioConductor();

    private readonly object _lock = new object();

    private List<RadioItem> _radio;
    private int _activeIndex = -1;
    private int _graceRemaining;
    private int _emptyTicks;
    private int _lastObservedProgressMs;
    private int _lastObservedDurationMs;

    internal event Action<RadioItem> ActiveItemChanged;

    internal bool IsActive
    {
        get { lock (_lock) { return _radio != null; } }
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
            _radio = radio;
            _activeIndex = startIndex;
            _graceRemaining = TRANSITION_GRACE_TICKS;
            _emptyTicks = 0;
            _lastObservedProgressMs = 0;
            _lastObservedDurationMs = 0;
        }

        RadioBackgroundService.Start();
        RaiseActiveItem(radio[startIndex]);
    }

    internal void Stop()
    {
        lock (_lock)
        {
            StopLocked();
        }
    }

    internal bool AdvanceManually()
    {
        lock (_lock)
        {
            if (_radio == null || _activeIndex < 0) return false;

            AdvanceLocked();
            return true;
        }
    }

    private void StopLocked()
    {
        _radio = null;
        _activeIndex = -1;

        RadioBackgroundService.Stop();
        RaiseActiveItem(null);
    }

    private void Tick()
    {
        lock (_lock)
        {
            if (_radio == null || _activeIndex < 0) return;

            var state = PlaybackStateStore.Instance;

            if (string.IsNullOrEmpty(state.CurrentItemUri))
            {
                if (_graceRemaining > 0)
                {
                    _graceRemaining--;
                    return;
                }
                if (++_emptyTicks >= EMPTY_TICKS_TO_ADVANCE && ActiveItemNearEnd())
                {
                    _emptyTicks = 0;
                    AdvanceLocked();
                }
                return;
            }

            _emptyTicks = 0;

            if (state.CurrentItemUri == _radio[_activeIndex].PlayUri)
            {
                HandleActiveItem(state);
                return;
            }

            int runIndex = IndexInActiveSongRun(state.CurrentItemUri);
            if (runIndex >= 0)
            {
                bool moved = runIndex != _activeIndex;
                _activeIndex = runIndex;
                _graceRemaining = 0;
                _lastObservedProgressMs = state.ProgressMs;
                _lastObservedDurationMs = state.DurationMs;
                if (moved) RaiseActiveItem(_radio[runIndex]);
                return;
            }

            if (_graceRemaining > 0)
            {
                _graceRemaining--;
                return;
            }

            StopLocked();
        }
    }

    private void HandleActiveItem(PlaybackStateStore state)
    {
        var active = _radio[_activeIndex];
        _graceRemaining = 0;

        int bestKnownProgress = Math.Max(state.ProgressMs, _lastObservedProgressMs);
        if (state.DurationMs > 0) _lastObservedDurationMs = state.DurationMs;
        if (state.IsPlaying) _lastObservedProgressMs = state.ProgressMs;

        int endMs = ActiveEndMs(active);
        if (endMs <= 0) return;

        if (state.IsPlaying && active.IsPodcastSegment && state.ProgressMs >= endMs)
        {
            AdvanceLocked();
            return;
        }

        if (!state.IsPlaying && bestKnownProgress >= endMs - END_TOLERANCE_MS)
        {
            AdvanceLocked();
        }
    }

    private int ActiveEndMs(RadioItem active)
    {
        if (active.IsPodcastSegment)
        {
            int end = active.PositionMs + RadioModel.SEGMENT_LENGTH_MS;
            return _lastObservedDurationMs > 0 ? Math.Min(end, _lastObservedDurationMs) : end;
        }
        return _lastObservedDurationMs;
    }

    private bool ActiveItemNearEnd()
    {
        int endMs = ActiveEndMs(_radio[_activeIndex]);
        return endMs > 0 && _lastObservedProgressMs >= endMs - END_TOLERANCE_MS;
    }

    private int IndexInActiveSongRun(string uri)
    {
        if (_radio[_activeIndex].IsPodcastSegment) return -1;

        int runStart = _activeIndex;
        while (runStart > 0 && !_radio[runStart - 1].IsPodcastSegment) runStart--;

        for (int i = runStart; i < _radio.Count && !_radio[i].IsPodcastSegment; i++)
        {
            if (_radio[i].PlayUri == uri) return i;
        }
        return -1;
    }

    private void AdvanceLocked()
    {
        // Move to the next item, skipping podcast segments the API reports as unavailable (an
        // episode removed from Spotify / region-locked). The skip count is bounded so a run of dead
        // items, or a broader outage misreported as "unavailable", can't churn through the queue.
        for (int skips = 0; skips <= MAX_UNAVAILABLE_SKIPS; skips++)
        {
            int nextIndex = _activeIndex + 1;
            if (nextIndex >= _radio.Count)
            {
                StopLocked();
                return;
            }

            _activeIndex = nextIndex;
            _graceRemaining = TRANSITION_GRACE_TICKS;
            _emptyTicks = 0;
            _lastObservedProgressMs = 0;
            _lastObservedDurationMs = 0;

            var next = _radio[nextIndex];
            RaiseActiveItem(next);

            var api = APICaller.Instance;
            if (api == null) return;

            api.SetPlaybackShuffle(false);
            if (next.IsPodcastSegment)
            {
                var outcome = api.PlayUriAtPosition(next.PlayUri, Math.Max(0, next.PositionMs - RESUME_REWIND_MS));
                if (outcome == PlaybackAttempt.Unavailable) continue; // dead episode — skip to the next segment
                return;
            }

            var run = new List<string>();
            for (int i = nextIndex; i < _radio.Count && !_radio[i].IsPodcastSegment; i++)
            {
                run.Add(_radio[i].PlayUri);
            }
            api.PlayUris(run);
            return;
        }

        // too many consecutive unavailable items — stop rather than spin through the whole queue
        StopLocked();
    }
}
