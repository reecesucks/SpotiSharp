using System.Collections.Concurrent;
using SpotifyAPI.Web;
using SpotiSharp.Helpers;
using SpotiSharpBackend;
using Constants = SpotiSharp.Consts.Constants;

namespace SpotiSharp.Models;

public class RotationTracksModel
{
    private const string CACHE_KEY_PREFIX = "playlisttracks_";

    private static readonly ConcurrentDictionary<string, List<RadioSong>> _tracksByPlaylistId = new();

    internal static void ClearMemory()
    {
        _tracksByPlaylistId.Clear();
    }

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
                ImageHelper.Thumbnail(track.Album?.Images)))
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

    internal static void PruneExcept(ISet<string> livePlaylistIds)
    {
        foreach (var key in DiskCacheHelper.ListKeys(CACHE_KEY_PREFIX))
        {
            var playlistId = key.Substring(CACHE_KEY_PREFIX.Length);
            if (!livePlaylistIds.Contains(playlistId))
            {
                _tracksByPlaylistId.TryRemove(playlistId, out _);
                DiskCacheHelper.Delete(key);
            }
        }

        foreach (var playlistId in _tracksByPlaylistId.Keys.ToList())
        {
            if (!livePlaylistIds.Contains(playlistId)) _tracksByPlaylistId.TryRemove(playlistId, out _);
        }
    }

    private static string CacheKey(string playlistId)
    {
        return CACHE_KEY_PREFIX + playlistId;
    }
}
