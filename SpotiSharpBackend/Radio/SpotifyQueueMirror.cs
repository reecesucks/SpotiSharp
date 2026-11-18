namespace SpotiSharpBackend.Radio;

/// <summary>How to get Spotify onto the song the radio wants next.</summary>
public enum QueueStart
{
    /// <summary>Play it outright: Spotify's queue isn't about to reach it.</summary>
    Play,

    /// <summary>It's already the next thing in Spotify's queue, so skipping forward reaches it.</summary>
    SkipTo
}

/// <summary>
/// What to send Spotify to start a radio item. <paramref name="PreSkipCount"/> real skip-forwards
/// must be sent first - for <see cref="QueueStart.SkipTo"/> that's what walks past anything stray
/// ahead of <paramref name="PlayUri"/> and lands on it (no separate play follows); for
/// <see cref="QueueStart.Play"/> it's what empties Spotify's queue before <paramref name="PlayUri"/>
/// is played outright. <paramref name="Run"/> is the whole run that should sit behind
/// <paramref name="PlayUri"/>; <paramref name="ToQueue"/> is only the part of it Spotify isn't
/// already holding, which is all that may be sent.
/// </summary>
public sealed record QueuePlan(QueueStart Start, string PlayUri, IReadOnlyList<string> Run, IReadOnlyList<string> ToQueue, int PreSkipCount);

/// <summary>Which queued songs the listener removed, and which songs the radio gained, after a radio edit.</summary>
public sealed record SyncPlan(IReadOnlyList<string> ToAppend);

public sealed record QueueView(List<string> NotYetPlayed, List<string> RadioMirror, List<string> ToSkip);

public enum RemovedSongCheck
{
    None,
    SkipNow,
    SkipInFlight
}

/// <summary>
/// Our model of Spotify's queue, and the decisions that depend on it: what to play and queue when
/// the radio starts an item, what to append or skip after a radio edit, and how far Spotify has got
/// through what we queued. Spotify's queue is append-only for us -- App Remote's play doesn't clear
/// it -- so this is the only record of what it still holds.
///
/// Decisions and state only: the caller does the talking to Spotify and reports back what landed.
/// </summary>
public sealed class SpotifyQueueMirror
{
    private readonly object _queueLock = new object();

    private readonly Func<string?, string> _label;

    private List<string> _queuedUris = new List<string>();

    private readonly List<string> _skipWhenReached = new List<string>();

    // Everything queued into Spotify that hasn't been seen playing since, oldest first -- our best
    // guess at its "Next in queue".
    private List<string> _notYetPlayed = new List<string>();
    private string? _playedDirectlyUri;
    private string? _lastNotedUri;

    private string? _skippingUri;
    private DateTime _skipSentAtUtc;

    // App Remote gives no way to read Spotify's real queue, so right after this mirror is created
    // (app start, or a fresh reconnect) it has no idea what's actually sitting there - a prior
    // session's leftovers, or anything queued from Spotify's own UI, are invisible until something
    // is done about them. The very first play of a session can't trust an empty _notYetPlayed to mean
    // an empty real queue, so it flushes a generous amount regardless; after that, this mirror is the
    // only thing adding to Spotify's queue, so its own count is trusted again.
    private bool _everFlushed;

    public SpotifyQueueMirror(Func<string?, string>? label = null)
    {
        _label = label ?? (uri => uri ?? "nothing");
    }

    /// <summary>
    /// The plan for starting <paramref name="uri"/> with <paramref name="queueAfter"/> behind it.
    /// Call <see cref="NotePlayAccepted"/> once Spotify has taken it, then <see cref="NoteQueued"/>
    /// for each uri that lands.
    ///
    /// Spotify's queue is append-only for us and App Remote has no way to clear it, so the only real
    /// flush is a skip-forward per entry - and a skip can only ever land on the very next thing
    /// queued, never clear what's behind it without abandoning what it just landed on. So a stray is
    /// only ever left alone when what's behind the target is an exact, provable match for the run
    /// about to be queued; any mismatch, anywhere, means nothing is trusted and everything tracked is
    /// flushed before playing outright. No stray ever gets to just sit there - that tolerance was
    /// exactly how the queue clogged up over a session.
    /// </summary>
    /// <param name="canSkip">Whether the caller can send a skip-forward; without one everything is a play, with nothing pre-flushed.</param>
    public QueuePlan PlanPlay(string uri, IEnumerable<string>? queueAfter, bool canSkip = true)
    {
        var run = queueAfter?.Where(queuedUri => !string.IsNullOrEmpty(queuedUri)).ToList() ?? new List<string>();

        lock (_queueLock)
        {
            int index = canSkip ? _notYetPlayed.IndexOf(uri) : -1;
            if (index >= 0)
            {
                var behind = _notYetPlayed.Skip(index + 1).ToList();
                bool cleanMatch = behind.Count <= run.Count && behind.SequenceEqual(run.Take(behind.Count));

                if (cleanMatch)
                {
                    var toQueue = run.Skip(behind.Count).ToList();
                    int preSkipCount = index + 1;

                    _playedDirectlyUri = uri;
                    _notYetPlayed = behind;
                    _skipWhenReached.Clear();
                    _skippingUri = null;
                    _everFlushed = true;

                    DiagnosticLog.Write($"[Queue] skipping past {preSkipCount} to {_label(uri)}, then queueing {toQueue.Count} of {run.Count}");
                    return new QueuePlan(QueueStart.SkipTo, uri, run, toQueue, preSkipCount);
                }
            }

            // Not found, or found but what's behind it isn't a provable match for the run - flush
            // everything tracked (the full cap instead, if nothing's tracked and this mirror has never
            // proven a clean baseline - an empty _notYetPlayed right after app start doesn't mean an
            // empty real queue, just that nothing's been seen yet) and queue the whole run fresh.
            bool knowsSomething = _notYetPlayed.Count > 0 || _everFlushed;
            int flushCount = knowsSomething ? _notYetPlayed.Count : RadioTuning.MAX_QUEUE_FLUSH_SKIPS;

            _playedDirectlyUri = uri;
            _notYetPlayed = new List<string>();
            _skipWhenReached.Clear();
            _skippingUri = null;
            _everFlushed = true;

            DiagnosticLog.Write($"[Queue] flushing {flushCount} then playing {_label(uri)}, then queueing all {run.Count} of the run fresh");
            return new QueuePlan(QueueStart.Play, uri, run, run, flushCount);
        }
    }

    /// <summary>Spotify's queue should sit empty while something else (an AudioBookshelf episode) owns
    /// playback instead of it - there's nothing for Spotify to play next regardless, and leaving
    /// whatever was tracked sitting there unflushed is exactly the kind of stray leftover that goes
    /// stale over a long episode. Returns how many real skip-forwards the caller needs to send to
    /// actually empty it; the caller is expected to have paused Spotify first, so this never races a
    /// still-playing track landing on something before the skips land.</summary>
    public int PlanClear()
    {
        lock (_queueLock)
        {
            int count = _notYetPlayed.Count;
            _notYetPlayed = new List<string>();
            _skipWhenReached.Clear();
            _skippingUri = null;
            _everFlushed = true;
            return count;
        }
    }

    /// <summary>Spotify took the play or the skip, so the run behind it is now the radio's run.</summary>
    public void NotePlayAccepted(QueuePlan plan)
    {
        lock (_queueLock)
        {
            _queuedUris = new List<string>(plan.Run);
            _skipWhenReached.Clear();
        }
    }

    public void NoteQueued(string uri)
    {
        lock (_queueLock) _notYetPlayed.Add(uri);
    }

    public void NoteQueueFailed(string uri)
    {
        DiagnosticLog.Write($"[Queue] queueing {_label(uri)} failed or timed out");
    }

    public void NoteQueueRoundDone(int queued, int total)
    {
        lock (_queueLock) DiagnosticLog.Write($"[Queue] queued {queued}/{total}; Spotify should now hold: {Describe(_notYetPlayed)}");
    }

    public void NoteWakeFailed(string uri)
    {
        DiagnosticLog.Write($"[Queue] couldn't wake Spotify to play {_label(uri)}");
    }

    /// <summary>
    /// Called on every track change Spotify reports. Spotify's queue is first in, first out, so
    /// reaching a queued uri means everything queued ahead of it was played or skipped.
    /// </summary>
    public void NoteNowPlaying(string? uri)
    {
        if (string.IsNullOrEmpty(uri) || uri == _lastNotedUri) return;
        _lastNotedUri = uri;

        lock (_queueLock)
        {
            if (uri == _playedDirectlyUri)
            {
                _playedDirectlyUri = null;
                return;
            }
            _playedDirectlyUri = null;

            int index = _notYetPlayed.IndexOf(uri);
            if (index < 0)
            {
                DiagnosticLog.Write($"[Queue] Spotify is on {_label(uri)}, which SpotiSharp didn't queue or just play; " +
                                    $"still queued: {Describe(_notYetPlayed)}");
                return;
            }

            _notYetPlayed.RemoveRange(0, index + 1);
            DiagnosticLog.Write($"[Queue] Spotify is on {_label(uri)} from our queue" +
                                (index > 0 ? $", past {index} queued ahead of it" : "") +
                                $"; still queued: {Describe(_notYetPlayed)}");
        }
    }

    /// <summary>
    /// The plan for bringing Spotify's queue in line with the radio after the listener edits it.
    /// <paramref name="upcomingRun"/> is the songs that should follow <paramref name="currentUri"/>
    /// before the next podcast. Songs new to the run (a removed podcast joins two runs) are appended;
    /// queued songs no longer in it are marked to skip when Spotify reaches them.
    /// </summary>
    public SyncPlan PlanSync(string currentUri, IReadOnlyList<string> upcomingRun)
    {
        lock (_queueLock)
        {
            int currentIndex = _queuedUris.IndexOf(currentUri);
            var pending = _queuedUris.Skip(currentIndex + 1).ToList();

            foreach (var removed in pending.Where(uri => !upcomingRun.Contains(uri)))
            {
                if (_skipWhenReached.Contains(removed)) continue;
                _skipWhenReached.Add(removed);
                DiagnosticLog.Write($"[Queue] {removed} removed from the radio, will skip it when Spotify reaches it");
            }

            var toAppend = upcomingRun.Where(uri => !pending.Contains(uri)).ToList();
            _queuedUris = pending.Concat(toAppend).ToList();

            return new SyncPlan(toAppend);
        }
    }

    public void NoteAppending(string uri)
    {
        DiagnosticLog.Write($"[Queue] appending {_label(uri)} to match the radio");
    }

    public void NoteAppendFailed(string uri)
    {
        DiagnosticLog.Write($"[Queue] appending {uri} failed or timed out");
    }

    private static readonly TimeSpan SkipLandingTimeout = TimeSpan.FromMilliseconds(RadioTuning.SKIP_LANDING_MS);

    /// <summary>
    /// Whether <paramref name="uri"/> is a queued song the listener removed from the radio. Returns
    /// <see cref="RemovedSongCheck.SkipNow"/> the first time it's seen, then <see cref="RemovedSongCheck.SkipInFlight"/>
    /// while it keeps playing until the skip lands (or times out).
    /// </summary>
    public RemovedSongCheck CheckRemovedSong(string? uri) => CheckRemovedSong(uri, DateTime.UtcNow);

    public RemovedSongCheck CheckRemovedSong(string? uri, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(uri)) return RemovedSongCheck.None;

        lock (_queueLock)
        {
            if (uri == _skippingUri && nowUtc - _skipSentAtUtc < SkipLandingTimeout) return RemovedSongCheck.SkipInFlight;
            _skippingUri = null;

            if (!_skipWhenReached.Remove(uri)) return RemovedSongCheck.None;

            _skippingUri = uri;
            _skipSentAtUtc = nowUtc;
            return RemovedSongCheck.SkipNow;
        }
    }

    public QueueView Snapshot()
    {
        lock (_queueLock)
        {
            return new QueueView(new List<string>(_notYetPlayed), new List<string>(_queuedUris), new List<string>(_skipWhenReached));
        }
    }

    public void LogQueueMirror(string when)
    {
        lock (_queueLock)
        {
            DiagnosticLog.Write($"[Queue] at {when}: Spotify should still hold {Describe(_notYetPlayed)}; " +
                                $"radio mirror {Describe(_queuedUris)}; to skip {Describe(_skipWhenReached)}");
        }
    }

    private string Describe(List<string> uris)
    {
        if (uris.Count == 0) return "[]";

        const int shown = 12;
        var listed = string.Join(", ", uris.Take(shown).Select(_label));
        return uris.Count > shown ? $"[{listed}, ... {uris.Count - shown} more]" : $"[{listed}]";
    }
}
