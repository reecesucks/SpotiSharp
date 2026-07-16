using SpotiSharpBackend;
using SpotiSharp.Consts;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class SongsListViewModel : BaseViewModel
{
    private List<Song> _songs = new List<Song>();
    private string _latestRequestedPlaylistId;

    public List<Song> Songs
    {
        get { return _songs; }
        set { SetProperty(ref _songs, value); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    public async void OnPlayListIdRefresh(string playlistId)
    {
        _latestRequestedPlaylistId = playlistId;

        IsLoading = true;
        var songsListModel = await Task.Run(() => new SongsListModel(playlistId));
        IsLoading = false;

        if (playlistId != _latestRequestedPlaylistId) return;
        Songs = songsListModel.Songs;
    }

    public void ClickSong(object sourceItem)
    {
        if (sourceItem is not Song song) return;
        if (song.PartOfPlayListWithId == Constants.LIKED_PLALIST_ID)
        {
            APICaller.Instance?.SetCurrentPlayingToSongInLikedPlaylist(song.SongId);
        }
        else
        {
            APICaller.Instance?.SetCurrentPlayingSong(song.SongId, song.PartOfPlayListWithId);
        }
    }
}