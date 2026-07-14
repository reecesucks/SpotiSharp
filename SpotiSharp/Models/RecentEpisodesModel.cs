using System.Collections.Concurrent;
using System.Globalization;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class RecentEpisodesModel
{
    private const int EPISODES_PER_SHOW = 3;

    private static readonly string[] ReleaseDateFormats = { "yyyy-MM-dd", "yyyy-MM", "yyyy" };

    private static readonly ConcurrentDictionary<string, List<RecentEpisode>> _episodesByShowId = new();

    internal static List<RecentEpisode> GetRecentEpisodesAcrossAllShows()
    {
        var result = new List<RecentEpisode>();

        foreach (var show in PlaylistListModel.SavedShows)
        {
            result.AddRange(GetRecentEpisodesForShow(show.Id, show.Name, show.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty));
        }

        return result.OrderByDescending(episode => episode.ReleaseDate).ToList();
    }

    internal static List<RecentEpisode> GetRecentEpisodesForShow(string showId, string showName, string showImageUrl)
    {
        return _episodesByShowId.GetOrAdd(showId, id => FetchRecentEpisodesForShow(id, showName, showImageUrl));
    }

    private static List<RecentEpisode> FetchRecentEpisodesForShow(string showId, string showName, string showImageUrl)
    {
        var episodes = APICaller.Instance?.GetPodcastEpisodesByPodcastId(showId)?.Where(episode => episode != null);
        if (episodes == null) return new List<RecentEpisode>();

        return episodes
            .Where(episode => !EpisodeHelper.IsListened(episode))
            .Select(episode => new RecentEpisode(
                episode.Id,
                episode.Name,
                showName,
                showImageUrl,
                ParseReleaseDate(episode.ReleaseDate)))
            .OrderByDescending(episode => episode.ReleaseDate)
            .Take(EPISODES_PER_SHOW)
            .ToList();
    }

    private static DateTime ParseReleaseDate(string releaseDate)
    {
        return DateTime.TryParseExact(releaseDate, ReleaseDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.MinValue;
    }
}
