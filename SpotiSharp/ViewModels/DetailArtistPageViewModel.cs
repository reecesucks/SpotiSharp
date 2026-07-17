using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class DetailArtistPageViewModel : BaseViewModel
{
    public ICommand GoBack { get; }

    public DetailArtistPageViewModel()
    {
        GoBack = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    private string _artistId;

    public string ArtistId
    {
        get { return _artistId; }
        set
        {
            SetProperty(ref _artistId, value);
            _ = LoadAlbumsAsync();
        }
    }

    private string _artistName;

    public string ArtistName
    {
        get { return _artistName; }
        set { SetProperty(ref _artistName, value); }
    }

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

    private async Task LoadAlbumsAsync()
    {
        var artistId = ArtistId;
        if (string.IsNullOrEmpty(artistId)) return;

        var session = ArtistAlbumsModel.GetSessionAlbums(artistId);
        if (session != null)
        {
            Albums = session;
            return;
        }

        var cached = await Task.Run(() => ArtistAlbumsModel.GetDiskCachedAlbums(artistId));
        if (artistId != ArtistId) return;
        if (cached != null)
            Albums = cached;
        else
            IsLoading = true;

        var fresh = await Task.Run(() => ArtistAlbumsModel.RefreshAlbums(artistId));
        if (artistId != ArtistId) return;
        if (fresh != null && !ArtistAlbumsModel.AreAlbumsEqual(Albums, fresh))
            Albums = fresh;
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        var artistId = ArtistId;
        if (string.IsNullOrEmpty(artistId)) return;

        var fresh = await Task.Run(() => ArtistAlbumsModel.RefreshAlbums(artistId));
        if (artistId != ArtistId) return;
        if (fresh != null && !ArtistAlbumsModel.AreAlbumsEqual(Albums, fresh))
            Albums = fresh;
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
