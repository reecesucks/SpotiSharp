using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class PlaylistListViewModel : BaseViewModel
{
    private Playlist _selectedPlaylist;

    public Playlist SelectedPlaylist
    {
        get { return _selectedPlaylist; }
        set { SetProperty(ref _selectedPlaylist, value); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<Playlist> _playLists = new List<Playlist>();

    public List<Playlist> PlayLists
    {
        get { return _playLists; }
        set { SetProperty(ref _playLists, value); }
    }

    public PlaylistListViewModel()
    {
        _ = LoadPlayListsAsync();
    }

    private async Task LoadPlayListsAsync()
    {
        var cached = await Task.Run(() => PlaylistListModel.CachedPlayLists);
        if (cached.Count > 0)
            PlayLists = cached;
        else
            IsLoading = true;

        bool changed = await Task.Run(PlaylistListModel.RefreshPlayLists);
        if (changed || PlayLists.Count == 0)
            PlayLists = PlaylistListModel.CachedPlayLists;
        IsLoading = false;
    }

    public async void GoToPlaylistDetail()
    {
        if (SelectedPlaylist == null) return;
        string playlistId = SelectedPlaylist.PlayListId;
        
        var navigationParameter = new Dictionary<string, object>
        {
            { "PlaylistId",  playlistId}
        };

        await Shell.Current.GoToAsync($"DetailPlaylistPage", navigationParameter);
    }
}