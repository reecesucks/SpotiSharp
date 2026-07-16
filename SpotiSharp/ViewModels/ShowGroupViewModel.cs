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

    private bool _hasRevalidated;

    private async void ToggleExpandedHandler()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            return;
        }

        IsExpanded = true;
        if (_hasRevalidated) return;
        _hasRevalidated = true;

        var session = RecentEpisodesModel.GetSessionEpisodesForShow(ShowId);
        if (session != null)
        {
            Episodes = session;
            return;
        }

        var cached = await Task.Run(() => RecentEpisodesModel.GetDiskCachedEpisodesForShow(ShowId));
        if (cached != null)
            Episodes = cached;
        else
            IsLoading = true;

        var fresh = await Task.Run(() => RecentEpisodesModel.RefreshEpisodesForShow(ShowId, ShowName, ShowImageUrl));
        if (fresh != null && !RecentEpisodesModel.AreEpisodesEqual(Episodes, fresh))
            Episodes = fresh;
        IsLoading = false;

        if (fresh == null) _hasRevalidated = false;
    }
}
