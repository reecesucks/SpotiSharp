using SpotiSharp.Helpers;

namespace SpotiSharp.Models;


public static class CacheManager
{

    private const int CACHE_VERSION = 4;

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
