using System.Globalization;
using SpotiSharp.Helpers;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;

/// <summary>Fetches and disk-caches the flat, across-all-shows episode list for the AudioBookshelf
/// podcasts page. Mirrors <see cref="RecentEpisodesModel"/>'s shape but talks only to
/// <see cref="AudiobookshelfApiClient"/> — kept separate so nothing here can affect the Spotify path.</summary>
public static class AudiobookshelfPodcastsModel
{
    private const string ALL_EPISODES_CACHE_KEY = "audiobookshelf_episodes";
    private const string BINGE_NEXT_CACHE_KEY = "audiobookshelf_binge_next_episodes";
    private const int EPISODES_PER_SHOW = 5;

    public static List<AudiobookshelfEpisode>? GetDiskCachedEpisodes()
    {
        return DiskCacheHelper.Load<List<AudiobookshelfEpisode>>(ALL_EPISODES_CACHE_KEY);
    }

    /// <summary>The next unfinished episode, in feed order, for each AudioBookshelf show pinned as
    /// "binge" - keyed by library item id. Refreshed by <see cref="RefreshBingeNextEpisodesAsync"/>,
    /// read synchronously by <see cref="AudiobookshelfPodcastEpisodeProvider"/> since Radio's episode
    /// selection can't make a network round trip of its own.</summary>
    internal static Dictionary<string, AudiobookshelfEpisode> GetDiskCachedBingeNextEpisodes()
    {
        return DiskCacheHelper.Load<Dictionary<string, AudiobookshelfEpisode>>(BINGE_NEXT_CACHE_KEY) ?? new Dictionary<string, AudiobookshelfEpisode>();
    }

    /// <summary>Picks, for every show pinned as "binge", the oldest episode not yet finished among
    /// what AudioBookshelf has already downloaded (not the full RSS back-catalog the way
    /// <see cref="RadioAudiobookshelfBingeAheadModel"/> looks ahead - only a downloaded episode has an
    /// audio file id Radio can actually play). Keeps <see cref="AudiobookshelfPodcastEpisodeProvider"/>
    /// from falling back to "most recently released", which ignores binge position entirely and is
    /// what was pulling the newest episode of a binged show into Radio instead of the next one in
    /// sequence.</summary>
    internal static async Task RefreshBingeNextEpisodesAsync()
    {
        if (!AudiobookshelfApiClient.IsConfigured) return;

        var bingeShowIds = RadioAudiobookshelfConfigModel.GetAllBingeShowIds();
        var result = new Dictionary<string, AudiobookshelfEpisode>();

        if (bingeShowIds.Count > 0)
        {
            var progressByShowAndEpisode = (await AudiobookshelfApiClient.GetMediaProgressAsync())
                .ToDictionary(p => (p.LibraryItemId, p.EpisodeId ?? string.Empty));

            foreach (var showId in bingeShowIds)
            {
                try
                {
                    var episode = await FindNextDownloadedBingeEpisodeAsync(showId, progressByShowAndEpisode);
                    if (episode != null) result[showId] = episode;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Write($"[Audiobookshelf] binge-next lookup failed for {showId}: {ex.Message}");
                }
            }
        }

        DiskCacheHelper.Save(BINGE_NEXT_CACHE_KEY, result);
    }

    private static async Task<AudiobookshelfEpisode?> FindNextDownloadedBingeEpisodeAsync(
        string libraryItemId, Dictionary<(string LibraryItemId, string EpisodeId), AudiobookshelfMediaProgress> progressByShowAndEpisode)
    {
        var item = await AudiobookshelfApiClient.GetLibraryItemAsync(libraryItemId);
        var downloaded = item?.Media?.Episodes;
        if (downloaded == null || downloaded.Count == 0) return null;

        var ordered = downloaded.OrderBy(ParsePubDate).ToList();

        // An explicit "Binge from here" pick always wins, same rule as the ahead-downloader.
        var anchorEpisodeId = RadioAudiobookshelfConfigModel.GetBinge(libraryItemId)?.LastTrackedEpisodeId;
        var startIndex = !string.IsNullOrEmpty(anchorEpisodeId)
            ? ordered.FindIndex(episode => episode.Id == anchorEpisodeId)
            : -1;

        if (startIndex < 0)
        {
            startIndex = ordered.FindIndex(episode =>
                !progressByShowAndEpisode.TryGetValue((libraryItemId, episode.Id), out var progress) || !progress.IsFinished);
        }
        if (startIndex < 0) return null;

        var next = ordered[startIndex];
        var showName = item!.Media?.Metadata?.Title ?? "Unknown show";
        var coverUrl = BuildCoverUrl(libraryItemId);

        var resumeMs = progressByShowAndEpisode.TryGetValue((libraryItemId, next.Id), out var nextProgress) && !nextProgress.IsFinished
            ? (int)(nextProgress.CurrentTimeSeconds * 1000)
            : 0;

        var chapters = next.Chapters?
            .Select(chapter => new AudiobookshelfChapter(chapter.StartSeconds, chapter.EndSeconds, chapter.Tags?.Title))
            .ToList();

        return new AudiobookshelfEpisode(
            next.Id, libraryItemId, next.AudioFile?.Ino ?? string.Empty, next.Title, showName, coverUrl,
            ParsePubDate(next), (int)(next.DurationSeconds * 1000), resumeMs, chapters);
    }

    public static async Task<List<AudiobookshelfEpisode>?> RefreshEpisodesAsync()
    {
        if (!AudiobookshelfApiClient.IsConfigured) return null;

        try
        {
            var progressByKey = (await AudiobookshelfApiClient.GetMediaProgressAsync())
                .ToDictionary(p => (p.LibraryItemId, p.EpisodeId ?? string.Empty));

            var podcasts = await AudiobookshelfApiClient.GetAllPodcastsAsync();
            var result = new List<AudiobookshelfEpisode>();

            foreach (var podcast in podcasts)
            {
                var showName = podcast.Media?.Metadata?.Title ?? "Unknown show";
                var coverUrl = BuildCoverUrl(podcast.Id);

                var episodes = await AudiobookshelfApiClient.GetEpisodesAsync(podcast.Id);
                var recent = episodes
                    .OrderByDescending(ParsePubDate)
                    .Take(EPISODES_PER_SHOW)
                    .Select(episode =>
                    {
                        // A finished episode resumes from the start, not from a second before the end.
                        var resumeMs = progressByKey.TryGetValue((podcast.Id, episode.Id), out var progress) && !progress.IsFinished
                            ? (int)(progress.CurrentTimeSeconds * 1000)
                            : 0;

                        var chapters = episode.Chapters?
                            .Select(chapter => new AudiobookshelfChapter(chapter.StartSeconds, chapter.EndSeconds, chapter.Tags?.Title))
                            .ToList();

                        return new AudiobookshelfEpisode(
                            episode.Id,
                            podcast.Id,
                            episode.AudioFile?.Ino ?? string.Empty,
                            episode.Title,
                            showName,
                            coverUrl,
                            ParsePubDate(episode),
                            (int)(episode.DurationSeconds * 1000),
                            resumeMs,
                            chapters);
                    });

                result.AddRange(recent);
            }

            var ordered = result.OrderByDescending(episode => episode.ReleaseDate).ToList();
            DiskCacheHelper.Save(ALL_EPISODES_CACHE_KEY, ordered);
            return ordered;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Audiobookshelf] episode refresh failed: {ex.Message}");
            return null;
        }
    }

    internal static string BuildCoverUrl(string libraryItemId)
    {
        var serverUrl = StorageHandler.AudiobookshelfServerUrl.TrimEnd('/');
        return $"{serverUrl}/api/items/{libraryItemId}/cover?token={Uri.EscapeDataString(StorageHandler.AudiobookshelfApiKey)}";
    }

    /// <summary>Just the show list (id/name/cover), for the Podcasts page's show-grouped layout - no
    /// episodes fetched yet, those come lazily per show on expand (<see cref="FetchEpisodesForShowAsync"/>),
    /// same as the Spotify Podcasts page. Deliberately separate from <see cref="RefreshEpisodesAsync"/>'s
    /// flat, capped-per-show cache, which feeds the AudioBookshelf Radio episode provider
    /// (<see cref="AudiobookshelfPodcastEpisodeProvider"/>) and must not change shape for this page's sake.</summary>
    internal static async Task<List<(string libraryItemId, string name, string coverUrl)>> GetShowsAsync()
    {
        if (!AudiobookshelfApiClient.IsConfigured) return new List<(string, string, string)>();

        var podcasts = await AudiobookshelfApiClient.GetAllPodcastsAsync();
        return podcasts
            .Select(podcast => (podcast.Id, podcast.Media?.Metadata?.Title ?? "Unknown show", BuildCoverUrl(podcast.Id)))
            .ToList();
    }

    /// <summary>Every episode AudioBookshelf has for one show, newest first - the full back catalog
    /// (<see cref="AudiobookshelfApiClient.GetEpisodesAsync"/> already returns all of it, not just
    /// recent ones), for the Podcasts page's per-show expand. "Load more" there just reveals more of
    /// this already-fetched list, no extra round trip needed.</summary>
    internal static async Task<List<AudiobookshelfEpisode>> FetchEpisodesForShowAsync(string libraryItemId, string showName, string showCoverUrl)
    {
        if (!AudiobookshelfApiClient.IsConfigured) return new List<AudiobookshelfEpisode>();

        var progressByEpisodeId = (await AudiobookshelfApiClient.GetMediaProgressAsync())
            .Where(progress => progress.LibraryItemId == libraryItemId)
            .ToDictionary(progress => progress.EpisodeId ?? string.Empty);

        var episodes = await AudiobookshelfApiClient.GetEpisodesAsync(libraryItemId);

        return episodes
            .Select(episode =>
            {
                var resumeMs = progressByEpisodeId.TryGetValue(episode.Id, out var progress) && !progress.IsFinished
                    ? (int)(progress.CurrentTimeSeconds * 1000)
                    : 0;

                var chapters = episode.Chapters?
                    .Select(chapter => new AudiobookshelfChapter(chapter.StartSeconds, chapter.EndSeconds, chapter.Tags?.Title))
                    .ToList();

                return new AudiobookshelfEpisode(
                    episode.Id,
                    libraryItemId,
                    episode.AudioFile?.Ino ?? string.Empty,
                    episode.Title,
                    showName,
                    showCoverUrl,
                    ParsePubDate(episode),
                    (int)(episode.DurationSeconds * 1000),
                    resumeMs,
                    chapters);
            })
            .OrderByDescending(episode => episode.ReleaseDate)
            .ToList();
    }

    private static DateTime ParsePubDate(AudiobookshelfEpisodeDto episode) => ParsePubDate(episode.PubDate);

    private static DateTime ParsePubDate(string? pubDate)
    {
        return DateTime.TryParse(pubDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.MinValue;
    }
}
