using System.Text.RegularExpressions;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class RadioModel
{
    internal static int SegmentLengthMs => RadioConfigModel.PodcastSegmentLengthMs;
    internal static int SongsPerSection => RadioConfigModel.GetSongsPerSection();
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
        PlaylistListModel.RefreshPlayLists();

        var episodes = GetEpisodes(out var liveProgress, out var staleBingeEpisodeIds);
        if (episodes == null) return null;

        var songPool = BuildSongPool();
        if (songPool == null) return null;

        var radio = new List<RadioItem>();
        int songIndex = 0;

        bool fullEpisodes = RadioConfigModel.GetFullPodcastEpisodes();

        foreach (var episode in episodes)
        {
            int startMs = ResumeStartFor(episode, liveProgress, staleBingeEpisodeIds);
            int remainingMs = Math.Max(0, episode.DurationMs - startMs);
            int segmentCount = fullEpisodes ? 1 : SegmentCountFor(remainingMs);

            int totalSegments = fullEpisodes ? 1 : Math.Max(segmentCount, SegmentCountFor(episode.DurationMs));
            int firstSegmentNumber = totalSegments - segmentCount;

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                AddSongs(radio, songPool, ref songIndex, SongsPerSection);
                radio.Add(RadioItem.ForPodcastSegment(
                    episode, segmentIndex, SegmentLengthMs, startMs,
                    firstSegmentNumber + segmentIndex, totalSegments,
                    isFinalSegment: segmentIndex == segmentCount - 1));
            }
        }

        InsertAlbumSongs(radio);

        SaveRadio(radio);
        return radio;
    }

    private static int SegmentCountFor(int spanMs)
    {
        if (spanMs <= 0) return 1;

        int segmentLengthMs = SegmentLengthMs;
        int fullSegments = spanMs / segmentLengthMs;
        int leftoverMs = spanMs - fullSegments * segmentLengthMs;

        if (leftoverMs > segmentLengthMs / 3 || fullSegments == 0) fullSegments++;

        return fullSegments;
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

    private static int ResumeStartFor(RecentEpisode episode, Dictionary<string, EpisodeProgress> liveProgress, ISet<string> staleBingeEpisodeIds)
    {
        if (staleBingeEpisodeIds != null && staleBingeEpisodeIds.Contains(episode.EpisodeId)) return 0;

        int resume = liveProgress != null && liveProgress.TryGetValue(episode.EpisodeId, out var live)
            ? live.ResumePositionMs
            : episode.ResumePositionMs;

        // Both guards mean "no useful resume point", so the episode plays from the start.
        // Finished episodes must never reach here - Spotify reports them either reset to zero
        // or past the end, so both would silently queue a full replay. GetEpisodes drops them.
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

    /// <summary>
    /// Picks the episodes for this radio. The cached "unlistened" verdict is only as fresh as
    /// the episode cache, so what Spotify reports right now decides which are still unfinished;
    /// <paramref name="liveProgress"/> carries that on for resume positions and is null if
    /// the lookup failed, in which case the cached verdict is all we have.
    /// </summary>
    private static List<RecentEpisode> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress, out HashSet<string> staleBingeEpisodeIds)
    {
        liveProgress = null;
        staleBingeEpisodeIds = new HashSet<string>();

        var cached = RecentEpisodesModel.GetDiskCachedEpisodesAcrossAllShows();
        var episodes = cached != null && cached.Count > 0 && cached.All(episode => episode.DurationMs > 0 && !string.IsNullOrEmpty(episode.ShowId))
            ? cached
            : RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows();
        if (episodes == null) return null;

        var excludedEpisodeIds = RadioConfigModel.Config.ExcludedEpisodeIds;
        if (excludedEpisodeIds.Count > 0) episodes = episodes.Where(episode => !excludedEpisodeIds.Contains(episode.EpisodeId)).ToList();

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
                var next = BingeProgressModel.FindNextEpisode(showId, show?.Name ?? string.Empty, ImageHelper.Thumbnail(show?.Images), excludedEpisodeIds);
                if (next != null) bingeEpisodes.Add(next);
            }
            episodes = episodes.Where(episode => !bingeShowIds.Contains(episode.ShowId)).ToList();
        }

        // What Spotify reports right now, for the recent pool and the binge picks alike -
        // it decides what is still unfinished, and feeds ResumeStartFor further down.
        var progress = APICaller.Instance?.GetEpisodeProgress(
            episodes.Select(episode => episode.EpisodeId)
                .Concat(bingeEpisodes.Select(episode => episode.EpisodeId))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList());
        liveProgress = progress;

        foreach (var episode in bingeEpisodes)
        {
            int resumeMs = progress?.GetValueOrDefault(episode.EpisodeId)?.ResumePositionMs ?? episode.ResumePositionMs;
            BingeProgressModel.NoteResumeProgress(episode.ShowId, episode.EpisodeId, resumeMs);
            if (BingeProgressModel.IsStale(episode.ShowId)) staleBingeEpisodeIds.Add(episode.EpisodeId);
        }

        if (progress != null)
        {
            episodes = episodes
                .Where(episode => !EpisodeHelper.IsListened(
                    progress.GetValueOrDefault(episode.EpisodeId), episode.DurationMs))
                .ToList();
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
                .OrderByDescending(episode => Math.Pow(random.NextDouble(), 1.0 / EffectiveWeight(showWeights[episode.ShowId])))
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
            .OrderByDescending(entry => Math.Pow(random.NextDouble(), 1.0 / EffectiveWeight(entry.Weight)))
            .Select(entry => entry.Item)
            .ToList();
    }

    private static Dictionary<string, int> ActiveWeights(Dictionary<string, int> weights)
    {
        return weights.Where(entry => entry.Value > 0).ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static double EffectiveWeight(int weight) => (double)weight * weight;
}
