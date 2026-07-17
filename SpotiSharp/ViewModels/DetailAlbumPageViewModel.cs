using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels;

public class DetailAlbumPageViewModel : BaseViewModel
{
    public ICommand GoBack { get; }
    public ICommand ToggleAlbumSaved { get; }

    public DetailAlbumPageViewModel()
    {
        GoBack = new Command(async () => await Shell.Current.GoToAsync(".."));
        ToggleAlbumSaved = new Command(ToggleAlbumSavedFunc);
    }

    private bool _isAlbumSaved;

    public bool IsAlbumSaved
    {
        get { return _isAlbumSaved; }
        private set { SetProperty(ref _isAlbumSaved, value); }
    }

    private void ToggleAlbumSavedFunc()
    {
        var albumId = AlbumId;
        if (string.IsNullOrEmpty(albumId)) return;

        bool newState = !IsAlbumSaved;
        IsAlbumSaved = newState;

        Task.Run(() =>
        {
            bool success = newState
                ? APICaller.Instance?.SaveAlbum(albumId) ?? false
                : APICaller.Instance?.RemoveSavedAlbum(albumId) ?? false;
            if (!success && albumId == AlbumId) IsAlbumSaved = !newState;
        });
    }

    private async Task LoadSavedStateAsync(string albumId)
    {
        var saved = await Task.Run(() => APICaller.Instance?.IsAlbumSaved(albumId));
        if (albumId != AlbumId) return;
        if (saved.HasValue) IsAlbumSaved = saved.Value;
    }

    private string _albumId;

    public string AlbumId
    {
        get { return _albumId; }
        set
        {
            SetProperty(ref _albumId, value);
            _ = LoadSongsAsync();
        }
    }

    private string _albumName;

    public string AlbumName
    {
        get { return _albumName; }
        set { SetProperty(ref _albumName, value); }
    }

    private string _albumImageUrl;

    public string AlbumImageUrl
    {
        get { return _albumImageUrl; }
        set { SetProperty(ref _albumImageUrl, value); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<AlbumSong> _songs = new List<AlbumSong>();

    public List<AlbumSong> Songs
    {
        get { return _songs; }
        set { SetProperty(ref _songs, value); }
    }

    private async Task LoadSongsAsync()
    {
        var albumId = AlbumId;
        if (string.IsNullOrEmpty(albumId)) return;

        _ = LoadSavedStateAsync(albumId);

        var session = AlbumSongsModel.GetSessionSongs(albumId);
        if (session != null)
        {
            Songs = session;
            return;
        }

        var cached = await Task.Run(() => AlbumSongsModel.GetDiskCachedSongs(albumId));
        if (albumId != AlbumId) return;
        if (cached != null)
            Songs = cached;
        else
            IsLoading = true;

        var fresh = await Task.Run(() => AlbumSongsModel.RefreshSongs(albumId));
        if (albumId != AlbumId) return;
        if (fresh != null && !AlbumSongsModel.AreSongsEqual(Songs, fresh))
            Songs = fresh;
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        var albumId = AlbumId;
        if (string.IsNullOrEmpty(albumId)) return;

        var fresh = await Task.Run(() => AlbumSongsModel.RefreshSongs(albumId));
        if (albumId != AlbumId) return;
        if (fresh != null && !AlbumSongsModel.AreSongsEqual(Songs, fresh))
            Songs = fresh;
    }

    public async void ClickSong(object sourceItem)
    {
        if (sourceItem is not AlbumSong song) return;

        if (PlaybackStateStore.Instance.HasActiveDevice)
        {
            bool started = await Task.Run(() => APICaller.Instance?.SetCurrentPlayingSongInAlbum(song.SongUri, AlbumId) ?? false);
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
