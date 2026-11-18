using System.Windows.Input;
using SpotiSharp.Keypad;
using SpotiSharp.Models;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.ViewModels;

public class AudiobookshelfPodcastsViewModel : BaseViewModel
{
    public IKeypadSection? Section { get; set; }

    public ICommand PlayEpisodeCommand { get; }
    public ICommand ToggleDownloadCommand { get; }

    private AudiobookshelfEpisode? _nowPlaying;

    public AudiobookshelfPodcastsViewModel()
    {
        PlayEpisodeCommand = new Command<AudiobookshelfEpisode>(PlayEpisode);
        ToggleDownloadCommand = new Command<AudiobookshelfEpisode>(ToggleDownload);
    }

    /// <summary>Long-press on an episode row ("Binge from here") - pins this exact episode as the
    /// show's binge position, overriding whatever <see cref="RadioAudiobookshelfBingeAheadModel"/>
    /// would otherwise guess from server-side progress.</summary>
    internal async Task BingeFromHereAsync(AudiobookshelfEpisode? episode)
    {
        if (episode == null) return;

        var binge = await RadioAudiobookshelfBingeAheadModel.CreateBingeFromEpisodeAsync(episode);
        if (binge == null) return;
        RadioAudiobookshelfConfigModel.SetBinge(episode.LibraryItemId, binge);
        DiagnosticLog.Write($"[Radio] binge pinned at \"{episode.EpisodeName}\" for show \"{episode.ShowName}\"");

        // Otherwise nothing downloads until the next app login - this is the whole point of pinning.
        await RadioAudiobookshelfBingeAheadModel.EnsureAheadAsync("Binge from here");
        // Same reasoning for Radio's episode pick - otherwise it keeps offering the newest episode of
        // this show instead of the one just pinned until the next app login refreshes the cache.
        await AudiobookshelfPodcastsModel.RefreshBingeNextEpisodesAsync();

        await Shell.Current.DisplayAlert("Binge set", $"Bingeing \"{episode.ShowName}\" from \"{episode.EpisodeName}\".", "OK");
    }

    /// <summary>Starts playing an episode and hands display/control over to the shared player bar —
    /// see <see cref="PlayerBarViewModel.ApplyAudiobookshelfState"/>. No bar of our own here. Plays from
    /// the downloaded file when there is one, otherwise streams.</summary>
    private void PlayEpisode(AudiobookshelfEpisode? episode)
    {
        if (episode == null || string.IsNullOrEmpty(episode.AudioFileIno)) return;
        if (AudiobookshelfPlaybackCommands.Load == null) return;

        var nowPlayingInfo = new AudiobookshelfNowPlayingInfo(episode.EpisodeName, episode.ShowName, episode.ShowCoverUrl);
        var localPath = AudiobookshelfDownloadsModel.GetLocalPath(episode.EpisodeId);
        if (localPath != null)
        {
            DiagnosticLog.Write($"[Audiobookshelf] playing episode {episode.EpisodeId} from downloaded file: {localPath}");
            AudiobookshelfPlaybackCommands.Load(localPath, new Dictionary<string, string>(), nowPlayingInfo, episode.ResumePositionMs);
        }
        else
        {
            var streamUrl = AudiobookshelfApiClient.BuildStreamUrl(episode.LibraryItemId, episode.AudioFileIno);
            DiagnosticLog.Write($"[Audiobookshelf] playing episode {episode.EpisodeId} by streaming: {streamUrl}");
            AudiobookshelfPlaybackCommands.Load(streamUrl, AudiobookshelfApiClient.BuildAuthHeaders(), nowPlayingInfo, episode.ResumePositionMs);
        }

        _nowPlaying = episode;
        PlayerBarViewModel.Instance.ApplyAudiobookshelfState(episode.EpisodeId, episode.EpisodeName, episode.ShowCoverUrl, isPlaying: true, episode.ResumePositionMs, episode.DurationMs);
        StartPolling();
    }

    private async void ToggleDownload(AudiobookshelfEpisode? episode)
    {
        if (episode == null || episode.IsDownloading) return;

        if (episode.IsDownloaded)
            AudiobookshelfDownloadsModel.DeleteDownload(episode);
        else
            await AudiobookshelfDownloadsModel.DownloadAsync(episode);
    }

    private bool _pollingStarted;
    private bool _wasPlaying;
    private DateTime _lastProgressSaveUtc = DateTime.MinValue;
    private string? _finishNotifiedEpisodeId;
    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(15);

    private void StartPolling()
    {
        if (_pollingStarted) return;
        _pollingStarted = true;

        Application.Current?.Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (_nowPlaying == null) return true;

            var isPlaying = AudiobookshelfPlaybackCommands.GetIsPlaying?.Invoke() ?? false;
            var positionMs = AudiobookshelfPlaybackCommands.GetPositionMs?.Invoke() ?? 0;
            PlayerBarViewModel.Instance.ApplyAudiobookshelfState(_nowPlaying.EpisodeId, _nowPlaying.EpisodeName, _nowPlaying.ShowCoverUrl, isPlaying, positionMs, _nowPlaying.DurationMs);

            // Save periodically while playing, and immediately on a pause - not every 1s tick,
            // since each save is a network round trip to the AudioBookshelf server.
            var now = DateTime.UtcNow;
            var justPaused = _wasPlaying && !isPlaying;
            var durationMs = AudiobookshelfPlaybackCommands.GetDurationMs?.Invoke() ?? 0;
            if (durationMs <= 0) durationMs = _nowPlaying.DurationMs;
            // The player reporting "not playing" right at the tail end (rather than a user-initiated
            // pause) means it played through to completion - without telling the server this was a
            // finish, the episode comes back as if never listened to.
            var isFinished = durationMs > 0 && positionMs >= durationMs - RadioTuning.END_TOLERANCE_MS;
            Task? saveTask = null;
            if (justPaused || isFinished || now - _lastProgressSaveUtc >= ProgressSaveInterval)
            {
                _lastProgressSaveUtc = now;
                saveTask = AudiobookshelfProgressSyncModel.SaveOrQueueAsync(_nowPlaying.LibraryItemId, _nowPlaying.EpisodeId, positionMs, durationMs, isFinished);
            }

            // Edge-triggered on the same completion signal, once per episode - finishing it is when
            // the binge window needs to advance so the next episodes start downloading right away,
            // not just on the next app start. Waits for the finished save above to land first, since
            // the ahead-window is computed from the server's own progress.
            if (isFinished && _finishNotifiedEpisodeId != _nowPlaying.EpisodeId)
            {
                _finishNotifiedEpisodeId = _nowPlaying.EpisodeId;
                _ = NotifyBingeAheadAfterSaveAsync(saveTask, "episode finished", (_nowPlaying.LibraryItemId, _nowPlaying.Guid));
            }
            _wasPlaying = isPlaying;

            return true;
        });
    }

    private static async Task NotifyBingeAheadAfterSaveAsync(Task? saveTask, string reason, (string libraryItemId, string? guid)? finishedEpisode = null)
    {
        if (saveTask != null) await saveTask;
        await RadioAudiobookshelfBingeAheadModel.EnsureAheadAsync(reason, finishedEpisode);
    }

    private bool _hasLoaded;

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private bool _isNotConfigured;
    public bool IsNotConfigured
    {
        get { return _isNotConfigured; }
        private set { SetProperty(ref _isNotConfigured, value); }
    }

    // Show-grouped, like the Spotify Podcasts page - collapsed rows with episodes fetched lazily per
    // show on expand (AudiobookshelfShowGroupViewModel), not a flat cross-show list.
    private List<AudiobookshelfShowGroupViewModel> _showGroups = new List<AudiobookshelfShowGroupViewModel>();
    public List<AudiobookshelfShowGroupViewModel> ShowGroups
    {
        get { return _showGroups; }
        private set { SetProperty(ref _showGroups, value); }
    }

    public async void EnsureLoaded()
    {
        if (_hasLoaded) return;
        _hasLoaded = true;

        IsNotConfigured = !AudiobookshelfApiClient.IsConfigured;
        if (IsNotConfigured) return;

        _ = AudiobookshelfProgressSyncModel.FlushPendingAsync();

        IsLoading = ShowGroups.Count == 0;
        var showsTask = AudiobookshelfPodcastsModel.GetShowsAsync();
        // Otherwise Radio's binge pick (AudiobookshelfPodcastEpisodeProvider) keeps reading whatever
        // this cache last held - which, outside "Binge from here" and the Settings pin toggle, could be
        // from a stale prefetch (or none at all, if AudioBookshelf was switched on after app start)
        // despite this page clearly showing the show has unplayed episodes right now.
        await Task.WhenAll(showsTask, AudiobookshelfPodcastsModel.RefreshBingeNextEpisodesAsync());
        var shows = showsTask.Result;
        ShowGroups = BuildShowGroups(shows);
        IsLoading = false;

        if (shows.Count == 0) _hasLoaded = false;
    }

    protected override async Task RefreshDataAsync()
    {
        IsNotConfigured = !AudiobookshelfApiClient.IsConfigured;
        if (IsNotConfigured) return;

        _ = AudiobookshelfProgressSyncModel.FlushPendingAsync();

        var showsTask = AudiobookshelfPodcastsModel.GetShowsAsync();
        await Task.WhenAll(showsTask, AudiobookshelfPodcastsModel.RefreshBingeNextEpisodesAsync());
        ShowGroups = BuildShowGroups(showsTask.Result);
    }

    // Regular shows first, then whichever shows are pinned as "binge" - a slight gap (IsFirstBingeShow,
    // read by the XAML trigger) before the first one is the only thing marking the split, not a header,
    // so a listener bingeing nothing never sees anything different from a single list.
    private static List<AudiobookshelfShowGroupViewModel> BuildShowGroups(List<(string libraryItemId, string name, string coverUrl)> shows)
    {
        var bingeShowIds = RadioAudiobookshelfConfigModel.GetAllBingeShowIds().ToHashSet();

        var regular = shows.Where(show => !bingeShowIds.Contains(show.libraryItemId))
            .Select(show => new AudiobookshelfShowGroupViewModel(show.libraryItemId, show.name, show.coverUrl))
            .ToList();
        var binge = shows.Where(show => bingeShowIds.Contains(show.libraryItemId))
            .Select(show => new AudiobookshelfShowGroupViewModel(show.libraryItemId, show.name, show.coverUrl))
            .ToList();

        if (binge.Count > 0) binge[0].IsFirstBingeShow = true;

        return regular.Concat(binge).ToList();
    }

    /// <summary>Re-applies the Podcasts page's "Unplayed" toggle to every show already in
    /// <see cref="ShowGroups"/>, expanded or not - called by <see cref="AudiobookshelfPodcastsPageViewModel"/>
    /// whenever the listener flips it.</summary>
    internal void ApplyUnplayedOnlyFilter()
    {
        foreach (var group in ShowGroups) group.RefreshUnplayedFilter();
    }
}
