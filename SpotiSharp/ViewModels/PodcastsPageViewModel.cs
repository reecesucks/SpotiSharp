namespace SpotiSharp.ViewModels;

public class PodcastsPageViewModel : BaseViewModel
{
    public RecentEpisodesListViewModel GroupedViewModel { get; }
    public RecentEpisodesFlatViewModel FlatViewModel { get; }

    public List<object> Pages { get; }

    private int _currentPageIndex;
    public int CurrentPageIndex
    {
        get { return _currentPageIndex; }
        set
        {
            if (SetProperty(ref _currentPageIndex, value) && value == 1)
            {
                FlatViewModel.EnsureLoaded();
            }
        }
    }

    public PodcastsPageViewModel()
    {
        GroupedViewModel = new RecentEpisodesListViewModel();
        FlatViewModel = new RecentEpisodesFlatViewModel();
        Pages = new List<object> { GroupedViewModel, FlatViewModel };
    }
}
