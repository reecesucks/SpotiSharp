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
        public DateTime QueuedAtUtc { get; set; }
    }

    private static Dictionary<string, PendingSave>? _queue;
    private static bool _flushInProgress;

    /// <summary>Flushes anything queued, then saves this position - queuing it in turn if that fails too.</summary>
    public static async Task SaveOrQueueAsync(string libraryItemId, string episodeId, int positionMs, int durationMs)
    {
        await FlushPendingAsync();

        var succeeded = await AudiobookshelfApiClient.SaveProgressAsync(libraryItemId, episodeId, positionMs, durationMs);
        if (succeeded)
        {
            DiagnosticLog.Write($"[Audiobookshelf] progress saved: episode {episodeId} at {positionMs}ms");
            return;
        }

        Enqueue(libraryItemId, episodeId, positionMs, durationMs);
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

            foreach (var pending in queue.Values.OrderBy(p => p.QueuedAtUtc).ToList())
            {
                var succeeded = await AudiobookshelfApiClient.SaveProgressAsync(pending.LibraryItemId, pending.EpisodeId, pending.PositionMs, pending.DurationMs);
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

    private static void Enqueue(string libraryItemId, string episodeId, int positionMs, int durationMs)
    {
        var queue = GetQueue();
        queue[episodeId] = new PendingSave
        {
            LibraryItemId = libraryItemId,
            EpisodeId = episodeId,
            PositionMs = positionMs,
            DurationMs = durationMs,
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
