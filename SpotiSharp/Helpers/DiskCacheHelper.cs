using System.Text.Json;

namespace SpotiSharp.Helpers;

public static class DiskCacheHelper
{
    private static readonly string CacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "apicache");

    public static T Load<T>(string key) where T : class
    {
        try
        {
            var path = GetPath(key);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch
        {
            // a missing or corrupt cache file is just a cache miss
            return null;
        }
    }

    public static void Save<T>(string key, T value)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllText(GetPath(key), JsonSerializer.Serialize(value));
        }
        catch
        {
            // failing to persist the cache must never break the app
        }
    }

    public static void Delete(string key)
    {
        try
        {
            File.Delete(GetPath(key));
        }
        catch
        {
            // a cache file that cannot be deleted is refreshed on next fetch anyway
        }
    }

    // all cache keys beginning with prefix, e.g. every per-playlist track cache
    public static IReadOnlyList<string> ListKeys(string prefix)
    {
        try
        {
            if (!Directory.Exists(CacheDirectory)) return Array.Empty<string>();
            return Directory.GetFiles(CacheDirectory, prefix + "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string GetPath(string key)
    {
        return Path.Combine(CacheDirectory, key + ".json");
    }
}
