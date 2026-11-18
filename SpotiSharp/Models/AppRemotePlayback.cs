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

        await SkipAheadAsync(plan.PreSkipCount);

        if (plan.Start == QueueStart.Play)
        {
            if (!await PlaybackCommands.PlayUri(plan.PlayUri)) return false;
        }
        else
        {
            // Play(uri) above covers resuming for QueueStart.Play - nothing else does for SkipTo,
            // and SkipAheadAsync just paused Spotify (so none of the songs it walked past were
            // actually heard) without anything to un-pause it once it lands on the target.
            PlaybackCommands.Resume?.Invoke();
        }

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

    /// <summary>
    /// Sends <paramref name="count"/> real skip-forwards before the rest of <see cref="TryPlayAsync"/>
    /// runs - see <see cref="SpotifyQueueMirror.PlanPlay"/> for what this walks past or flushes.
    /// App Remote has no clear-queue call, so this is the only way to empty Spotify's real queue;
    /// each skip is spaced out so Spotify doesn't drop one fired too soon after the last.
    /// </summary>
    private static async Task SkipAheadAsync(int count)
    {
        if (count <= 0 || PlaybackCommands.SkipNext == null) return;

        // Skipping while playing means each skip briefly starts the next queued song audibly before
        // the following skip cuts it off - a run of these is exactly what made clearing a few stray
        // songs (or the "unknown queue" 50-item flush) sound like a string of song snippets.
        // Spotify's own skip-next keeps whatever paused/playing state it was already in, so pausing
        // first makes every skip in this loop silent; the caller is responsible for resuming
        // afterward if it actually wants something playing once the flush lands (see the two call
        // sites: TryPlayAsync resumes or plays outright, ClearQueueAsync deliberately doesn't).
        PlaybackCommands.Pause?.Invoke();

        int capped = Math.Min(count, RadioTuning.MAX_QUEUE_FLUSH_SKIPS);

        // When the caller actually knows this many real items are queued (SkipTo, or a Play with a
        // provably-tracked count), every one of them is expected to land, so send the full count
        // regardless of an odd failure. But the "unknown queue" fallback (a fresh reconnect/restart,
        // nothing tracked yet) picks this count blind, hoping a few of Spotify's real stray leftovers
        // are in there somewhere - Spotify answering "Cannot skip song" means there's nothing left to
        // skip past, so sending the rest of a 50-skip flush after that point is both pointless and
        // what was turning one tap into a 15-second stall (and risking an extra skip landing after
        // the eventual play, overshooting past the song that was actually asked for).
        if (PlaybackCommands.SkipNextAsync != null)
        {
            for (int i = 0; i < capped; i++)
            {
                bool landed = await PlaybackCommands.SkipNextAsync();
                if (!landed)
                {
                    DiagnosticLog.Write($"[Queue] skip-ahead stopped after {i}/{capped}: Spotify has nothing left to skip past");
                    return;
                }
                await Task.Delay(RadioTuning.QUEUE_FLUSH_SKIP_DELAY_MS);
            }
            return;
        }

        for (int i = 0; i < capped; i++)
        {
            PlaybackCommands.SkipNext();
            await Task.Delay(RadioTuning.QUEUE_FLUSH_SKIP_DELAY_MS);
        }
    }

    /// <summary>Empties Spotify's real queue of whatever this mirror still thinks is sitting there -
    /// see <see cref="SpotifyQueueMirror.PlanClear"/>. A no-op (0 skips) once it's already empty, so
    /// calling this on every AB episode start/resume is harmless.</summary>
    public static async Task ClearQueueAsync()
    {
        int count = Queue.PlanClear();
        await SkipAheadAsync(count);
    }

    public static void LogQueueMirror(string when) => Queue.LogQueueMirror(when);

    public static RemovedSongCheck CheckRemovedSong(string? uri) => Queue.CheckRemovedSong(uri);

    /// <summary>Plays a radio item locally: a podcast segment from its rewound start, or a song followed by the rest of its run.
    /// Takes <see cref="IRadioQueueItem"/> rather than the concrete <see cref="RadioItem"/> so the AudioBookshelf-backed
    /// radio's own queue item type can share this same Spotify playback plumbing for its music half.</summary>
    public static async Task<bool> TryPlayItemAsync(IRadioQueueItem item, IReadOnlyList<string>? songRun)
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
