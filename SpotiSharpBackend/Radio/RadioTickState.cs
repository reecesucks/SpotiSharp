namespace SpotiSharpBackend.Radio;

public enum RadioTickAction
{
    None,
    StartActive,
    Stop
}

public readonly record struct RadioTickResult(RadioTickAction Action, bool ActiveItemChanged)
{
    public static readonly RadioTickResult Nothing = new RadioTickResult(RadioTickAction.None, false);
    public static readonly RadioTickResult Moved = new RadioTickResult(RadioTickAction.None, true);

    public static RadioTickResult Start(bool activeItemChanged) => new RadioTickResult(RadioTickAction.StartActive, activeItemChanged);
}
public sealed class RadioTickState
{
    private IReadOnlyList<IRadioQueueItem>? _queue;
    private int _activeIndex = -1;

    private int _lastObservedProgressMs;
    private int _lastObservedDurationMs;
    private DateTime _lastObservedAtUtc;
    private bool _lastObservedWasPlaying;

    private DateTime _startIssuedAtUtc;
    private int _startAttempts;
    private bool _startConfirmed;
    private DateTime? _silenceSinceUtc;

    private int _unavailableSkips;

    public RadioTickState(IReadOnlyList<IRadioQueueItem> queue, int startIndex, DateTime nowUtc, bool alreadyIssued)
    {
        if (queue == null || startIndex < 0 || startIndex >= queue.Count) return;

        _queue = queue;
        SetActive(startIndex, nowUtc);
        if (alreadyIssued) _startAttempts = 1;
    }

    public bool IsActive => _queue != null;
    public int ActiveIndex => _activeIndex;
    public IRadioQueueItem? ActiveItem => _queue != null && _activeIndex >= 0 ? _queue[_activeIndex] : null;

    public IReadOnlyList<IRadioQueueItem> RemainingItems
    {
        get
        {
            if (_queue == null || _activeIndex < 0) return Array.Empty<IRadioQueueItem>();
            return _queue.Skip(_activeIndex).ToList();
        }
    }

    public RadioTickResult Tick(PlaybackSnapshot state, DateTime nowUtc)
    {
        if (_queue == null || _activeIndex < 0) return RadioTickResult.Nothing;

        if (state.CurrentItemUri == _queue[_activeIndex].PlayUri) return HandleActiveItem(state, nowUtc);

        int runIndex = IndexInActiveSongRun(state.CurrentItemUri);
        if (runIndex >= 0) return MoveWithinRun(runIndex, state, nowUtc);


        if (!_startConfirmed)
        {
            if (nowUtc < _startIssuedAtUtc.AddMilliseconds(RadioTuning.START_GRACE_MS)) return RadioTickResult.Nothing;
            return RetryOrSkipActive(nowUtc);
        }

        if (ActivePlayedThrough(nowUtc)) return Advance(nowUtc);

        if (!string.IsNullOrEmpty(state.CurrentItemUri)) return StopResult();

        _silenceSinceUtc ??= nowUtc;
        if (nowUtc >= _silenceSinceUtc.Value.AddMilliseconds(RadioTuning.DEAD_AIR_TIMEOUT_MS)) return StopResult();

        return RadioTickResult.Nothing;
    }

    public RadioTickResult ReportStartOutcome(PlaybackAttempt outcome, DateTime nowUtc)
    {
        if (_queue == null || _activeIndex < 0) return RadioTickResult.Nothing;

        _startAttempts++;
        _startIssuedAtUtc = nowUtc;

        if (outcome != PlaybackAttempt.Unavailable)
        {
            _unavailableSkips = 0;
            return RadioTickResult.Nothing;
        }

        if (++_unavailableSkips > RadioTuning.MAX_UNAVAILABLE_SKIPS) return StopResult();

        return Advance(nowUtc);
    }

    public RadioTickResult AdvanceManually(DateTime nowUtc)
    {
        if (_queue == null || _activeIndex < 0) return RadioTickResult.Nothing;

        _unavailableSkips = 0;
        return Advance(nowUtc);
    }

    public void Resync(IReadOnlyList<IRadioQueueItem> queue, int activeIndex)
    {
        if (_queue == null || queue == null || activeIndex < 0 || activeIndex >= queue.Count) return;

        _queue = queue;
        _activeIndex = activeIndex;
    }

    public void Stop()
    {
        _queue = null;
        _activeIndex = -1;
    }

    private RadioTickResult HandleActiveItem(PlaybackSnapshot state, DateTime nowUtc)
    {
        var active = _queue![_activeIndex];

        _startConfirmed = true;
        _startAttempts = 0;
        _unavailableSkips = 0;
        _silenceSinceUtc = null;

        if (state.DurationMs > 0) _lastObservedDurationMs = state.DurationMs;

        _lastObservedWasPlaying = state.IsPlaying;
        if (state.IsPlaying)
        {
            _lastObservedProgressMs = state.ProgressMs;
            _lastObservedAtUtc = nowUtc;
        }

        int endMs = ActiveEndMs(active);
        if (endMs <= 0) return RadioTickResult.Nothing;

        if (state.IsPlaying && active.IsPodcastSegment && state.ProgressMs >= endMs) return Advance(nowUtc);

        if (!state.IsPlaying && Math.Max(state.ProgressMs, _lastObservedProgressMs) >= endMs - RadioTuning.END_TOLERANCE_MS)
        {
            return Advance(nowUtc);
        }

        return RadioTickResult.Nothing;
    }

    private RadioTickResult MoveWithinRun(int runIndex, PlaybackSnapshot state, DateTime nowUtc)
    {
        bool moved = runIndex != _activeIndex;
        _activeIndex = runIndex;

        _startConfirmed = true;
        _startAttempts = 0;
        _unavailableSkips = 0;
        _silenceSinceUtc = null;

        _lastObservedProgressMs = state.ProgressMs;
        _lastObservedDurationMs = state.DurationMs;
        _lastObservedAtUtc = nowUtc;
        _lastObservedWasPlaying = state.IsPlaying;

        return moved ? RadioTickResult.Moved : RadioTickResult.Nothing;
    }

    private RadioTickResult RetryOrSkipActive(DateTime nowUtc)
    {
        if (_startAttempts < RadioTuning.MAX_START_ATTEMPTS) return RadioTickResult.Start(false);

        return Advance(nowUtc);
    }

    private RadioTickResult Advance(DateTime nowUtc)
    {
        int nextIndex = _activeIndex + 1;
        if (nextIndex >= _queue!.Count) return StopResult();

        SetActive(nextIndex, nowUtc);
        return RadioTickResult.Start(activeItemChanged: true);
    }

    private RadioTickResult StopResult()
    {
        Stop();
        return new RadioTickResult(RadioTickAction.Stop, false);
    }

    private void SetActive(int index, DateTime nowUtc)
    {
        _activeIndex = index;

        _lastObservedProgressMs = 0;
        _lastObservedDurationMs = 0;
        _lastObservedAtUtc = nowUtc;
        _lastObservedWasPlaying = false;

        _startIssuedAtUtc = nowUtc;
        _startAttempts = 0;
        _startConfirmed = false;
        _silenceSinceUtc = null;
    }

    private bool ActivePlayedThrough(DateTime nowUtc)
    {
        if (!_lastObservedWasPlaying) return false;

        int endMs = ActiveEndMs(_queue![_activeIndex]);
        if (endMs <= 0) return false;

        double sinceLastSampleMs = (nowUtc - _lastObservedAtUtc).TotalMilliseconds;
        if (sinceLastSampleMs < 0) return false;

        return _lastObservedProgressMs + sinceLastSampleMs >= endMs - RadioTuning.END_TOLERANCE_MS;
    }

    private int ActiveEndMs(IRadioQueueItem active)
    {
        if (active.IsPodcastSegment)
        {
            int end = active.PositionMs + RadioTuning.SEGMENT_LENGTH_MS;
            return _lastObservedDurationMs > 0 ? Math.Min(end, _lastObservedDurationMs) : end;
        }
        return _lastObservedDurationMs;
    }

    private int IndexInActiveSongRun(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return -1;
        if (_queue![_activeIndex].IsPodcastSegment) return -1;

        int runStart = _activeIndex;
        while (runStart > 0 && !_queue[runStart - 1].IsPodcastSegment) runStart--;

        int runEnd = _activeIndex;
        while (runEnd + 1 < _queue.Count && !_queue[runEnd + 1].IsPodcastSegment) runEnd++;

        for (int i = _activeIndex + 1; i <= runEnd; i++)
        {
            if (_queue[i].PlayUri == uri) return i;
        }
        for (int i = runStart; i < _activeIndex; i++)
        {
            if (_queue[i].PlayUri == uri) return i;
        }
        return -1;
    }
}
