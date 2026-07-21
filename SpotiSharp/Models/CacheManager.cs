using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

// Coordinates wiping the content caches. Radio config (weights, binge markers,
// album modes) is always preserved.
public static class CacheManager
{
    // bump when a cache format changes (e.g. smaller image urls) so stale entries
    // are dropped and re-fetched on next launch
    private const int CACHE_VERSION = 3;

    private const string VERSION_KEY = "cacheversion";
    private const string CONFIG_KEY = "radioconfig";

    private class CacheVersion
    {
        public int Version { get; set; }
    }

    public static void MigrateIfNeeded()
    {
        var stored = DiskCacheHelper.Load<CacheVersion>(VERSION_KEY)?.Version ?? 0;
        if (stored >= CACHE_VERSION) return;

        DiskCacheHelper.ClearAllExcept(CONFIG_KEY);
        DiskCacheHelper.Save(VERSION_KEY, new CacheVersion { Version = CACHE_VERSION });
    }

    // debug/maintenance: wipe cached content (disk + in-memory) but keep radio settings
    public static void ClearContentCaches()
    {
        DiskCacheHelper.ClearAllExcept(CONFIG_KEY, VERSION_KEY);

        PlaylistListModel.ClearMemory();
        ArtistListModel.ClearMemory();
        SavedAlbumsModel.ClearMemory();
        ArtistAlbumsModel.ClearMemory();
        AlbumSongsModel.ClearMemory();
        RecentEpisodesModel.ClearMemory();
        RotationTracksModel.ClearMemory();
    }
}
