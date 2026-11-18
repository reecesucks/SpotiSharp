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

    /// <summary>Resolves an episode id against both episode caches. The recent-episodes cache
    /// (<see cref="GetDiskCachedEpisodes"/>) only holds a capped, per-show list of the most recently
    /// released episodes, so a show being binged through its back-catalog has its current episode in
    /// <see cref="GetDiskCachedBingeNextEpisodes"/> instead - checking the recent list alone misses it.</summary>
    public static AudiobookshelfEpisode? FindCachedEpisode(string episodeId)
    {
        var recent = GetDiskCachedEpisodes()?.FirstOrDefault(candidate => candidate.EpisodeId == episodeId);
        if (recent != null) return recent;

        return GetDiskCachedBingeNextEpisodes().Values
            .FirstOrDefault(candidate => candidate.EpisodeId == episodeId);
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

        DiagnosticLog.Write($"[Audiobookshelf] binge-next refresh: {bingeShowIds.Count} pinned show(s), {result.Count} with a next episode");
        DiskCacheHelper.Save(BINGE_NEXT_CACHE_KEY, result);
    }

    // Logging what actually comes back here (count and the raw title values) is how the Start/End/Title
    // shape mismatch that silently zeroed out every splice point was found - keep it, in case the
    // server's tagging shape moves again.
    private static List<AudiobookshelfChapter>? MapChapters(string episodeTitle, List<AudiobookshelfChapterDto>? rawChapters)
    {
        if (rawChapters == null)
        {
            DiagnosticLog.Write($"[Audiobookshelf] \"{episodeTitle}\" has no chapters field at all from the server");
            return null;
        }

        var chapters = rawChapters
            .Select(chapter => new AudiobookshelfChapter(chapter.StartSeconds, chapter.EndSeconds, chapter.Title))
            .ToList();

        int spliceCount = chapters.Count(chapter => chapter.IsSplice);
        DiagnosticLog.Write($"[Audiobookshelf] \"{episodeTitle}\" has {chapters.Count} chapter(s) from the server, {spliceCount} tagged splice" +
            (chapters.Count > 0 ? $" (tags seen: {string.Join(", ", chapters.Select(chapter => chapter.Tag ?? "null").Distinct())})" : ""));

        return chapters;
    }

    private static async Task<AudiobookshelfEpisode?> FindNextDownloadedBingeEpisodeAsync(
        string libraryItemId, Dictionary<(string LibraryItemId, string EpisodeId), AudiobookshelfMediaProgress> progressByShowAndEpisode)
    {
        var item = await AudiobookshelfApiClient.GetLibraryItemAsync(libraryItemId);
        var downloaded = item?.Media?.Episodes;
        if (downloaded == null || downloaded.Count == 0)
        {
            DiagnosticLog.Write($"[Audiobookshelf] binge-next for {libraryItemId}: server reports 0 episodes for this item");
            return null;
        }

        var ordered = downloaded.OrderBy(ParsePubDate).ToList();

        // An explicit "Binge from here" pick wins while it's still unfinished, same rule as the
        // ahead-downloader (RadioAudiobookshelfBingeAheadModel.FindBingePositionAsync) - nothing ever
        // moves the pin forward as episodes finish, so a finished pin has to fall back to the first
        // unfinished episode or this show gets stuck re-offering the same finished episode forever.
        // Matched by feed guid first - stable across AudioBookshelf deleting/re-downloading the pinned
        // episode, unlike its own episode id, which stops matching anything once that happens (the
        // id-based anchor is kept only for binges pinned before AnchorGuid existed).
        var binge = RadioAudiobookshelfConfigModel.GetBinge(libraryItemId);
        var anchorIndex = !string.IsNullOrEmpty(binge?.AnchorGuid)
            ? ordered.FindIndex(episode => episode.Guid == binge.AnchorGuid)
            : -1;
        if (anchorIndex < 0 && !string.IsNullOrEmpty(binge?.LastTrackedEpisodeId))
            anchorIndex = ordered.FindIndex(episode => episode.Id == binge.LastTrackedEpisodeId);
        bool anchorFinished = anchorIndex >= 0 &&
            progressByShowAndEpisode.TryGetValue((libraryItemId, ordered[anchorIndex].Id), out var anchorProgress) && anchorProgress.IsFinished;

        var startIndex = anchorIndex >= 0 && !anchorFinished ? anchorIndex : -1;

        if (startIndex < 0)
        {
            startIndex = ordered.FindIndex(episode =>
                !progressByShowAndEpisode.TryGetValue((libraryItemId, episode.Id), out var progress) || !progress.IsFinished);
        }
        if (startIndex < 0)
        {
            DiagnosticLog.Write($"[Audiobookshelf] binge-next for {libraryItemId}: all {ordered.Count} episode(s) show as finished - nothing to offer");
            return null;
        }

        var next = ordered[startIndex];
        var showName = item!.Media?.Metadata?.Title ?? "Unknown show";
        var coverUrl = BuildCoverUrl(libraryItemId);

        var resumeMs = progressByShowAndEpisode.TryGetValue((libraryItemId, next.Id), out var nextProgress) && !nextProgress.IsFinished
            ? (int)(nextProgress.CurrentTimeSeconds * 1000)
            : 0;

        var chapters = MapChapters(next.Title, next.Chapters);

        return new AudiobookshelfEpisode(
            next.Id, libraryItemId, next.AudioFile?.Ino ?? string.Empty, next.Title, showName, coverUrl,
            ParsePubDate(next), (int)(next.DurationSeconds * 1000), resumeMs, chapters, guid: next.Guid);
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
                        var hasProgress = progressByKey.TryGetValue((podcast.Id, episode.Id), out var progress);
                        var resumeMs = hasProgress && !progress.IsFinished
                            ? (int)(progress.CurrentTimeSeconds * 1000)
                            : 0;

                        var chapters = MapChapters(episode.Title, episode.Chapters);

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
                            chapters,
                            isFinished: hasProgress && progress.IsFinished,
                            guid: episode.Guid);
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
                var hasProgress = progressByEpisodeId.TryGetValue(episode.Id, out var progress);
                var resumeMs = hasProgress && !progress.IsFinished
                    ? (int)(progress.CurrentTimeSeconds * 1000)
                    : 0;

                var chapters = MapChapters(episode.Title, episode.Chapters);

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
                    chapters,
                    isFinished: hasProgress && progress.IsFinished,
                    guid: episode.Guid);
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
