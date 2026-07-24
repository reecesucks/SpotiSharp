using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class AlbumListViewModel : BaseViewModel
{
    private Album _selectedAlbum;

    public Album SelectedAlbum
    {
        get { return _selectedAlbum; }
        set { SetProperty(ref _selectedAlbum, value); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<Album> _albums = new List<Album>();

    public List<Album> Albums
    {
        get { return _albums; }
        set { SetProperty(ref _albums, value); }
    }

    public AlbumListViewModel()
    {
        IsLoading = true;
        Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(350), () => _ = LoadAlbumsAsync());
    }

    private async Task LoadAlbumsAsync()
    {
        var cached = await Task.Run(() => SavedAlbumsModel.CachedAlbums);
        if (cached.Count > 0)
        {
            Albums = cached;
            IsLoading = false;
        }

        await RefreshDataAsync();
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        bool changed = await Task.Run(SavedAlbumsModel.RefreshAlbums);
        if (changed || Albums.Count == 0)
            Albums = SavedAlbumsModel.CachedAlbums;
    }

    public async void GoToAlbumDetail()
    {
        if (SelectedAlbum == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "AlbumId", SelectedAlbum.AlbumId },
            { "AlbumName", SelectedAlbum.AlbumName },
            { "AlbumImageUrl", SelectedAlbum.AlbumImageUrl }
        };

        await Shell.Current.GoToAsync("DetailAlbumPage", navigationParameter);
    }
}
