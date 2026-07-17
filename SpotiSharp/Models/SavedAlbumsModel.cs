using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class SavedAlbumsModel
{
    private const string SAVED_ALBUM_CACHE_KEY = "savedalbums";

    private static List<Album> _albums;

    internal static List<Album> CachedAlbums
    {
        get
        {
            _albums ??= DiskCacheHelper.Load<List<Album>>(SAVED_ALBUM_CACHE_KEY);
            return _albums ?? new List<Album>();
        }
    }

    internal static bool RefreshAlbums()
    {
        var fetched = FetchAlbums();
        if (fetched == null) return false;

        bool changed = _albums == null || !ArtistAlbumsModel.AreAlbumsEqual(_albums, fetched);
        _albums = fetched;
        if (changed) DiskCacheHelper.Save(SAVED_ALBUM_CACHE_KEY, fetched);
        return changed;
    }

    private static List<Album> FetchAlbums()
    {
        var savedAlbums = APICaller.Instance?.GetSavedAlbums();
        if (savedAlbums == null) return null;

        return savedAlbums
            .OrderByDescending(savedAlbum => savedAlbum.AddedAt)
            .Select(savedAlbum => new Album(
                savedAlbum.Album.Id,
                savedAlbum.Album.Name,
                savedAlbum.Album.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty,
                savedAlbum.Album.ReleaseDate ?? string.Empty,
                string.Join(", ", savedAlbum.Album.Artists?.Select(artist => artist.Name) ?? Enumerable.Empty<string>())))
            .ToList();
    }
}
