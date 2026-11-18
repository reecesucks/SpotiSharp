using SpotiSharp.Helpers;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;

/// <summary>Saves AudioBookshelf playback progress, queuing to disk instead of losing it when the
/// save fails (e.g. offline) and flushing the queue the next time a save succeeds. Only the most
/// recent position per episode is kept queued - an older queued position is pointless once a newer
/// one exists for the same episode.</summary>
public static class AudiobookshelfProgressSyncModel
{
    private const string QUEUE_CACHE_KEY = "audiobookshelf_pending_progress";

    private class PendingSave
    {
        public string LibraryItemId { get; set; } = string.Empty;
        public string EpisodeId { get; set; } = string.Empty;
        public int PositionMs { get; set; }
        public int DurationMs { get; set; }
        public bool IsFinished { get; set; }
        public DateTime QueuedAtUtc { get; set; }
    }

    private static Dictionary<string, PendingSave>? _queue;
    private static bool _flushInProgress;

    // A queued save this old is almost certainly stale - genuine offline stretches are minutes or
    // hours, not days, and after that long the listener has very likely moved on (finished the
    // episode some other way, like the Radio page's "Mark as played", or just kept listening past
    // where this position was saved). Replaying it then would silently overwrite whatever's
    // actually current on the server - exactly what happened to a save queued back when progress
    // saves for Radio-driven playback were failing outright, replayed days later once that was fixed.
    private static readonly TimeSpan MaxPendingAge = TimeSpan.FromHours(24);

    /// <summary>Flushes anything queued, then saves this position - queuing it in turn if that fails too.</summary>
    public static async Task SaveOrQueueAsync(string libraryItemId, string episodeId, int positionMs, int durationMs, bool isFinished = false)
    {
        await FlushPendingAsync();

        var succeeded = await AudiobookshelfApiClient.SaveProgressAsync(libraryItemId, episodeId, positionMs, durationMs, isFinished);
        if (succeeded)
        {
            DiagnosticLog.Write($"[Audiobookshelf] progress saved: episode {episodeId} at {positionMs}ms{(isFinished ? " (finished)" : "")}");

            // This save went straight through, bypassing the queue entirely - but a stale entry for
            // the same episode queued from an earlier failure (e.g. back when progress saves for
            // Radio-driven playback were silently failing outright, before that bug was fixed) would
            // otherwise just sit there forever, waiting for some unrelated future flush to blindly
            // replay it and stomp this (newer, possibly "finished") position with old data. Nothing
            // else ever clears a queued entry except a flush that successfully resends that exact
            // one, so a save that skips the queue has to clear it itself.
            Dequeue(episodeId);
            return;
        }

        Enqueue(libraryItemId, episodeId, positionMs, durationMs, isFinished);
    }

    private static void Dequeue(string episodeId)
    {
        var queue = GetQueue();
        if (queue.Remove(episodeId)) SaveQueue(queue);
    }

    /// <summary>Attempts to send every queued save, in the order queued, stopping at the first one that
    /// still fails (most likely still offline) and leaving the rest queued for next time. Safe to call
    /// often - a no-op when the queue is empty.</summary>
    public static async Task FlushPendingAsync()
    {
        if (_flushInProgress) return;

        var queue = GetQueue();
        if (queue.Count == 0) return;

        _flushInProgress = true;
        try
        {
            DiagnosticLog.Write($"[Audiobookshelf] attempting to flush {queue.Count} queued progress save(s)");

            var now = DateTime.UtcNow;
            foreach (var pending in queue.Values.OrderBy(p => p.QueuedAtUtc).ToList())
            {
                if (now - pending.QueuedAtUtc > MaxPendingAge)
                {
                    DiagnosticLog.Write($"[Audiobookshelf] dropping stale queued progress for episode {pending.EpisodeId} (queued at {pending.QueuedAtUtc:O}), not replaying it over whatever's current now");
                    queue.Remove(pending.EpisodeId);
                    SaveQueue(queue);
                    continue;
                }

                var succeeded = await AudiobookshelfApiClient.SaveProgressAsync(pending.LibraryItemId, pending.EpisodeId, pending.PositionMs, pending.DurationMs, pending.IsFinished);
                if (!succeeded)
                {
                    DiagnosticLog.Write($"[Audiobookshelf] flush stopped - still failing to reach server ({queue.Count} save(s) remain queued)");
                    return;
                }

                queue.Remove(pending.EpisodeId);
                SaveQueue(queue);
                DiagnosticLog.Write($"[Audiobookshelf] flushed queued progress for episode {pending.EpisodeId} (queued at {pending.QueuedAtUtc:O})");
            }

            DiagnosticLog.Write("[Audiobookshelf] progress queue fully flushed");
        }
        finally
        {
            _flushInProgress = false;
        }
    }

    private static void Enqueue(string libraryItemId, string episodeId, int positionMs, int durationMs, bool isFinished)
    {
        var queue = GetQueue();
        queue[episodeId] = new PendingSave
        {
            LibraryItemId = libraryItemId,
            EpisodeId = episodeId,
            PositionMs = positionMs,
            DurationMs = durationMs,
            IsFinished = isFinished,
            QueuedAtUtc = DateTime.UtcNow
        };
        SaveQueue(queue);
        DiagnosticLog.Write($"[Audiobookshelf] progress save queued offline: episode {episodeId} at {positionMs}ms (queue size now {queue.Count})");
    }

    private static Dictionary<string, PendingSave> GetQueue()
    {
        return _queue ??= DiskCacheHelper.Load<Dictionary<string, PendingSave>>(QUEUE_CACHE_KEY) ?? new Dictionary<string, PendingSave>();
    }

    private static void SaveQueue(Dictionary<string, PendingSave> queue)
    {
        _queue = queue;
        DiskCacheHelper.Save(QUEUE_CACHE_KEY, queue);
    }
}
