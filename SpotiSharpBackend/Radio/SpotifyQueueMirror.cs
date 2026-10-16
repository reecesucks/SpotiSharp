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
/// What to send Spotify to start a radio item. <paramref name="Run"/> is the whole run that should
/// sit behind <paramref name="PlayUri"/>; <paramref name="ToQueue"/> is only the part of it Spotify
/// isn't already holding, which is all that may be sent.
/// </summary>
public sealed record QueuePlan(QueueStart Start, string PlayUri, IReadOnlyList<string> Run, IReadOnlyList<string> ToQueue);

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
    private readonly List<string> _notYetPlayed = new List<string>();
    private string? _playedDirectlyUri;
    private string? _lastNotedUri;

    private string? _skippingUri;
    private DateTime _skipSentAtUtc;

    public SpotifyQueueMirror(Func<string?, string>? label = null)
    {
        _label = label ?? (uri => uri ?? "nothing");
    }

    /// <summary>
    /// The plan for starting <paramref name="uri"/> with <paramref name="queueAfter"/> behind it.
    /// Call <see cref="NotePlayAccepted"/> once Spotify has taken it, then <see cref="NoteQueued"/>
    /// for each uri that lands.
    ///
    /// Spotify's queue is append-only for us, so what it already holds decides both how to reach
    /// <paramref name="uri"/> and what's left to queue. Sending the whole run every time -- while
    /// play() leaves the previous copies in place -- is what used to leave the run duplicated behind
    /// a song and the song itself sitting at the head of the queue, ready to restart when it ended.
    /// </summary>
    /// <param name="canSkip">Whether the caller can send a skip-forward; without one everything is a play.</param>
    public QueuePlan PlanPlay(string uri, IEnumerable<string>? queueAfter, bool canSkip = true)
    {
        var run = queueAfter?.Where(queuedUri => !string.IsNullOrEmpty(queuedUri)).ToList() ?? new List<string>();

        lock (_queueLock)
        {
            // Already the next thing up: skipping forward reaches it and consumes the queue entry,
            // where playing it would leave that entry to play the song a second time.
            bool atHead = canSkip && _notYetPlayed.Count > 0 && _notYetPlayed[0] == uri;
            var start = atHead ? QueueStart.SkipTo : QueueStart.Play;

            // What Spotify still holds once it's on `uri`, and the part of the run that isn't in
            // there already. Anything else it holds is left alone: it plays out of turn later and
            // the radio deals with it then, but nothing is gained by queueing a second copy now.
            var behind = atHead ? _notYetPlayed.Skip(1).ToList() : new List<string>(_notYetPlayed);

            int matched = 0;
            foreach (var queued in behind)
            {
                if (matched < run.Count && queued == run[matched]) matched++;
            }
            var toQueue = run.Skip(matched).ToList();

            // Only a play puts Spotify on something we never queued; a skip lands on the queue
            // entry itself, which NoteNowPlaying should account for as usual.
            _playedDirectlyUri = atHead ? null : uri;

            var strays = behind.Count - matched;
            DiagnosticLog.Write(
                $"[Queue] {(atHead ? "skipping to" : "playing")} {_label(uri)} then queueing {toQueue.Count} of " +
                $"{run.Count}; Spotify should still hold {_notYetPlayed.Count} from before" +
                $"{(strays > 0 ? $" ({strays} not in the run)" : "")}: {Describe(_notYetPlayed)}");

            return new QueuePlan(start, uri, run, toQueue);
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
