using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

public sealed record LastPlayedSnapshot(string Uri, string Title, string ImageUrl, int ProgressMs, bool IsTrack);

public static class LastPlayedModel
{
    private const string LAST_PLAYED_CACHE_KEY = "lastplayed";

    public static LastPlayedSnapshot Load() => DiskCacheHelper.Load<LastPlayedSnapshot>(LAST_PLAYED_CACHE_KEY);

    public static void Save(LastPlayedSnapshot snapshot) => DiskCacheHelper.Save(LAST_PLAYED_CACHE_KEY, snapshot);
}
