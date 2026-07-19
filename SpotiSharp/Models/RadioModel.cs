using System.Text.RegularExpressions;
using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

public class RadioModel
{
    internal const int SEGMENT_LENGTH_MS = 15 * 60 * 1000;
    private const int SONGS_BETWEEN_SEGMENTS = 3;
    private const int EPISODE_COUNT = 3;

    private const string RADIO_CACHE_KEY = "radio";

    internal static List<RadioItem> CachedRadio => DiskCacheHelper.Load<List<RadioItem>>(RADIO_CACHE_KEY);

    internal static void SaveRadio(List<RadioItem> radio)
    {
        DiskCacheHelper.Save(RADIO_CACHE_KEY, radio);
    }

    private static readonly Regex RotationTag = new Regex(@"#R-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static List<RadioItem> Generate()
    {
        var episodes = GetEpisodes();
        if (episodes == null) return null;

        var songPool = BuildSongPool();
        if (songPool == null) return null;

        var radio = new List<RadioItem>();
        int songIndex = 0;

        foreach (var episode in episodes)
        {
            int segmentCount = Math.Max(1, (int)Math.Ceiling(episode.DurationMs / (double)SEGMENT_LENGTH_MS));
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                if (radio.Count > 0) AddSongs(radio, songPool, ref songIndex, SONGS_BETWEEN_SEGMENTS);
                radio.Add(RadioItem.ForPodcastSegment(episode, segmentIndex, segmentCount, SEGMENT_LENGTH_MS));
            }
        }

        SaveRadio(radio);
        return radio;
    }

    private static void AddSongs(List<RadioItem> radio, List<RadioItem> songPool, ref int songIndex, int amount)
    {
        for (int i = 0; i < amount && songIndex < songPool.Count; i++)
        {
            radio.Add(songPool[songIndex]);
            songIndex++;
        }
    }

    private static List<RecentEpisode> GetEpisodes()
    {
        var cached = RecentEpisodesModel.GetDiskCachedEpisodesAcrossAllShows();
        var episodes = cached != null && cached.Count > 0 && cached.All(episode => episode.DurationMs > 0 && !string.IsNullOrEmpty(episode.ShowId))
            ? cached
            : RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows();
        if (episodes == null) return null;

        var configuredShowWeights = RadioConfigModel.Config.ShowWeights;
        var showWeights = ActiveWeights(configuredShowWeights);
        var bingeShowIds = RadioConfigModel.Config.BingeShows.Keys.ToList();

        var bingeEpisodes = new List<RecentEpisode>();
        if (bingeShowIds.Count > 0)
        {
            var savedShows = PlaylistListModel.SavedShows;
            foreach (var showId in bingeShowIds)
            {
                bool excluded = showWeights.Count > 0
                    ? !showWeights.ContainsKey(showId)
                    : RadioConfigModel.IsExplicitlyOff(configuredShowWeights, showId);
                if (excluded) continue;
                var show = savedShows.FirstOrDefault(savedShow => savedShow.Id == showId);
                var next = BingeProgressModel.FindNextEpisode(showId, show?.Name ?? string.Empty, show?.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty);
                if (next != null) bingeEpisodes.Add(next);
            }
            episodes = episodes.Where(episode => !bingeShowIds.Contains(episode.ShowId)).ToList();
        }

        List<RecentEpisode> chosen;
        if (showWeights.Count == 0)
        {
            chosen = bingeEpisodes
                .Concat(episodes.Where(episode => !RadioConfigModel.IsExplicitlyOff(configuredShowWeights, episode.ShowId)))
                .Take(EPISODE_COUNT)
                .ToList();
        }
        else
        {
            var random = new Random();
            chosen = bingeEpisodes
                .Concat(episodes.Where(episode => showWeights.ContainsKey(episode.ShowId)))
                .OrderByDescending(episode => Math.Pow(random.NextDouble(), 1.0 / showWeights[episode.ShowId]))
                .Take(EPISODE_COUNT)
                .ToList();
        }

        return chosen
            .OrderByDescending(episode => bingeShowIds.Contains(episode.ShowId))
            .ThenByDescending(episode => episode.ReleaseDate)
            .ToList();
    }

    private static List<RadioItem> BuildSongPool()
    {
        var configuredPlaylistWeights = RadioConfigModel.Config.PlaylistWeights;
        var playlistWeights = ActiveWeights(configuredPlaylistWeights);
        if (playlistWeights.Count == 0)
        {
            playlistWeights = PlaylistListModel.PlayLists
                .Where(playlist => RotationTag.IsMatch(playlist.PlayListTitle ?? string.Empty))
                .Where(playlist => !RadioConfigModel.IsExplicitlyOff(configuredPlaylistWeights, playlist.PlayListId))
                .ToDictionary(playlist => playlist.PlayListId, _ => 1);
        }

        var songWeights = new Dictionary<string, (RadioItem Item, int Weight)>();
        foreach (var (playlistId, weight) in playlistWeights)
        {
            var tracks = RotationTracksModel.GetTracks(playlistId);
            if (tracks == null) return null;

            foreach (var track in tracks)
            {
                if (songWeights.TryGetValue(track.SongUri, out var existing) && existing.Weight >= weight) continue;
                songWeights[track.SongUri] = (RadioItem.ForSong(track.SongTitle, track.SongArtists, track.SongImageUrl, track.SongUri), weight);
            }
        }

        var random = new Random();
        return songWeights.Values
            .OrderByDescending(entry => Math.Pow(random.NextDouble(), 1.0 / entry.Weight))
            .Select(entry => entry.Item)
            .ToList();
    }

    private static Dictionary<string, int> ActiveWeights(Dictionary<string, int> weights)
    {
        return weights.Where(entry => entry.Value > 0).ToDictionary(entry => entry.Key, entry => entry.Value);
    }
}
