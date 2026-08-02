using System.Windows.Input;
using SpotifyAPI.Web;
using SpotiSharp.Helpers;
using SpotiSharp.Keypad;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RecentEpisodesListViewModel : BaseViewModel
{
    public IKeypadSection? Section { get; set; }

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

    public ICommand ToggleGroupExpanded { get; }

    public RecentEpisodesListViewModel()
    {
        ToggleGroupExpanded = new Command<ShowGroupViewModel>(group => group?.ToggleExpanded.Execute(null));

        IsLoading = true;
        Application.Current?.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(350), () => _ = LoadShowGroupsAsync());
    }

    private async Task LoadShowGroupsAsync()
    {
        var cached = await Task.Run(() => PlaylistListModel.CachedSavedShows);
        if (cached.Count > 0)
        {
            ShowGroups = ToShowGroups(cached);
            IsLoading = false;
        }

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
