using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class ArtistListViewModel : BaseViewModel
{
    private Artist _selectedArtist;

    public Artist SelectedArtist
    {
        get { return _selectedArtist; }
        set { SetProperty(ref _selectedArtist, value); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<Artist> _artists = new List<Artist>();

    public List<Artist> Artists
    {
        get { return _artists; }
        set { SetProperty(ref _artists, value); }
    }

    public ArtistListViewModel()
    {
        IsLoading = true;
        Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(350), () => _ = LoadArtistsAsync());
    }

    private async Task LoadArtistsAsync()
    {
        var cached = await Task.Run(() => ArtistListModel.CachedArtists);
        if (cached.Count > 0)
        {
            Artists = cached;
            IsLoading = false;
        }

        await RefreshDataAsync();
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        bool changed = await Task.Run(ArtistListModel.RefreshArtists);
        if (changed || Artists.Count == 0)
            Artists = ArtistListModel.CachedArtists;
    }

    public async void GoToArtistDetail()
    {
        if (SelectedArtist == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "ArtistId", SelectedArtist.ArtistId },
            { "ArtistName", SelectedArtist.ArtistName }
        };

        await Shell.Current.GoToAsync("DetailArtistPage", navigationParameter);
    }
}
