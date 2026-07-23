using System.Text.RegularExpressions;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class RadioModel
{
    internal const int SEGMENT_LENGTH_MS = SpotiSharpBackend.Radio.RadioTuning.SEGMENT_LENGTH_MS;
    internal const int SONGS_BETWEEN_SEGMENTS = 3;
    private const int EPISODE_COUNT = 3;
    private const int ALBUM_SONG_COUNT = 4;

    private const int RESUME_IGNORE_THRESHOLD_MS = 30 * 1000;

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

        var resumePositions = APICaller.Instance?.GetEpisodeResumePositions(
            episodes.Select(episode => episode.EpisodeId).Where(id => !string.IsNullOrEmpty(id)).ToList());

        var radio = new List<RadioItem>();
        int songIndex = 0;

        foreach (var episode in episodes)
        {
            int startMs = ResumeStartFor(episode, resumePositions);
            int remainingMs = Math.Max(0, episode.DurationMs - startMs);
            int segmentCount = Math.Max(1, (int)Math.Ceiling(remainingMs / (double)SEGMENT_LENGTH_MS));

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                if (radio.Count > 0) AddSongs(radio, songPool, ref songIndex, SONGS_BETWEEN_SEGMENTS);
                radio.Add(RadioItem.ForPodcastSegment(episode, segmentIndex, segmentCount, SEGMENT_LENGTH_MS, startMs));
            }
        }

        InsertAlbumSongs(radio);

        SaveRadio(radio);
        return radio;
    }

    private static void InsertAlbumSongs(List<RadioItem> radio)
    {
        var albumModes = RadioConfigModel.Config.AlbumModes
            .Where(entry => entry.Value != RadioAlbumMode.Off)
            .ToList();
        if (albumModes.Count == 0) return;

        var savedAlbums = SavedAlbumsModel.CachedAlbums;
        var random = new Random();

        foreach (var (albumId, mode) in albumModes)
        {
            var album = savedAlbums.FirstOrDefault(savedAlbum => savedAlbum.AlbumId == albumId);
            if (album == null) continue;

            var songs = AlbumSongsModel.GetSongsCachedFirst(albumId);
            if (songs == null || songs.Count == 0) continue;

            // a random handful, kept in album track order
            var picked = songs
                .OrderBy(_ => random.Next())
                .Take(ALBUM_SONG_COUNT)
                .OrderBy(song => songs.IndexOf(song))
                .Select(song => RadioItem.ForSong(song.SongTitle, song.SongArtists, album.AlbumImageUrl, song.SongUri))
                .ToList();

            if (mode == RadioAlbumMode.Consecutive)
            {
                radio.InsertRange(random.Next(radio.Count + 1), picked);
            }
            else
            {
                foreach (var item in picked)
                {
                    radio.Insert(random.Next(radio.Count + 1), item);
                }
            }
        }
    }

    private static int ResumeStartFor(RecentEpisode episode, Dictionary<string, int> livePositions)
    {
        int resume = livePositions != null && livePositions.TryGetValue(episode.EpisodeId, out var live)
            ? live
            : episode.ResumePositionMs;

        if (resume < RESUME_IGNORE_THRESHOLD_MS || resume >= episode.DurationMs) return 0;
        return resume;
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
                var next = BingeProgressModel.FindNextEpisode(showId, show?.Name ?? string.Empty, ImageHelper.Thumbnail(show?.Images));
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

    // the playlists currently feeding the radio's song pool
    internal static List<string> SourcePlaylistIds()
    {
        return SourcePlaylistWeights().Keys.ToList();
    }

    private static Dictionary<string, int> SourcePlaylistWeights()
    {
        var livePlaylistIds = PlaylistListModel.PlayLists.Select(playlist => playlist.PlayListId).ToHashSet();

        var configuredPlaylistWeights = RadioConfigModel.Config.PlaylistWeights;
        var playlistWeights = ActiveWeights(configuredPlaylistWeights);
        if (playlistWeights.Count == 0)
        {
            return PlaylistListModel.PlayLists
                .Where(playlist => RotationTag.IsMatch(playlist.PlayListTitle ?? string.Empty))
                .Where(playlist => !RadioConfigModel.IsExplicitlyOff(configuredPlaylistWeights, playlist.PlayListId))
                .ToDictionary(playlist => playlist.PlayListId, _ => 1);
        }

        return playlistWeights
            .Where(entry => livePlaylistIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static List<RadioItem> BuildSongPool()
    {
        var playlistWeights = SourcePlaylistWeights();

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
