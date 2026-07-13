using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RecentEpisodesListViewModel : BaseViewModel
{
    private List<RecentEpisode> _recentEpisodes;

    public List<RecentEpisode> RecentEpisodes
    {
        get { return _recentEpisodes; }
        set { SetProperty(ref _recentEpisodes, value); }
    }

    public RecentEpisodesListViewModel()
    {
        RecentEpisodes = RecentEpisodesModel.RecentEpisodes;
    }
}
