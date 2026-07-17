using System.Collections.Concurrent;
using SpotifyAPI.Web;
using SpotiSharp.Helpers;
using SpotiSharpBackend;
using Constants = SpotiSharp.Consts.Constants;

namespace SpotiSharp.Models;

public class RotationTracksModel
{
    private static readonly ConcurrentDictionary<string, List<RadioSong>> _tracksByPlaylistId = new();

    internal static List<RadioSong> GetTracks(string playlistId)
    {
        if (_tracksByPlaylistId.TryGetValue(playlistId, out var session)) return session;

        var cached = DiskCacheHelper.Load<List<RadioSong>>(CacheKey(playlistId));
        if (cached != null)
        {
            _tracksByPlaylistId[playlistId] = cached;
            return cached;
        }

        return RefreshTracks(playlistId);
    }

    internal static List<RadioSong> RefreshTracks(string playlistId)
    {
        var tracks = FetchTracks(playlistId);
        if (tracks == null) return null;

        var fetched = tracks
            .Select(track => new RadioSong(
                track.Uri,
                track.Name,
                string.Join(", ", track.Artists.Select(artist => artist.Name)),
                track.Album?.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty))
            .ToList();

        _tracksByPlaylistId[playlistId] = fetched;
        DiskCacheHelper.Save(CacheKey(playlistId), fetched);
        return fetched;
    }

    private static List<FullTrack> FetchTracks(string playlistId)
    {
        if (playlistId == Constants.LIKED_PLALIST_ID)
            return APICaller.Instance?.GetUserLikedSongs()?.Select(savedTrack => savedTrack.Track).Where(track => track != null).ToList();

        var tracks = APICaller.Instance?.GetTracksByPlaylistId(playlistId);
        return tracks?.Select(playlistTrack => playlistTrack.Track).OfType<FullTrack>().ToList();
    }

    internal static void Invalidate(string playlistId)
    {
        _tracksByPlaylistId.TryRemove(playlistId, out _);
        DiskCacheHelper.Delete(CacheKey(playlistId));
    }

    private static string CacheKey(string playlistId)
    {
        return "playlisttracks_" + playlistId;
    }
}
