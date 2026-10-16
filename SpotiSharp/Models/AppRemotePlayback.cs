using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

/// <summary>
/// Talks to Spotify through App Remote on behalf of the radio. What Spotify's queue holds and what
/// should go into it is <see cref="SpotifyQueueMirror"/>'s call -- this only carries the plan out and
/// reports back what landed.
/// </summary>
public static class AppRemotePlayback
{
    public static readonly SpotifyQueueMirror Queue = new SpotifyQueueMirror(RadioConductor.Label);

    public static async Task<bool> TryPlayAsync(string uri, IEnumerable<string>? queueAfter = null)
    {
        if (string.IsNullOrEmpty(uri) || PlaybackCommands.PlayUri == null) return false;

        if (PlaybackCommands.WakeSpotify != null && !await PlaybackCommands.WakeSpotify())
        {
            Queue.NoteWakeFailed(uri);
            return false;
        }

        var plan = Queue.PlanPlay(uri, queueAfter, canSkip: PlaybackCommands.SkipNext != null);

        if (plan.Start == QueueStart.SkipTo) PlaybackCommands.SkipNext!.Invoke();
        else if (!await PlaybackCommands.PlayUri(plan.PlayUri)) return false;

        Queue.NotePlayAccepted(plan);

        if (PlaybackCommands.QueueUri != null && plan.ToQueue.Count > 0)
        {
            int queued = 0;
            foreach (var queuedUri in plan.ToQueue)
            {
                if (await PlaybackCommands.QueueUri(queuedUri))
                {
                    queued++;
                    Queue.NoteQueued(queuedUri);
                }
                else
                {
                    Queue.NoteQueueFailed(queuedUri);
                }
            }

            Queue.NoteQueueRoundDone(queued, plan.ToQueue.Count);
        }

        return true;
    }

    public static void NoteNowPlaying(string? uri) => Queue.NoteNowPlaying(uri);

    public static QueueView SnapshotQueues() => Queue.Snapshot();

    public static void LogQueueMirror(string when) => Queue.LogQueueMirror(when);

    public static RemovedSongCheck CheckRemovedSong(string? uri) => Queue.CheckRemovedSong(uri);

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

    /// <summary>Brings Spotify's queue in line with the radio after the listener edits it.</summary>
    public static async Task SyncQueueAsync(string currentUri, IReadOnlyList<string> upcomingRun)
    {
        var plan = Queue.PlanSync(currentUri, upcomingRun);

        if (PlaybackCommands.QueueUri == null) return;
        foreach (var uri in plan.ToAppend)
        {
            Queue.NoteAppending(uri);
            if (await PlaybackCommands.QueueUri(uri)) Queue.NoteQueued(uri);
            else Queue.NoteAppendFailed(uri);
        }
    }
}
