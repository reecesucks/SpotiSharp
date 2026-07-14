using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class ShowGroupViewModel : BaseViewModel
{
    public string ShowId { get; }
    public string ShowName { get; }
    public string ShowImageUrl { get; }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get { return _isExpanded; }
        private set { SetProperty(ref _isExpanded, value); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<RecentEpisode> _episodes = new List<RecentEpisode>();
    public List<RecentEpisode> Episodes
    {
        get { return _episodes; }
        private set { SetProperty(ref _episodes, value); }
    }

    public ICommand ToggleExpanded { get; }

    public ShowGroupViewModel(string showId, string showName, string showImageUrl)
    {
        ShowId = showId;
        ShowName = showName;
        ShowImageUrl = showImageUrl;
        ToggleExpanded = new Command(ToggleExpandedHandler);
    }

    private async void ToggleExpandedHandler()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            return;
        }

        IsExpanded = true;
        if (Episodes.Count == 0)
        {
            IsLoading = true;
            Episodes = await Task.Run(() => RecentEpisodesModel.GetRecentEpisodesForShow(ShowId, ShowName, ShowImageUrl));
            IsLoading = false;
        }
    }
}
