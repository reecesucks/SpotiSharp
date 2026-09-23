using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public static class AppRemotePlayback
{
    private static readonly object QueueLock = new object();

    private static List<string> _queuedUris = new List<string>();

    private static readonly List<string> _skipWhenReached = new List<string>();

    public static async Task<bool> TryPlayAsync(string uri, IEnumerable<string>? queueAfter = null)
    {
        if (string.IsNullOrEmpty(uri) || PlaybackCommands.PlayUri == null) return false;

        if (PlaybackCommands.WakeSpotify != null && !await PlaybackCommands.WakeSpotify()) return false;

        if (!await PlaybackCommands.PlayUri(uri)) return false;

        var toQueue = queueAfter?.Where(queuedUri => !string.IsNullOrEmpty(queuedUri)).ToList() ?? new List<string>();
        lock (QueueLock)
        {
            _queuedUris = new List<string>(toQueue);
            _skipWhenReached.Clear();
        }

        if (PlaybackCommands.QueueUri != null)
        {
            foreach (var queuedUri in toQueue) await PlaybackCommands.QueueUri(queuedUri);
        }

        return true;
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
            DiagnosticLog.Write($"[Queue] appending {uri} to match the radio");
            await PlaybackCommands.QueueUri(uri);
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
