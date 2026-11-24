using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

/// <summary>Owns the two swipeable pages of the AudioBookshelf Podcasts view - the show-grouped list
/// and the flat, all-shows recency list - mirroring <see cref="PodcastsPageViewModel"/>'s Spotify
/// counterpart. Also owns the "Unplayed" toggle shown above both pages, since it filters either one.</summary>
public class AudiobookshelfPodcastsPageViewModel : BaseViewModel
{
    public AudiobookshelfPodcastsViewModel GroupedViewModel { get; }
    public AudiobookshelfRecentEpisodesFlatViewModel FlatViewModel { get; }

    public List<object> Pages { get; }

    public ICommand ToggleUnplayedOnly { get; }

    private bool _showUnplayedOnly;
    public bool ShowUnplayedOnly
    {
        get { return _showUnplayedOnly; }
        private set { SetProperty(ref _showUnplayedOnly, value); }
    }

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

    public AudiobookshelfPodcastsPageViewModel()
    {
        GroupedViewModel = new AudiobookshelfPodcastsViewModel();
        FlatViewModel = new AudiobookshelfRecentEpisodesFlatViewModel(GroupedViewModel);
        Pages = new List<object> { GroupedViewModel, FlatViewModel };

        _showUnplayedOnly = RadioAudiobookshelfConfigModel.GetPodcastsPageUnplayedOnly();
        ToggleUnplayedOnly = new Command(ToggleUnplayedOnlyHandler);
    }

    private void ToggleUnplayedOnlyHandler()
    {
        ShowUnplayedOnly = !ShowUnplayedOnly;
        RadioAudiobookshelfConfigModel.SetPodcastsPageUnplayedOnly(ShowUnplayedOnly);

        GroupedViewModel.ApplyUnplayedOnlyFilter();
        FlatViewModel.ApplyFilter();
    }
}
