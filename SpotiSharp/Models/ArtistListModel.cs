using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class ArtistListModel
{
    private const string ARTIST_CACHE_KEY = "artists";

    private static List<Artist> _artists;

    internal static List<Artist> CachedArtists
    {
        get
        {
            _artists ??= DiskCacheHelper.Load<List<Artist>>(ARTIST_CACHE_KEY);
            return _artists ?? new List<Artist>();
        }
    }

    internal static bool RefreshArtists()
    {
        var fetched = FetchArtists();
        if (fetched == null) return false;

        bool changed = _artists == null || !AreArtistsEqual(_artists, fetched);
        _artists = fetched;
        if (changed) DiskCacheHelper.Save(ARTIST_CACHE_KEY, fetched);
        return changed;
    }

    private static List<Artist> FetchArtists()
    {
        var followedArtists = APICaller.Instance?.GetFollowedArtists();
        if (followedArtists == null) return null;

        return followedArtists
            .Select(artist => new Artist(artist.Id, artist.Name, ImageHelper.Thumbnail(artist.Images)))
            .OrderBy(artist => artist.ArtistName)
            .ToList();
    }

    private static bool AreArtistsEqual(List<Artist> current, List<Artist> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) =>
            a.ArtistId == b.ArtistId &&
            a.ArtistName == b.ArtistName &&
            a.ArtistImageUrl == b.ArtistImageUrl).All(equal => equal);
    }
}
