using System.Collections.Concurrent;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class AlbumSongsModel
{
    private static readonly ConcurrentDictionary<string, List<AlbumSong>> _songsByAlbumId = new();

    internal static List<AlbumSong> GetSessionSongs(string albumId)
    {
        return _songsByAlbumId.TryGetValue(albumId, out var songs) ? songs : null;
    }

    internal static List<AlbumSong> GetDiskCachedSongs(string albumId)
    {
        return DiskCacheHelper.Load<List<AlbumSong>>(SongCacheKey(albumId));
    }

    internal static List<AlbumSong> RefreshSongs(string albumId)
    {
        var tracks = APICaller.Instance?.GetAlbumTracks(albumId);
        if (tracks == null) return null;

        var fetched = tracks
            .Select(track => new AlbumSong(
                track.Id,
                track.Uri,
                track.Name,
                string.Join(", ", track.Artists.Select(artist => artist.Name)),
                track.TrackNumber))
            .ToList();

        _songsByAlbumId[albumId] = fetched;
        DiskCacheHelper.Save(SongCacheKey(albumId), fetched);
        return fetched;
    }

    internal static bool AreSongsEqual(List<AlbumSong> current, List<AlbumSong> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) =>
            a.SongId == b.SongId &&
            a.SongTitle == b.SongTitle).All(equal => equal);
    }

    private static string SongCacheKey(string albumId)
    {
        return "albumtracks_" + albumId;
    }
}
