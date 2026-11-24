using System.Windows.Input;
using SpotiSharp.Keypad;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels;

/// <summary>Second page of the AudioBookshelf Podcasts view, reached by swiping sideways off the
/// show-grouped list (<see cref="AudiobookshelfPodcastsViewModel"/>) - every episode across every
/// show, newest first. Double-tapping a row marks it as "up next" for the next AudioBookshelf radio
/// generation, same mechanism the Radio page's "More episodes" picker already uses
/// (<see cref="RadioAudiobookshelfConfigModel.ToggleUpNext"/>), so <see cref="AudiobookshelfPodcastEpisodeProvider"/>
/// needs no changes to pick these up, in the order they were marked.</summary>
public class AudiobookshelfRecentEpisodesFlatViewModel : BaseViewModel
{
    public IKeypadSection? Section { get; set; }

    private readonly AudiobookshelfPodcastsViewModel _owner;

    public ICommand PlayEpisodeCommand => _owner.PlayEpisodeCommand;
    public ICommand ToggleMarkCommand { get; }

    public AudiobookshelfRecentEpisodesFlatViewModel(AudiobookshelfPodcastsViewModel owner)
    {
        _owner = owner;
        ToggleMarkCommand = new Command<AudiobookshelfEpisode>(ToggleMark);
    }

    private bool _hasLoaded;

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    // The unfiltered fetch, newest first across every show - what the Podcasts page's "Unplayed"
    // toggle (RadioAudiobookshelfConfigModel.GetPodcastsPageUnplayedOnly) filters down into Episodes.
    private List<AudiobookshelfEpisode> _rawEpisodes = new List<AudiobookshelfEpisode>();

    private List<AudiobookshelfEpisode> _episodes = new List<AudiobookshelfEpisode>();
    public List<AudiobookshelfEpisode> Episodes
    {
        get { return _episodes; }
        private set { SetProperty(ref _episodes, value); RefreshUpNextLabels(); }
    }

    public async void EnsureLoaded()
    {
        if (_hasLoaded) return;
        _hasLoaded = true;

        var cached = AudiobookshelfPodcastsModel.GetDiskCachedEpisodes();
        if (cached != null && cached.Count > 0)
            SetRawEpisodes(cached);
        else
            IsLoading = true;

        var fresh = await AudiobookshelfPodcastsModel.RefreshEpisodesAsync();
        if (fresh != null && !AreEpisodesEqual(_rawEpisodes, fresh))
            SetRawEpisodes(fresh);
        IsLoading = false;

        if (fresh == null) _hasLoaded = false;
    }

    protected override async Task RefreshDataAsync()
    {
        var fresh = await AudiobookshelfPodcastsModel.RefreshEpisodesAsync();
        if (fresh != null && !AreEpisodesEqual(_rawEpisodes, fresh))
            SetRawEpisodes(fresh);
        else
            RefreshUpNextLabels();
    }

    private void SetRawEpisodes(List<AudiobookshelfEpisode> raw)
    {
        _rawEpisodes = raw;
        ApplyFilter();
    }

    /// <summary>Recomputes <see cref="Episodes"/> from the already-fetched <see cref="_rawEpisodes"/>
    /// against the current "Unplayed" setting - called by <see cref="AudiobookshelfPodcastsPageViewModel"/>
    /// whenever the listener flips it, with no network round trip needed.</summary>
    internal void ApplyFilter()
    {
        Episodes = RadioAudiobookshelfConfigModel.GetPodcastsPageUnplayedOnly()
            ? _rawEpisodes.Where(episode => !episode.IsFinished).ToList()
            : _rawEpisodes;
    }

    private void ToggleMark(AudiobookshelfEpisode? episode)
    {
        if (episode == null) return;

        RadioAudiobookshelfConfigModel.ToggleUpNext(episode);
        DiagnosticLog.Write($"[Audiobookshelf] up next toggled for \"{episode.EpisodeName}\": {RadioAudiobookshelfConfigModel.GetUpNext().Count} picked");
        RefreshUpNextLabels();
    }

    private void RefreshUpNextLabels()
    {
        var upNext = RadioAudiobookshelfConfigModel.GetUpNext();
        foreach (var episode in Episodes)
        {
            int at = -1;
            for (int i = 0; i < upNext.Count; i++)
            {
                if (upNext[i].EpisodeId == episode.EpisodeId) at = i;
            }

            episode.UpNextLabel = at < 0 ? null : upNext.Count == 1 ? "Up next" : $"Up next {at + 1}";
        }
    }

    private static bool AreEpisodesEqual(List<AudiobookshelfEpisode> current, List<AudiobookshelfEpisode> fetched)
    {
        return current.Count == fetched.Count && current.Zip(fetched, (a, b) => a.EpisodeId == b.EpisodeId).All(equal => equal);
    }
}
