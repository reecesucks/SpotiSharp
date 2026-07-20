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

    private static List<RadioAlbumToggleViewModel> LoadAlbumToggles()
    {
        // cache first, the api is only asked when nothing is cached yet
        var albums = SavedAlbumsModel.CachedAlbums;
        if (albums.Count == 0)
        {
            SavedAlbumsModel.RefreshAlbums();
            albums = SavedAlbumsModel.CachedAlbums;
        }

        return albums
            .Select(album => new RadioAlbumToggleViewModel(
                album.AlbumId,
                album.AlbumName,
                album.ArtistNames,
                album.AlbumImageUrl,
                RadioConfigModel.GetAlbumMode(album.AlbumId)))
            .ToList();
    }
}
