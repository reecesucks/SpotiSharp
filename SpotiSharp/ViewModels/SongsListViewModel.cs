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
        await LoadSongsAsync(playlistId);
    }

    private async Task LoadSongsAsync(string playlistId)
    {
        _latestRequestedPlaylistId = playlistId;

        IsLoading = true;
        var songsListModel = await Task.Run(() => new SongsListModel(playlistId));
        IsLoading = false;

        if (playlistId != _latestRequestedPlaylistId) return;
        Songs = songsListModel.Songs;
    }

    protected override async Task RefreshDataAsync()
    {
        if (string.IsNullOrEmpty(_latestRequestedPlaylistId)) return;
        await LoadSongsAsync(_latestRequestedPlaylistId);
    }

    public async void ClickSong(object sourceItem)
    {
        if (sourceItem is not Song song) return;


        if (PlaybackStateStore.Instance.HasActiveDevice)
        {
            bool started = await Task.Run(() => song.PartOfPlayListWithId == Constants.LIKED_PLALIST_ID
                ? APICaller.Instance?.SetCurrentPlayingToSongInLikedPlaylist(song.SongId) ?? false
                : APICaller.Instance?.SetCurrentPlayingSong(song.SongUri, song.PartOfPlayListWithId) ?? false);

            if (started) return;
        }

        if (!await LaunchInSpotify(song.SongUri))
        {
            await Shell.Current.DisplayAlert("Playback failed", "Couldn't start playback. Make sure Spotify is installed and you're signed in.", "OK");
        }
    }

    private static async Task<bool> LaunchInSpotify(string songUri)
    {
        if (string.IsNullOrEmpty(songUri)) return false;
        try
        {
            return await Launcher.Default.TryOpenAsync(songUri);
        }
        catch
        {
            return false;
        }
    }
}