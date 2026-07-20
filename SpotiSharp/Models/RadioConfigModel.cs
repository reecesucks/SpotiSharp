using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

public static class RadioConfigModel
{
    private const string RADIO_CONFIG_KEY = "radioconfig";

    private static RadioConfig _config;

    internal static RadioConfig Config
    {
        get
        {
            if (_config == null)
            {
                _config = DiskCacheHelper.Load<RadioConfig>(RADIO_CONFIG_KEY) ?? new RadioConfig();
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

    internal static RadioAlbumMode GetAlbumMode(string albumId)
    {
        return Config.AlbumModes.GetValueOrDefault(albumId);
    }

    internal static void SetAlbumMode(string albumId, RadioAlbumMode mode)
    {
        if (mode == RadioAlbumMode.Off) Config.AlbumModes.Remove(albumId);
        else Config.AlbumModes[albumId] = mode;
        Save();
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
