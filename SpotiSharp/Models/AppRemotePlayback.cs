using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public static class AppRemotePlayback
{
    private static readonly object QueueLock = new object();

    private static List<string> _queuedUris = new List<string>();

    private static readonly List<string> _skipWhenReached = new List<string>();

    // Diagnostics only: everything queued into Spotify that hasn't been seen playing since, oldest
    // first. Play() doesn't clear Spotify's queue, so this is our best guess at its "Next in queue".
    private static readonly List<string> _notYetPlayed = new List<string>();
    private static string? _playedDirectlyUri;
    private static string? _lastNotedUri;

    public static async Task<bool> TryPlayAsync(string uri, IEnumerable<string>? queueAfter = null)
    {
        if (string.IsNullOrEmpty(uri) || PlaybackCommands.PlayUri == null) return false;

        if (PlaybackCommands.WakeSpotify != null && !await PlaybackCommands.WakeSpotify())
        {
            DiagnosticLog.Write($"[Queue] couldn't wake Spotify to play {RadioConductor.Label(uri)}");
            return false;
        }

        var toQueue = queueAfter?.Where(queuedUri => !string.IsNullOrEmpty(queuedUri)).ToList() ?? new List<string>();
        lock (QueueLock)
        {
            _playedDirectlyUri = uri;
            DiagnosticLog.Write(
                $"[Queue] playing {RadioConductor.Label(uri)} then queueing {toQueue.Count}; " +
                $"Spotify should still hold {_notYetPlayed.Count} from before: {Describe(_notYetPlayed)}");
        }

        if (!await PlaybackCommands.PlayUri(uri)) return false;

        lock (QueueLock)
        {
            _queuedUris = new List<string>(toQueue);
            _skipWhenReached.Clear();
        }

        if (PlaybackCommands.QueueUri != null && toQueue.Count > 0)
        {
            int queued = 0;
            foreach (var queuedUri in toQueue)
            {
                if (await PlaybackCommands.QueueUri(queuedUri))
                {
                    queued++;
                    NoteQueued(queuedUri);
                }
                else
                {
                    DiagnosticLog.Write($"[Queue] queueing {RadioConductor.Label(queuedUri)} failed or timed out");
                }
            }

            lock (QueueLock) DiagnosticLog.Write($"[Queue] queued {queued}/{toQueue.Count}; Spotify should now hold: {Describe(_notYetPlayed)}");
        }

        return true;
    }

    private static void NoteQueued(string uri)
    {
        lock (QueueLock) _notYetPlayed.Add(uri);
    }

    /// <summary>
    /// Called on every track change Spotify reports. Spotify's queue is first in, first out, so
    /// reaching a queued uri means everything queued ahead of it was played or skipped.
    /// </summary>
    public static void NoteNowPlaying(string? uri)
    {
        if (string.IsNullOrEmpty(uri) || uri == _lastNotedUri) return;
        _lastNotedUri = uri;

        lock (QueueLock)
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
                DiagnosticLog.Write($"[Queue] Spotify is on {RadioConductor.Label(uri)}, which SpotiSharp didn't queue or just play; " +
                                    $"still queued: {Describe(_notYetPlayed)}");
                return;
            }

            _notYetPlayed.RemoveRange(0, index + 1);
            DiagnosticLog.Write($"[Queue] Spotify is on {RadioConductor.Label(uri)} from our queue" +
                                (index > 0 ? $", past {index} queued ahead of it" : "") +
                                $"; still queued: {Describe(_notYetPlayed)}");
        }
    }

    public sealed record QueueView(List<string> NotYetPlayed, List<string> RadioMirror, List<string> ToSkip);

    public static QueueView SnapshotQueues()
    {
        lock (QueueLock)
        {
            return new QueueView(new List<string>(_notYetPlayed), new List<string>(_queuedUris), new List<string>(_skipWhenReached));
        }
    }

    public static void LogQueueMirror(string when)
    {
        lock (QueueLock)
        {
            DiagnosticLog.Write($"[Queue] at {when}: Spotify should still hold {Describe(_notYetPlayed)}; " +
                                $"radio mirror {Describe(_queuedUris)}; to skip {Describe(_skipWhenReached)}");
        }
    }

    private static string Describe(List<string> uris)
    {
        if (uris.Count == 0) return "[]";

        const int shown = 12;
        var listed = string.Join(", ", uris.Take(shown).Select(RadioConductor.Label));
        return uris.Count > shown ? $"[{listed}, ... {uris.Count - shown} more]" : $"[{listed}]";
    }

    /// <summary>Plays a radio item locally: a podcast segment from its rewound start, or a song followed by the rest of its run.</summary>
    public static async Task<bool> TryPlayItemAsync(RadioItem item, IReadOnlyList<string>? songRun)
    {
        if (item.IsPodcastSegment)
        {
            if (!await TryPlayAsync(item.PlayUri)) return false;

            var rewoundMs = Math.Max(0, item.PositionMs - RadioTuning.RESUME_REWIND_MS);
            if (rewoundMs > 0) PlaybackCommands.SeekTo?.Invoke(rewoundMs);

            return true;
        }

        if (songRun == null || songRun.Count == 0) return false;
        return await TryPlayAsync(songRun[0], songRun.Skip(1));
    }

    /// <summary>
    /// Brings Spotify's queue in line with the radio after the listener edits it. <paramref name="upcomingRun"/>
    /// is the songs that should follow <paramref name="currentUri"/> before the next podcast. Songs new to the
    /// run (a removed podcast joins two runs) are appended; queued songs no longer in it are marked to skip.
    /// </summary>
    public static async Task SyncQueueAsync(string currentUri, IReadOnlyList<string> upcomingRun)
    {
        List<string> toAppend;
        lock (QueueLock)
        {
            int currentIndex = _queuedUris.IndexOf(currentUri);
            var pending = _queuedUris.Skip(currentIndex + 1).ToList();

            foreach (var removed in pending.Where(uri => !upcomingRun.Contains(uri)))
            {
                if (_skipWhenReached.Contains(removed)) continue;
                _skipWhenReached.Add(removed);
                DiagnosticLog.Write($"[Queue] {removed} removed from the radio, will skip it when Spotify reaches it");
            }

            toAppend = upcomingRun.Where(uri => !pending.Contains(uri)).ToList();
            _queuedUris = pending.Concat(toAppend).ToList();
        }

        if (PlaybackCommands.QueueUri == null) return;
        foreach (var uri in toAppend)
        {
            DiagnosticLog.Write($"[Queue] appending {RadioConductor.Label(uri)} to match the radio");
            if (await PlaybackCommands.QueueUri(uri)) NoteQueued(uri);
            else DiagnosticLog.Write($"[Queue] appending {uri} failed or timed out");
        }
    }

    public enum RemovedSongCheck
    {
        None,
        SkipNow,
        SkipInFlight
    }

    private static readonly TimeSpan SkipLandingTimeout = TimeSpan.FromSeconds(5);

    private static string? _skippingUri;
    private static DateTime _skipSentAtUtc;

    /// <summary>
    /// Whether <paramref name="uri"/> is a queued song the listener removed from the radio. Returns
    /// <see cref="RemovedSongCheck.SkipNow"/> the first time it's seen, then <see cref="RemovedSongCheck.SkipInFlight"/>
    /// while it keeps playing until the skip lands (or times out).
    /// </summary>
    public static RemovedSongCheck CheckRemovedSong(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return RemovedSongCheck.None;

        lock (QueueLock)
        {
            if (uri == _skippingUri && DateTime.UtcNow - _skipSentAtUtc < SkipLandingTimeout) return RemovedSongCheck.SkipInFlight;
            _skippingUri = null;

            if (!_skipWhenReached.Remove(uri)) return RemovedSongCheck.None;

            _skippingUri = uri;
            _skipSentAtUtc = DateTime.UtcNow;
            return RemovedSongCheck.SkipNow;
        }
    }
}
