using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioAlbumListViewModel : BaseViewModel
{
    public string Title => "Albums";

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<RadioAlbumToggleViewModel> _items = new List<RadioAlbumToggleViewModel>();

    public List<RadioAlbumToggleViewModel> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    public RadioAlbumListViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        Items = await Task.Run(LoadAlbumToggles);
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        // pull-to-refresh forces the api, then rebuilds the rows from fresh data
        Items = await Task.Run(() =>
        {
            SavedAlbumsModel.RefreshAlbums();
            return BuildToggles();
        });
    }

    private static List<RadioAlbumToggleViewModel> LoadAlbumToggles()
    {
        // cache first, the api is only asked when nothing is cached yet
        if (SavedAlbumsModel.CachedAlbums.Count == 0) SavedAlbumsModel.RefreshAlbums();
        return BuildToggles();
    }

    private static List<RadioAlbumToggleViewModel> BuildToggles()
    {
        return SavedAlbumsModel.CachedAlbums
            .Select(album => new RadioAlbumToggleViewModel(
                album.AlbumId,
                album.AlbumName,
                album.ArtistNames,
                album.AlbumImageUrl,
                RadioConfigModel.GetAlbumMode(album.AlbumId)))
            .ToList();
    }
}
