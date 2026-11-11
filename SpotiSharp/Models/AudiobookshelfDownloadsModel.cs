using SpotiSharp.Helpers;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;

/// <summary>Downloads an episode's audio file to the app's own storage for offline playback, and tracks
/// which episodes are downloaded. The index (episode id -> local file path + chapter markers) is the
/// only persisted state; the files themselves live under <see cref="DownloadDirectory"/> and are named
/// by episode id. Chapters are cached here (not just in the episode-list cache) so they survive being
/// offline - Radio will read them from here once it uses them to pause for ad breaks.</summary>
public static class AudiobookshelfDownloadsModel
{
    private const string INDEX_CACHE_KEY = "audiobookshelf_downloads_index";

    private static readonly string DownloadDirectory = Path.Combine(FileSystem.AppDataDirectory, "audiobookshelf-downloads");

    private class DownloadRecord
    {
        public string LocalPath { get; set; } = string.Empty;
        public List<AudiobookshelfChapter> Chapters { get; set; } = new();
    }

    private static Dictionary<string, DownloadRecord>? _index;

    // Tracked here, not just as a flag on the AudiobookshelfEpisode instance that started the download:
    // the podcasts page builds a brand new episode list (new objects) every time you navigate back to
    // it, so an instance-only flag would make an in-progress download silently stop showing as
    // "downloading" the moment you left and returned to the page, even though it's still running.
    private static readonly HashSet<string> DownloadingEpisodeIds = new();

    // Same reasoning as DownloadingEpisodeIds: a rebuilt page's fresh episode object needs to pick up
    // where an in-progress download actually is, not just that one is happening.
    private static readonly Dictionary<string, double> DownloadProgressByEpisodeId = new();

    public static bool IsDownloaded(string episodeId) => GetLocalPath(episodeId) != null;

    public static bool IsDownloadInProgress(string episodeId)
    {
        lock (DownloadingEpisodeIds) return DownloadingEpisodeIds.Contains(episodeId);
    }

    /// <summary>Fraction downloaded so far (0-1), or -1 if not currently downloading or the total size isn't known yet.</summary>
    public static double GetDownloadProgress(string episodeId)
    {
        lock (DownloadProgressByEpisodeId) return DownloadProgressByEpisodeId.GetValueOrDefault(episodeId, -1);
    }

    public static string? GetLocalPath(string episodeId)
    {
        var index = GetIndex();
        if (!index.TryGetValue(episodeId, out var record)) return null;
        if (File.Exists(record.LocalPath)) return record.LocalPath;

        // the file is gone (cleared storage, manual deletion) - the index entry is stale, drop it
        index.Remove(episodeId);
        SaveIndex(index);
        return null;
    }

    public static IReadOnlyList<AudiobookshelfChapter> GetChapters(string episodeId)
    {
        var index = GetIndex();
        return index.TryGetValue(episodeId, out var record) ? record.Chapters : Array.Empty<AudiobookshelfChapter>();
    }

    public static async Task DownloadAsync(AudiobookshelfEpisode episode, CancellationToken cancellationToken = default)
    {
        if (episode.IsDownloaded) return;

        lock (DownloadingEpisodeIds)
        {
            // the authoritative guard against two callers (e.g. the same episode shown on an old and a
            // freshly rebuilt page) racing to write the same partial file - not just episode.IsDownloading
            if (!DownloadingEpisodeIds.Add(episode.EpisodeId)) return;

            // First download of the batch: keep the process alive for the duration - without this,
            // a plain background Task gets killed along with the whole app once it's backgrounded for
            // a while, silently abandoning the download (the partial file survives and resumes correctly
            // next launch, but nothing visible happens in the meantime).
            if (DownloadingEpisodeIds.Count == 1) AudiobookshelfDownloadBackgroundService.Start();
        }

        episode.IsDownloading = true;
        episode.DownloadProgress = -1;
        SetProgress(episode.EpisodeId, -1);
        try
        {
            Directory.CreateDirectory(DownloadDirectory);
            var partialPath = Path.Combine(DownloadDirectory, $"{episode.EpisodeId}.mp3.partial");
            var finalPath = Path.Combine(DownloadDirectory, $"{episode.EpisodeId}.mp3");

            // A previous attempt may have gotten partway through before failing (lost connection,
            // airplane mode) - resume from there via an HTTP Range request instead of starting a
            // 2-hour file over from byte 0 every time.
            var resumeFromByte = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
            DiagnosticLog.Write(resumeFromByte > 0
                ? $"[Audiobookshelf] resuming download: episode {episode.EpisodeId} ({episode.EpisodeName}) from byte {resumeFromByte}"
                : $"[Audiobookshelf] download started: episode {episode.EpisodeId} ({episode.EpisodeName})");

            // Throttled - a fast connection can call this hundreds of times a second, which is both
            // pointless to show and needlessly loads the UI thread with property-changed notifications.
            var lastReportUtc = DateTime.MinValue;
            void OnProgress(long bytesSoFar, long? totalBytes)
            {
                var now = DateTime.UtcNow;
                if (now - lastReportUtc < TimeSpan.FromMilliseconds(250)) return;
                lastReportUtc = now;

                var fraction = totalBytes is > 0 ? bytesSoFar / (double)totalBytes.Value : -1;
                SetProgress(episode.EpisodeId, fraction);
                MainThread.BeginInvokeOnMainThread(() => episode.DownloadProgress = fraction);
            }

            // Forced onto a background thread rather than just awaited: ToggleDownload is invoked
            // directly from a UI-thread Command, and Android's main-thread network policy has been
            // seen killing this with a NetworkOnMainThreadException when connectivity drops mid-request
            // (the native HTTP handler apparently does some of its work synchronously in that case).
            await Task.Run(() => AudiobookshelfApiClient.DownloadEpisodeFileAsync(episode.LibraryItemId, episode.AudioFileIno, partialPath, resumeFromByte, OnProgress, cancellationToken), cancellationToken);

            var sizeBytes = new FileInfo(partialPath).Length;
            if (sizeBytes <= 0) throw new IOException("downloaded file is empty");

            File.Move(partialPath, finalPath, overwrite: true);

            var index = GetIndex();
            index[episode.EpisodeId] = new DownloadRecord { LocalPath = finalPath, Chapters = episode.Chapters };
            SaveIndex(index);

            episode.IsDownloaded = true;
            DiagnosticLog.Write($"[Audiobookshelf] download finished: episode {episode.EpisodeId} -> {finalPath} ({sizeBytes / 1024.0 / 1024.0:F1} MB, {episode.Chapters.Count} chapter marker(s) cached)");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Audiobookshelf] download failed for episode {episode.EpisodeId}: {ex.Message} (partial progress kept - tap download again to resume)");
        }
        finally
        {
            episode.IsDownloading = false;
            episode.DownloadProgress = -1;
            lock (DownloadingEpisodeIds)
            {
                DownloadingEpisodeIds.Remove(episode.EpisodeId);
                if (DownloadingEpisodeIds.Count == 0) AudiobookshelfDownloadBackgroundService.Stop();
            }
            lock (DownloadProgressByEpisodeId) DownloadProgressByEpisodeId.Remove(episode.EpisodeId);
        }
    }

    private static void SetProgress(string episodeId, double fraction)
    {
        lock (DownloadProgressByEpisodeId) DownloadProgressByEpisodeId[episodeId] = fraction;
    }

    public static void DeleteDownload(AudiobookshelfEpisode episode)
    {
        var index = GetIndex();
        if (index.TryGetValue(episode.EpisodeId, out var record))
        {
            try
            {
                File.Delete(record.LocalPath);
                DiagnosticLog.Write($"[Audiobookshelf] deleted download for episode {episode.EpisodeId} ({record.LocalPath})");
            }
            catch (Exception ex) { DiagnosticLog.Write($"[Audiobookshelf] failed to delete download for episode {episode.EpisodeId}: {ex.Message}"); }

            index.Remove(episode.EpisodeId);
            SaveIndex(index);
        }

        episode.IsDownloaded = false;
    }

    private static Dictionary<string, DownloadRecord> GetIndex()
    {
        return _index ??= DiskCacheHelper.Load<Dictionary<string, DownloadRecord>>(INDEX_CACHE_KEY) ?? new Dictionary<string, DownloadRecord>();
    }

    private static void SaveIndex(Dictionary<string, DownloadRecord> index)
    {
        _index = index;
        DiskCacheHelper.Save(INDEX_CACHE_KEY, index);
    }
}
