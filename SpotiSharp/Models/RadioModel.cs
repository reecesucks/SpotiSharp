using System.Text.RegularExpressions;

namespace SpotiSharp.Models;

public class RadioModel
{
    private const int SEGMENT_LENGTH_MS = 15 * 60 * 1000;
    private const int SONGS_BETWEEN_SEGMENTS = 3;
    private const int EPISODE_COUNT = 3;

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

        var showWeights = ActiveWeights(RadioConfigModel.Config.ShowWeights);
        if (showWeights.Count == 0) return episodes.Take(EPISODE_COUNT).ToList();

        var random = new Random();
        return episodes
            .Where(episode => showWeights.ContainsKey(episode.ShowId))
            .OrderByDescending(episode => Math.Pow(random.NextDouble(), 1.0 / showWeights[episode.ShowId]))
            .Take(EPISODE_COUNT)
            .OrderByDescending(episode => episode.ReleaseDate)
            .ToList();
    }

    private static List<RadioItem> BuildSongPool()
    {
        var playlistWeights = ActiveWeights(RadioConfigModel.Config.PlaylistWeights);
        if (playlistWeights.Count == 0)
        {
            playlistWeights = PlaylistListModel.PlayLists
                .Where(playlist => RotationTag.IsMatch(playlist.PlayListTitle ?? string.Empty))
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
