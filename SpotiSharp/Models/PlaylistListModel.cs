using SpotifyAPI.Web;
using SpotiSharpBackend;
using Constants = SpotiSharp.Consts.Constants;

namespace SpotiSharp.Models;

public class PlaylistListModel
{
    private static List<Playlist> _playLists = new List<Playlist>();
    
    public static List<Playlist> PlayLists
    {
        get
        {
            LoadPlaylist();
            return _playLists;
        }
        private set => _playLists = value;
    }

    public PlaylistListModel()
    {
        LoadPlaylist();
    }

    internal static void LoadPlaylist()
    {
        var tmpPlaylist = new List<Playlist>();
        
        // liked playlist
        int? likedSongsAmount = APICaller.Instance?.GetUserLikedSongsAmount();
        tmpPlaylist.Add(new Playlist(Constants.LIKED_PLALIST_ID, Constants.LIKED_PLALIST_IMAGE_URL, "Liked Songs", likedSongsAmount ?? 0));
        
        // followed playlists
        var userPlaylists = APICaller.Instance?.GetAllUserPlaylists();
        if (userPlaylists == null) return;
        foreach (var playlist in userPlaylists)
        {
            tmpPlaylist.Add(new Playlist(playlist.Id, playlist.Images.ElementAtOrDefault(0)?.Url ?? string.Empty, playlist.Name, playlist.Tracks.Total ?? 0));
        }
        _playLists = tmpPlaylist;
    }

    private static List<FullShow> _savedShows = new List<FullShow>();

    public static List<FullShow> SavedShows
    {
        get
        {
            LoadSavedShows();
            return _savedShows;
        }
        private set => _savedShows = value;
    }

    internal static void LoadSavedShows()
    {
        var tmpSavedShowList = new List<FullShow>();

        // followed playlists
        var savedShows = APICaller.Instance?.GetSavedShows();
        if (savedShows == null) return;
        foreach (var show in savedShows)
        {
            tmpSavedShowList.Add(show.Show);
        }
        _savedShows = tmpSavedShowList;
    }
}