using System.Collections.Concurrent;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class ArtistAlbumsModel
{
    private static readonly ConcurrentDictionary<string, List<Album>> _albumsByArtistId = new();

    internal static List<Album> GetSessionAlbums(string artistId)
    {
        return _albumsByArtistId.TryGetValue(artistId, out var albums) ? albums : null;
    }

    internal static List<Album> GetDiskCachedAlbums(string artistId)
    {
        return DiskCacheHelper.Load<List<Album>>(AlbumCacheKey(artistId));
    }

    internal static List<Album> RefreshAlbums(string artistId)
    {
        var albums = APICaller.Instance?.GetArtistAlbums(artistId);
        if (albums == null) return null;

        var fetched = albums
            .Select(album => new Album(
                album.Id,
                album.Name,
                album.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty,
                album.ReleaseDate ?? string.Empty,
                string.Join(", ", album.Artists?.Select(artist => artist.Name) ?? Enumerable.Empty<string>())))
            .OrderByDescending(album => album.ReleaseDate)
            .ToList();

        _albumsByArtistId[artistId] = fetched;
        DiskCacheHelper.Save(AlbumCacheKey(artistId), fetched);
        return fetched;
    }

    internal static bool AreAlbumsEqual(List<Album> current, List<Album> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) =>
            a.AlbumId == b.AlbumId &&
            a.AlbumName == b.AlbumName &&
            a.AlbumImageUrl == b.AlbumImageUrl).All(equal => equal);
    }

    private static string AlbumCacheKey(string artistId)
    {
        return "artistalbums_" + artistId;
    }
}
