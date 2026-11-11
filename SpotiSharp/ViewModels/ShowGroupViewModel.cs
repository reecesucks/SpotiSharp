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

    private const int LOAD_MORE_PAGE_SIZE = 10;
    private int _moreOffset;

    private bool _isLoadingMore;
    public bool IsLoadingMore
    {
        get { return _isLoadingMore; }
        private set { SetProperty(ref _isLoadingMore, value); }
    }

    private bool _canLoadMore;
    public bool CanLoadMore
    {
        get { return _canLoadMore; }
        private set { SetProperty(ref _canLoadMore, value); }
    }

    public ICommand ToggleExpanded { get; }
    public ICommand LoadMore { get; }

    public ShowGroupViewModel(string showId, string showName, string showImageUrl)
    {
        ShowId = showId;
        ShowName = showName;
        ShowImageUrl = showImageUrl;
        ToggleExpanded = new Command(ToggleExpandedHandler);
        LoadMore = new Command(async () => await LoadMoreHandler());
    }

    private async Task LoadMoreHandler()
    {
        if (_isLoadingMore) return;
        IsLoadingMore = true;

        var (more, hasMore) = await Task.Run(() =>
            RecentEpisodesModel.FetchMoreEpisodesForShow(ShowId, ShowName, ShowImageUrl, _moreOffset, LOAD_MORE_PAGE_SIZE));

        // The already-shown episodes came from a separate, uncapped-order fetch - skip anything
        // "load more" turns up again so the list never shows the same episode twice.
        var knownIds = Episodes.Select(episode => episode.EpisodeId).ToHashSet();
        var appended = more.Where(episode => !knownIds.Contains(episode.EpisodeId)).ToList();

        Episodes = Episodes.Concat(appended).ToList();
        _moreOffset += more.Count;
        CanLoadMore = hasMore;
        IsLoadingMore = false;
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

        // Show whatever's already known immediately (session cache, else disk cache), but always
        // refresh behind it - a session-cache hit used to skip this entirely, so an episode deleted
        // on the server stayed visible for the rest of the app session no matter how many times the
        // group was collapsed and re-expanded.
        var session = RecentEpisodesModel.GetSessionEpisodesForShow(ShowId);
        if (session != null)
        {
            Episodes = session;
        }
        else
        {
            var cached = await Task.Run(() => RecentEpisodesModel.GetDiskCachedEpisodesForShow(ShowId));
            if (cached != null)
                Episodes = cached;
            else
                IsLoading = true;
        }

        var fresh = await Task.Run(() => RecentEpisodesModel.RefreshEpisodesForShow(ShowId, ShowName, ShowImageUrl));
        if (fresh != null && !RecentEpisodesModel.AreEpisodesEqual(Episodes, fresh))
            Episodes = fresh;
        IsLoading = false;

        if (fresh == null)
        {
            _hasRevalidated = false;
            return;
        }

        // "Load more" pages on from here - it doesn't know this show's EPISODES_PER_SHOW cap, only
        // how many rows are already on screen.
        _moreOffset = Episodes.Count;
        CanLoadMore = true;
    }
}
