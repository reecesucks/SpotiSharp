using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

public static class RadioAudiobookshelfConfigModel
{
    private const string RADIO_CONFIG_KEY = "radioconfig";

    private const int MIN_SONGS_PER_SECTION = 1;
    private const int MAX_SONGS_PER_SECTION = 10;
    private const int MIN_PODCAST_SEGMENT_MINUTES = 5;
    private const int MAX_PODCAST_SEGMENT_MINUTES = 30;

    private static RadioAudiobookshelfConfig _config;

    internal static RadioAudiobookshelfConfig Config
    {
        get
        {
            if (_config == null)
            {
                _config = DiskCacheHelper.Load<RadioAudiobookshelfConfig>(RADIO_CONFIG_KEY) ?? new RadioAudiobookshelfConfig();
                MigrateLegacyToggles();
            }
            return _config;
        }
    }

    internal static int GetPlaylistWeight(string playlistId)
    {
        return Config.PlaylistWeights.GetValueOrDefault(playlistId);
    }

    internal static int GetShowWeight(string showId)
    {
        return Config.ShowWeights.GetValueOrDefault(showId);
    }

    internal static void SetPlaylistWeight(string playlistId, int weight)
    {
        SetWeight(Config.PlaylistWeights, playlistId, weight);
    }

    internal static void SetShowWeight(string showId, int weight)
    {
        SetWeight(Config.ShowWeights, showId, weight);
    }

    private static void SetWeight(Dictionary<string, int> weights, string id, int weight)
    {
        weights[id] = Math.Max(0, weight);
        Save();
    }

    internal static bool IsExplicitlyOff(Dictionary<string, int> weights, string id)
    {
        return weights.TryGetValue(id, out var weight) && weight <= 0;
    }

    internal static int GetSongsPerSection()
    {
        return Config.SongsPerSection;
    }

    internal static void SetSongsPerSection(int songs)
    {
        Config.SongsPerSection = Math.Clamp(songs, MIN_SONGS_PER_SECTION, MAX_SONGS_PER_SECTION);
        Save();
    }

    internal static int GetPodcastSegmentMinutes()
    {
        return Config.PodcastSegmentMinutes;
    }

    internal static void SetPodcastSegmentMinutes(int minutes)
    {
        Config.PodcastSegmentMinutes = Math.Clamp(minutes, MIN_PODCAST_SEGMENT_MINUTES, MAX_PODCAST_SEGMENT_MINUTES);
        Save();
    }

    internal static int PodcastSegmentLengthMs => Config.PodcastSegmentMinutes * 60 * 1000;

    internal static bool GetFullPodcastEpisodes()
    {
        return Config.FullPodcastEpisodes;
    }

    internal static void SetFullPodcastEpisodes(bool full)
    {
        Config.FullPodcastEpisodes = full;
        Save();
    }

    internal static RadioAudiobookshelfListenMode GetListenMode()
    {
        return Config.ListenMode;
    }

    internal static void SetListenMode(RadioAudiobookshelfListenMode mode)
    {
        Config.ListenMode = mode;
        Save();
    }

    internal static RadioAudiobookshelfAlbumMode GetAlbumMode(string albumId)
    {
        return Config.AlbumModes.GetValueOrDefault(albumId);
    }

    internal static void SetAlbumMode(string albumId, RadioAudiobookshelfAlbumMode mode)
    {
        if (mode == RadioAudiobookshelfAlbumMode.Off) Config.AlbumModes.Remove(albumId);
        else Config.AlbumModes[albumId] = mode;
        Save();
    }

    internal static IReadOnlyList<RecentEpisode> GetUpNext()
    {
        return Config.UpNextEpisodes;
    }

    /// <summary>Adds the episode to the end of up next, or takes it out if it's already there.</summary>
    internal static void ToggleUpNext(RecentEpisode episode)
    {
        if (episode == null || string.IsNullOrEmpty(episode.EpisodeId)) return;

        int removed = Config.UpNextEpisodes.RemoveAll(picked => picked.EpisodeId == episode.EpisodeId);
        if (removed == 0) Config.UpNextEpisodes.Add(episode);
        Save();
    }

    internal static void ClearUpNext()
    {
        if (Config.UpNextEpisodes.Count == 0) return;

        Config.UpNextEpisodes.Clear();
        Save();
    }

    internal static void ExcludeEpisode(string episodeId)
    {
        if (string.IsNullOrEmpty(episodeId)) return;
        if (Config.ExcludedEpisodeIds.Add(episodeId)) Save();
    }

    internal static BingeProgress GetBinge(string showId)
    {
        return Config.BingeShows.GetValueOrDefault(showId);
    }

    internal static void SetBinge(string showId, BingeProgress binge)
    {
        Config.BingeShows[showId] = binge;
        Save();
    }

    internal static void ClearBinge(string showId)
    {
        Config.BingeShows.Remove(showId);
        Save();
    }

    /// <summary>Every show id currently pinned as a binge - used by <see cref="RadioAudiobookshelfBingeAheadModel"/>
    /// to know which AudioBookshelf shows to keep a few episodes ahead-downloaded on the server
    /// (only meaningful when <see cref="SpotiSharpBackend.StorageHandler.UseAudiobookshelfPodcasts"/> is on,
    /// since otherwise these ids are Spotify show ids instead).</summary>
    internal static IReadOnlyCollection<string> GetAllBingeShowIds()
    {
        return Config.BingeShows.Keys.ToList();
    }

    internal static void SaveConfig()
    {
        Save();
    }

    // drops radio config entries for playlists that no longer exist in spotify
    internal static void PrunePlaylists(ISet<string> livePlaylistIds)
    {
        var removed = Config.PlaylistWeights.Keys.Where(id => !livePlaylistIds.Contains(id)).ToList();
        if (removed.Count == 0) return;

        foreach (var id in removed) Config.PlaylistWeights.Remove(id);
        Save();
    }

    // drops radio config entries for albums no longer saved in the library
    internal static void PruneAlbums(ISet<string> liveAlbumIds)
    {
        var removed = Config.AlbumModes.Keys.Where(id => !liveAlbumIds.Contains(id)).ToList();
        if (removed.Count == 0) return;

        foreach (var id in removed) Config.AlbumModes.Remove(id);
        Save();
    }

    private static void MigrateLegacyToggles()
    {
        if (_config.EnabledPlaylistIds.Count == 0 && _config.EnabledShowIds.Count == 0) return;

        foreach (var id in _config.EnabledPlaylistIds) _config.PlaylistWeights.TryAdd(id, 1);
        foreach (var id in _config.EnabledShowIds) _config.ShowWeights.TryAdd(id, 1);
        _config.EnabledPlaylistIds.Clear();
        _config.EnabledShowIds.Clear();
        Save();
    }

    private static void Save()
    {
        DiskCacheHelper.Save(RADIO_CONFIG_KEY, _config);
    }
}
