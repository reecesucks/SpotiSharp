using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RecentEpisodesFlatViewModel : BaseViewModel
{
    private bool _hasLoaded;

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

    public async void EnsureLoaded()
    {
        if (_hasLoaded) return;
        _hasLoaded = true;

        IsLoading = true;
        Episodes = await Task.Run(RecentEpisodesModel.GetRecentEpisodesAcrossAllShows);
        IsLoading = false;
    }
}
