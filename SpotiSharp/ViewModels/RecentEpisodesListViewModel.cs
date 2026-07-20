using SpotifyAPI.Web;
using SpotiSharp.Helpers;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RecentEpisodesListViewModel : BaseViewModel
{
    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<ShowGroupViewModel> _showGroups = new List<ShowGroupViewModel>();
    public List<ShowGroupViewModel> ShowGroups
    {
        get { return _showGroups; }
        private set { SetProperty(ref _showGroups, value); }
    }

    public RecentEpisodesListViewModel()
    {
        _ = LoadShowGroupsAsync();
    }

    private async Task LoadShowGroupsAsync()
    {
        var cached = await Task.Run(() => PlaylistListModel.CachedSavedShows);
        if (cached.Count > 0)
            ShowGroups = ToShowGroups(cached);
        else
            IsLoading = true;

        await RefreshDataAsync();
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        bool changed = await Task.Run(PlaylistListModel.RefreshSavedShows);
        if (changed || ShowGroups.Count == 0)
            ShowGroups = ToShowGroups(PlaylistListModel.CachedSavedShows);
    }

    private static List<ShowGroupViewModel> ToShowGroups(List<FullShow> shows)
    {
        return shows
            .Select(show => new ShowGroupViewModel(show.Id, show.Name, ImageHelper.Thumbnail(show.Images)))
            .ToList();
    }
}
