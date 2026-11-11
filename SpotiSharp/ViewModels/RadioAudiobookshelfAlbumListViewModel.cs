using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfAlbumListViewModel : BaseViewModel
{
    public string Title => "Albums";

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<RadioAudiobookshelfAlbumToggleViewModel> _items = new List<RadioAudiobookshelfAlbumToggleViewModel>();

    public List<RadioAudiobookshelfAlbumToggleViewModel> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    private RadioAudiobookshelfAlbumToggleViewModel _selectedItem;

    public RadioAudiobookshelfAlbumToggleViewModel SelectedItem
    {
        get { return _selectedItem; }
        set { SetProperty(ref _selectedItem, value); }
    }

    public RadioAudiobookshelfAlbumListViewModel()
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

    private static List<RadioAudiobookshelfAlbumToggleViewModel> LoadAlbumToggles()
    {
        // cache first, the api is only asked when nothing is cached yet
        if (SavedAlbumsModel.CachedAlbums.Count == 0) SavedAlbumsModel.RefreshAlbums();
        return BuildToggles();
    }

    private static List<RadioAudiobookshelfAlbumToggleViewModel> BuildToggles()
    {
        return SavedAlbumsModel.CachedAlbums
            .Select(album => new RadioAudiobookshelfAlbumToggleViewModel(
                album.AlbumId,
                album.AlbumName,
                album.ArtistNames,
                album.AlbumImageUrl,
                RadioAudiobookshelfConfigModel.GetAlbumMode(album.AlbumId)))
            .ToList();
    }
}
