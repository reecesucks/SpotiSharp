using System.Collections.Concurrent;
using System.Globalization;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class RecentEpisodesModel
{
    private const int EPISODES_PER_SHOW = 3;
    private const string ALL_EPISODES_CACHE_KEY = "recentepisodes";

    private static readonly string[] ReleaseDateFormats = { "yyyy-MM-dd", "yyyy-MM", "yyyy" };

    private static readonly ConcurrentDictionary<string, List<RecentEpisode>> _episodesByShowId = new();

    internal static void ClearMemory()
    {
        _episodesByShowId.Clear();
    }

    internal static List<RecentEpisode> GetSessionEpisodesForShow(string showId)
    {
        return _episodesByShowId.TryGetValue(showId, out var episodes) ? episodes : null;
    }

    internal static List<RecentEpisode> GetDiskCachedEpisodesForShow(string showId)
    {
        return DiskCacheHelper.Load<List<RecentEpisode>>(EpisodeCacheKey(showId));
    }

    internal static List<RecentEpisode> GetDiskCachedEpisodesAcrossAllShows()
    {
        return DiskCacheHelper.Load<List<RecentEpisode>>(ALL_EPISODES_CACHE_KEY);
    }

    internal static List<RecentEpisode> RefreshEpisodesForShow(string showId, string showName, string showImageUrl)
    {
        var fetched = FetchRecentEpisodesForShow(showId, showName, showImageUrl);
        if (fetched == null) return null;

        _episodesByShowId[showId] = fetched;
        DiskCacheHelper.Save(EpisodeCacheKey(showId), fetched);
        return fetched;
    }

    internal static List<RecentEpisode> RefreshRecentEpisodesAcrossAllShows(bool forceRefresh = false)
    {
        var result = new List<RecentEpisode>();

        foreach (var show in PlaylistListModel.SavedShows)
        {
            var episodes = (forceRefresh ? null : GetSessionEpisodesForShow(show.Id))
                ?? RefreshEpisodesForShow(show.Id, show.Name, ImageHelper.Thumbnail(show.Images));
            if (episodes == null) return null;
            result.AddRange(episodes);
        }

        var ordered = result.OrderByDescending(episode => episode.ReleaseDate).ToList();
        DiskCacheHelper.Save(ALL_EPISODES_CACHE_KEY, ordered);
        return ordered;
    }

    internal static bool AreEpisodesEqual(List<RecentEpisode> current, List<RecentEpisode> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) =>
            a.EpisodeId == b.EpisodeId &&
            a.EpisodeName == b.EpisodeName).All(equal => equal);
    }

    private static List<RecentEpisode> FetchRecentEpisodesForShow(string showId, string showName, string showImageUrl)
    {
        var episodes = APICaller.Instance?.GetPodcastEpisodesByPodcastId(showId)?.Where(episode => episode != null);
        if (episodes == null) return null;

        return episodes
            .Where(episode => !EpisodeHelper.IsListened(episode))
            .Select(episode => new RecentEpisode(
                episode.Id,
                episode.Name,
                showId,
                showName,
                showImageUrl,
                ParseReleaseDate(episode.ReleaseDate),
                episode.DurationMs))
            .OrderByDescending(episode => episode.ReleaseDate)
            .Take(EPISODES_PER_SHOW)
            .ToList();
    }

    private static string EpisodeCacheKey(string showId)
    {
        return "episodes_" + showId;
    }

    internal static DateTime ParseReleaseDate(string releaseDate)
    {
        return DateTime.TryParseExact(releaseDate, ReleaseDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.MinValue;
    }
}
