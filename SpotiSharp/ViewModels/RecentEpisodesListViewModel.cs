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
        IsLoading = true;
        ShowGroups = await Task.Run(() => PlaylistListModel.SavedShows
            .Select(show => new ShowGroupViewModel(show.Id, show.Name, show.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty))
            .ToList());
        IsLoading = false;
    }
}
