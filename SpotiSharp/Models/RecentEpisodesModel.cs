using System.Globalization;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class RecentEpisodesModel
{
    private const int EPISODES_PER_SHOW = 3;

    private static readonly string[] ReleaseDateFormats = { "yyyy-MM-dd", "yyyy-MM", "yyyy" };

    private static List<RecentEpisode> _recentEpisodes = new List<RecentEpisode>();

    public static List<RecentEpisode> RecentEpisodes
    {
        get
        {
            LoadRecentEpisodes();
            return _recentEpisodes;
        }
        private set => _recentEpisodes = value;
    }

    internal static void LoadRecentEpisodes()
    {
        var result = new List<RecentEpisode>();

        foreach (var show in PlaylistListModel.SavedShows)
        {
            var episodes = APICaller.Instance?.GetPodcastEpisodesByPodcastId(show.Id);
            if (episodes == null) continue;

            result.AddRange(episodes
                .Select(episode => new RecentEpisode(
                    episode.Id,
                    episode.Name,
                    show.Name,
                    episode.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty,
                    ParseReleaseDate(episode.ReleaseDate)))
                .OrderByDescending(episode => episode.ReleaseDate)
                .Take(EPISODES_PER_SHOW));
        }

        _recentEpisodes = result.OrderByDescending(episode => episode.ReleaseDate).ToList();
    }

    private static DateTime ParseReleaseDate(string releaseDate)
    {
        return DateTime.TryParseExact(releaseDate, ReleaseDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.MinValue;
    }
}
