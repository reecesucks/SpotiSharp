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

        var cached = await Task.Run(RecentEpisodesModel.GetDiskCachedEpisodesAcrossAllShows);
        if (cached != null && cached.Count > 0)
            Episodes = cached;
        else
            IsLoading = true;

        var fresh = await Task.Run(() => RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows());
        if (fresh != null && !RecentEpisodesModel.AreEpisodesEqual(Episodes, fresh))
            Episodes = fresh;
        IsLoading = false;

        if (fresh == null) _hasLoaded = false;
    }

    protected override async Task RefreshDataAsync()
    {
        var fresh = await Task.Run(() => RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows(forceRefresh: true));
        if (fresh != null && !RecentEpisodesModel.AreEpisodesEqual(Episodes, fresh))
            Episodes = fresh;
    }
}
