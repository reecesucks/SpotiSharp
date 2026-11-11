using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

/// <summary>One AudioBookshelf show row on the Podcasts page, mirroring <see cref="ShowGroupViewModel"/>'s
/// Spotify pattern (collapsed by default, expands to its episode list, revalidates on every expand so a
/// show removed from a feed or an episode deleted on the server doesn't linger forever in memory).
/// Unlike Spotify's read-only episode rows, AudioBookshelf's episode rows stay playable/downloadable/
/// binge-able, via the owning <see cref="AudiobookshelfPodcastsViewModel"/>'s commands.</summary>
public class AudiobookshelfShowGroupViewModel : BaseViewModel
{
    public string Id { get; }
    public string Name { get; }
    public string ImageUrl { get; }

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

    private const int PAGE_SIZE = 3;

    // The full back catalog, fetched once per expand/revalidate - "load more" only ever reveals
    // more of this, no extra network call, since AudioBookshelf hands back everything in one go.
    private List<AudiobookshelfEpisode> _allEpisodes = new List<AudiobookshelfEpisode>();

    private List<AudiobookshelfEpisode> _episodes = new List<AudiobookshelfEpisode>();
    public List<AudiobookshelfEpisode> Episodes
    {
        get { return _episodes; }
        private set { SetProperty(ref _episodes, value); }
    }

    private bool _canLoadMore;
    public bool CanLoadMore
    {
        get { return _canLoadMore; }
        private set { SetProperty(ref _canLoadMore, value); }
    }

    public ICommand ToggleExpanded { get; }
    public ICommand LoadMore { get; }

    public AudiobookshelfShowGroupViewModel(string id, string name, string imageUrl)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        ToggleExpanded = new Command(async () => await ToggleExpandedHandler());
        LoadMore = new Command(() => ShowMore());
    }

    private bool _hasRevalidated;

    private async Task ToggleExpandedHandler()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            return;
        }

        IsExpanded = true;
        if (_hasRevalidated) return;
        _hasRevalidated = true;

        if (_allEpisodes.Count > 0) ShowMore(initial: true);
        else IsLoading = true;

        var fresh = await AudiobookshelfPodcastsModel.FetchEpisodesForShowAsync(Id, Name, ImageUrl);
        MarkDownloaded(fresh);

        bool changed = !AreEpisodesEqual(_allEpisodes, fresh);
        _allEpisodes = fresh;
        if (changed || Episodes.Count == 0) ShowMore(initial: true);
        IsLoading = false;
    }

    private void ShowMore(bool initial = false)
    {
        int count = initial ? Math.Min(PAGE_SIZE, _allEpisodes.Count) : Math.Min(Episodes.Count + PAGE_SIZE, _allEpisodes.Count);
        Episodes = _allEpisodes.Take(count).ToList();
        CanLoadMore = count < _allEpisodes.Count;
    }

    private static void MarkDownloaded(List<AudiobookshelfEpisode> episodes)
    {
        foreach (var episode in episodes)
        {
            episode.IsDownloaded = AudiobookshelfDownloadsModel.IsDownloaded(episode.EpisodeId);
            episode.IsDownloading = AudiobookshelfDownloadsModel.IsDownloadInProgress(episode.EpisodeId);
            episode.DownloadProgress = AudiobookshelfDownloadsModel.GetDownloadProgress(episode.EpisodeId);
        }
    }

    private static bool AreEpisodesEqual(List<AudiobookshelfEpisode> current, List<AudiobookshelfEpisode> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) => a.EpisodeId == b.EpisodeId).All(equal => equal);
    }
}
