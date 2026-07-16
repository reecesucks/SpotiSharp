using SpotifyAPI.Web;
using SpotiSharp.Helpers;
using SpotiSharpBackend;
using Constants = SpotiSharp.Consts.Constants;

namespace SpotiSharp.Models;

public class PlaylistListModel
{
    private const string PLAYLIST_CACHE_KEY = "playlists";
    private const string SAVED_SHOW_CACHE_KEY = "savedshows";

    private static List<Playlist> _playLists;

    public static List<Playlist> PlayLists
    {
        get
        {
            _playLists ??= DiskCacheHelper.Load<List<Playlist>>(PLAYLIST_CACHE_KEY);
            if (_playLists == null) RefreshPlayLists();
            return _playLists ?? new List<Playlist>();
        }
    }

    internal static List<Playlist> CachedPlayLists
    {
        get
        {
            _playLists ??= DiskCacheHelper.Load<List<Playlist>>(PLAYLIST_CACHE_KEY);
            return _playLists ?? new List<Playlist>();
        }
    }

    internal static bool RefreshPlayLists()
    {
        var fetched = FetchPlayLists();
        if (fetched == null) return false;

        bool changed = _playLists == null || !ArePlayListsEqual(_playLists, fetched);
        _playLists = fetched;
        if (changed) DiskCacheHelper.Save(PLAYLIST_CACHE_KEY, fetched);
        return changed;
    }

    private static List<Playlist> FetchPlayLists()
    {
        var userPlaylists = APICaller.Instance?.GetAllUserPlaylists();
        if (userPlaylists == null) return null;

        var tmpPlaylist = new List<Playlist>();

        // liked playlist
        int? likedSongsAmount = APICaller.Instance?.GetUserLikedSongsAmount();
        tmpPlaylist.Add(new Playlist(Constants.LIKED_PLALIST_ID, Constants.LIKED_PLALIST_IMAGE_URL, "Liked Songs", likedSongsAmount ?? 0));

        // followed playlists
        foreach (var playlist in userPlaylists)
        {
            tmpPlaylist.Add(new Playlist(playlist.Id, playlist.Images.ElementAtOrDefault(0)?.Url ?? string.Empty, playlist.Name, playlist.Tracks.Total ?? 0));
        }
        return tmpPlaylist;
    }

    private static bool ArePlayListsEqual(List<Playlist> current, List<Playlist> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) =>
            a.PlayListId == b.PlayListId &&
            a.PlayListImageURL == b.PlayListImageURL &&
            a.PlayListTitle == b.PlayListTitle &&
            a.SongAmount == b.SongAmount).All(equal => equal);
    }

    private static List<FullShow> _savedShows;

    public static List<FullShow> SavedShows
    {
        get
        {
            _savedShows ??= DiskCacheHelper.Load<List<FullShow>>(SAVED_SHOW_CACHE_KEY);
            if (_savedShows == null) RefreshSavedShows();
            return _savedShows ?? new List<FullShow>();
        }
    }

    internal static List<FullShow> CachedSavedShows
    {
        get
        {
            _savedShows ??= DiskCacheHelper.Load<List<FullShow>>(SAVED_SHOW_CACHE_KEY);
            return _savedShows ?? new List<FullShow>();
        }
    }

    internal static bool RefreshSavedShows()
    {
        var fetched = FetchSavedShows();
        if (fetched == null) return false;

        if (fetched.Count == 0 && _savedShows?.Count > 0) return false;

        bool changed = _savedShows == null || !AreSavedShowsEqual(_savedShows, fetched);
        _savedShows = fetched;
        if (changed) DiskCacheHelper.Save(SAVED_SHOW_CACHE_KEY, fetched);
        return changed;
    }

    private static List<FullShow> FetchSavedShows()
    {
        var savedShows = APICaller.Instance?.GetSavedShows();
        if (savedShows == null) return null;

        return savedShows.Select(show => show.Show).ToList();
    }

    private static bool AreSavedShowsEqual(List<FullShow> current, List<FullShow> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) =>
            a.Id == b.Id &&
            a.Name == b.Name &&
            (a.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty) == (b.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty)).All(equal => equal);
    }
}