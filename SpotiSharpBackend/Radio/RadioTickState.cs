namespace SpotiSharpBackend.Radio;

public enum RadioTickAction
{
    None,
    StartActive,
    SkipUnexpected,
    Stop
}

public readonly record struct RadioTickResult(RadioTickAction Action, bool ActiveItemChanged)
{
    public static readonly RadioTickResult Nothing = new RadioTickResult(RadioTickAction.None, false);
    public static readonly RadioTickResult Moved = new RadioTickResult(RadioTickAction.None, true);

    public static RadioTickResult Start(bool activeItemChanged) => new RadioTickResult(RadioTickAction.StartActive, activeItemChanged);

    public static readonly RadioTickResult Skip = new RadioTickResult(RadioTickAction.SkipUnexpected, false);
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

    private string? _skippingUri;
    private DateTime _skipSentAtUtc;

    private readonly int _segmentLengthMs;

    public RadioTickState(IReadOnlyList<IRadioQueueItem> queue, int startIndex, DateTime nowUtc, bool alreadyIssued,
        int segmentLengthMs = RadioTuning.SEGMENT_LENGTH_MS)
    {
        _segmentLengthMs = segmentLengthMs;

        if (queue == null || startIndex < 0 || startIndex >= queue.Count) return;

        _queue = queue;
        SetActive(startIndex, nowUtc);
        if (alreadyIssued) _startAttempts = 1;
    }

    public bool IsActive => _queue != null;
    public int ActiveIndex => _activeIndex;
    public IRadioQueueItem? ActiveItem => _queue != null && _activeIndex >= 0 ? _queue[_activeIndex] : null;
    public string? SkippingUri => _skippingUri;

    /// <summary>Why the last advance, retry, skip or stop was decided, for the diagnostics log.</summary>
    public string? LastReason { get; private set; }

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

        int aheadIndex = IndexAheadInActiveSongRun(state.CurrentItemUri);
        if (aheadIndex >= 0) return MoveWithinRun(aheadIndex, state, nowUtc, $"Spotify moved ahead in the run to {state.CurrentItemUri}");

        if (!state.IsPlaying && state.ProgressMs == 0 && _lastObservedWasPlaying && IsEarlierInActiveSongRun(state.CurrentItemUri))
            return AdvancePastRun(nowUtc, $"Spotify is back on earlier run song {state.CurrentItemUri} paused at 0, so the run finished");

        if (!_startConfirmed)
        {
            if (nowUtc < _startIssuedAtUtc.AddMilliseconds(RadioTuning.START_GRACE_MS)) return RadioTickResult.Nothing;
            return RetryOrSkipActive(state, nowUtc);
        }

        bool somethingElsePlaying = !string.IsNullOrEmpty(state.CurrentItemUri);

        if (ActivePlayedThrough(nowUtc, boundProjection: somethingElsePlaying))
            return Advance(nowUtc, $"active item played through ({DescribeObservation(nowUtc)}), Spotify now on {state.CurrentItemUri ?? "nothing"}");

        if (somethingElsePlaying && IsInQueue(state.CurrentItemUri)) return SkipUnexpected(state, nowUtc);

        if (somethingElsePlaying)
            return StopResult($"{state.CurrentItemUri} isn't in the radio and the active item hadn't finished ({DescribeObservation(nowUtc)})");

        _silenceSinceUtc ??= nowUtc;
        if (nowUtc >= _silenceSinceUtc.Value.AddMilliseconds(RadioTuning.DEAD_AIR_TIMEOUT_MS))
            return StopResult($"nothing playing for {RadioTuning.DEAD_AIR_TIMEOUT_MS / 1000}s");

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

        if (++_unavailableSkips > RadioTuning.MAX_UNAVAILABLE_SKIPS)
            return StopResult($"{_unavailableSkips} items in a row were unavailable");

        return Advance(nowUtc, $"{ActiveItem?.PlayUri} is unavailable");
    }

    public RadioTickResult AdvanceManually(DateTime nowUtc)
    {
        if (_queue == null || _activeIndex < 0) return RadioTickResult.Nothing;

        _unavailableSkips = 0;
        return Advance(nowUtc, "Next pressed");
    }

    public void Resync(IReadOnlyList<IRadioQueueItem> queue, int activeIndex)
    {
        if (_queue == null || queue == null || activeIndex < 0 || activeIndex >= queue.Count) return;

        _queue = queue;
        _activeIndex = activeIndex;
    }

    /// <summary>
    /// Reports a stretch during which no usable playback snapshot arrived. Wall-clock time across
    /// such a gap is not evidence that anything was playing, so the timing baseline is reset and
    /// elapsed-time projection stays off until a fresh sample confirms playback again.
    /// </summary>
    public void NotifyObservationGap(DateTime nowUtc)
    {
        if (_queue == null || _activeIndex < 0) return;

        _lastObservedAtUtc = nowUtc;
        _lastObservedWasPlaying = false;
        _silenceSinceUtc = null;
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

        if (state.IsPlaying && _lastObservedWasPlaying && active.IsPodcastSegment)
        {
            int projectedEndMs = ActiveEndMs(active);
            if (projectedEndMs > 0)
            {            
                double projectedMs = _lastObservedProgressMs + (nowUtc - _lastObservedAtUtc).TotalMilliseconds;
                if (projectedMs >= projectedEndMs)
                    return Advance(nowUtc, $"segment end projected ({projectedMs:F0} of {projectedEndMs}ms)");
            }
        }

        if (state.IsPlaying && !active.IsPodcastSegment && state.ProgressMs < _lastObservedProgressMs
            && ActivePlayedThrough(nowUtc, boundProjection: true))
            return AdvancePastReplay(state, nowUtc);

        bool wasPlaying = _lastObservedWasPlaying;
        _lastObservedWasPlaying = state.IsPlaying;

        if (state.IsPlaying && (!wasPlaying || state.ProgressMs != _lastObservedProgressMs))
        {
            _lastObservedProgressMs = state.ProgressMs;
            _lastObservedAtUtc = nowUtc;
        }

        int endMs = ActiveEndMs(active);
        if (endMs <= 0) return RadioTickResult.Nothing;

        if (state.IsPlaying && active.IsPodcastSegment && state.ProgressMs >= endMs)
            return Advance(nowUtc, $"segment reached its end ({state.ProgressMs} of {endMs}ms)");

        if (!state.IsPlaying)
        {
            if (Math.Max(state.ProgressMs, _lastObservedProgressMs) >= endMs - RadioTuning.END_TOLERANCE_MS)
            {
                return Advance(nowUtc, $"paused at the end ({Math.Max(state.ProgressMs, _lastObservedProgressMs)} of {endMs}ms)");
            }

            if (wasPlaying && state.ProgressMs == 0 && _lastObservedProgressMs > 0)
                return Advance(nowUtc, $"paused at 0 after playing to {_lastObservedProgressMs}ms, so it finished");
        }

        return RadioTickResult.Nothing;
    }

    private RadioTickResult MoveWithinRun(int runIndex, PlaybackSnapshot state, DateTime nowUtc, string reason)
    {
        bool moved = runIndex != _activeIndex;
        if (moved) LastReason = reason;
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

    private RadioTickResult AdvancePastReplay(PlaybackSnapshot state, DateTime nowUtc)
    {
        int nextIndex = _activeIndex + 1;
        if (nextIndex < _queue!.Count && !_queue[nextIndex].IsPodcastSegment && _queue[nextIndex].PlayUri == state.CurrentItemUri)
            return MoveWithinRun(nextIndex, state, nowUtc, "song finished and Spotify is already on the next run song");

        return Advance(nowUtc, $"song jumped back to {state.ProgressMs}ms after playing through, so it finished");
    }

    private RadioTickResult SkipUnexpected(PlaybackSnapshot state, DateTime nowUtc)
    {
        if (!state.IsPlaying) return RadioTickResult.Nothing;

        if (state.CurrentItemUri == _skippingUri && nowUtc < _skipSentAtUtc.AddMilliseconds(RadioTuning.SKIP_LANDING_MS))
            return RadioTickResult.Nothing;

        _skippingUri = state.CurrentItemUri;
        _skipSentAtUtc = nowUtc;
        LastReason = $"{state.CurrentItemUri} is a radio item playing out of turn";
        return RadioTickResult.Skip;
    }

    private RadioTickResult RetryOrSkipActive(PlaybackSnapshot state, DateTime nowUtc)
    {
        if (_startAttempts < RadioTuning.MAX_START_ATTEMPTS)
        {
            LastReason = $"start not confirmed after {RadioTuning.START_GRACE_MS / 1000}s (Spotify on {state.CurrentItemUri ?? "nothing"}), " +
                         $"retrying (attempt {_startAttempts + 1} of {RadioTuning.MAX_START_ATTEMPTS})";
            return RadioTickResult.Start(false);
        }

        return Advance(nowUtc, $"gave up after {RadioTuning.MAX_START_ATTEMPTS} start attempts");
    }

    private RadioTickResult Advance(DateTime nowUtc, string reason)
    {
        int nextIndex = _activeIndex + 1;
        if (nextIndex >= _queue!.Count) return StopResult($"{reason}, and that was the end of the radio");

        LastReason = reason;
        SetActive(nextIndex, nowUtc);
        return RadioTickResult.Start(activeItemChanged: true);
    }

    private RadioTickResult AdvancePastRun(DateTime nowUtc, string reason)
    {
        int runEnd = _activeIndex;
        while (runEnd + 1 < _queue!.Count && !_queue[runEnd + 1].IsPodcastSegment) runEnd++;

        int nextIndex = runEnd + 1;
        if (nextIndex >= _queue.Count) return StopResult($"{reason}, and that was the end of the radio");

        LastReason = reason;
        SetActive(nextIndex, nowUtc);
        return RadioTickResult.Start(activeItemChanged: true);
    }

    private RadioTickResult StopResult(string reason)
    {
        LastReason = reason;
        Stop();
        return new RadioTickResult(RadioTickAction.Stop, false);
    }

    private string DescribeObservation(DateTime nowUtc) =>
        $"last seen {(_lastObservedWasPlaying ? "playing" : "not playing")} at {_lastObservedProgressMs} of {ActiveEndMs(_queue![_activeIndex])}ms, " +
        $"{(nowUtc - _lastObservedAtUtc).TotalSeconds:F0}s ago";

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

    public int? RemainingSegmentMs(DateTime nowUtc)
    {
        if (_queue == null || _activeIndex < 0) return null;

        var active = _queue[_activeIndex];
        if (!active.IsPodcastSegment) return null;

        int endMs = ActiveEndMs(active);
        if (endMs <= 0) return null;

        if (!_lastObservedWasPlaying) return endMs - _lastObservedProgressMs;

        double sinceLastSampleMs = Math.Max(0, (nowUtc - _lastObservedAtUtc).TotalMilliseconds);
        return (int)(endMs - (_lastObservedProgressMs + sinceLastSampleMs));
    }

    private bool ActivePlayedThrough(DateTime nowUtc, bool boundProjection)
    {
        if (!_lastObservedWasPlaying) return false;

        int endMs = ActiveEndMs(_queue![_activeIndex]);
        if (endMs <= 0) return false;

        double sinceLastSampleMs = (nowUtc - _lastObservedAtUtc).TotalMilliseconds;
        if (sinceLastSampleMs < 0) return false;

        // Crediting an unbounded stretch of wall clock as playback would let an hours-long pause
        // look like our item finishing, and the radio would seize playback back instead of bowing
        // out to what the listener chose. Only bounded when something else is already playing -
        // a silent gap says nothing was taken over, and sparse sampling there can run long.
        if (boundProjection && sinceLastSampleMs > RadioTuning.MAX_PLAYTHROUGH_PROJECTION_MS) return false;

        return _lastObservedProgressMs + sinceLastSampleMs >= endMs - RadioTuning.END_TOLERANCE_MS;
    }

    private int ActiveEndMs(IRadioQueueItem active)
    {
        if (active.IsPodcastSegment)
        {
            if (active.IsFinalPodcastSegment) return _lastObservedDurationMs;

            int end = active.PositionMs + _segmentLengthMs;
            return _lastObservedDurationMs > 0 ? Math.Min(end, _lastObservedDurationMs) : end;
        }
        return _lastObservedDurationMs;
    }

    private int IndexAheadInActiveSongRun(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return -1;
        if (_queue![_activeIndex].IsPodcastSegment) return -1;

        for (int i = _activeIndex + 1; i < _queue.Count && !_queue[i].IsPodcastSegment; i++)
        {
            if (_queue[i].PlayUri == uri) return i;
        }
        return -1;
    }

    private bool IsEarlierInActiveSongRun(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;
        if (_queue![_activeIndex].IsPodcastSegment) return false;

        for (int i = _activeIndex - 1; i >= 0 && !_queue[i].IsPodcastSegment; i--)
        {
            if (_queue[i].PlayUri == uri) return true;
        }
        return false;
    }

    private bool IsInQueue(string? uri) =>
        !string.IsNullOrEmpty(uri) && _queue!.Any(item => item.PlayUri == uri);
}
