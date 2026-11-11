using System.Windows.Input;
using SpotiSharp.Keypad;
using SpotiSharp.Models;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

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

        var binge = RadioAudiobookshelfBingeAheadModel.CreateBingeFromEpisode(episode);
        RadioAudiobookshelfConfigModel.SetBinge(episode.LibraryItemId, binge);
        DiagnosticLog.Write($"[Radio] binge pinned at \"{episode.EpisodeName}\" for show \"{episode.ShowName}\"");

        // Otherwise nothing downloads until the next app login - this is the whole point of pinning.
        await RadioAudiobookshelfBingeAheadModel.EnsureAheadAsync();

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
        PlayerBarViewModel.Instance.ApplyAudiobookshelfState(episode.EpisodeId, episode.EpisodeName, episode.ShowCoverUrl, isPlaying: true, episode.ResumePositionMs);
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
            PlayerBarViewModel.Instance.ApplyAudiobookshelfState(_nowPlaying.EpisodeId, _nowPlaying.EpisodeName, _nowPlaying.ShowCoverUrl, isPlaying, positionMs);

            // Save periodically while playing, and immediately on a pause - not every 1s tick,
            // since each save is a network round trip to the AudioBookshelf server.
            var now = DateTime.UtcNow;
            var justPaused = _wasPlaying && !isPlaying;
            if (justPaused || now - _lastProgressSaveUtc >= ProgressSaveInterval)
            {
                _lastProgressSaveUtc = now;
                var durationMs = AudiobookshelfPlaybackCommands.GetDurationMs?.Invoke() ?? 0;
                if (durationMs <= 0) durationMs = _nowPlaying.DurationMs;
                _ = AudiobookshelfProgressSyncModel.SaveOrQueueAsync(_nowPlaying.LibraryItemId, _nowPlaying.EpisodeId, positionMs, durationMs);
            }
            _wasPlaying = isPlaying;

            return true;
        });
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
        var shows = await AudiobookshelfPodcastsModel.GetShowsAsync();
        ShowGroups = shows.Select(show => new AudiobookshelfShowGroupViewModel(show.libraryItemId, show.name, show.coverUrl)).ToList();
        IsLoading = false;

        if (shows.Count == 0) _hasLoaded = false;
    }

    protected override async Task RefreshDataAsync()
    {
        IsNotConfigured = !AudiobookshelfApiClient.IsConfigured;
        if (IsNotConfigured) return;

        _ = AudiobookshelfProgressSyncModel.FlushPendingAsync();

        var shows = await AudiobookshelfPodcastsModel.GetShowsAsync();
        ShowGroups = shows.Select(show => new AudiobookshelfShowGroupViewModel(show.libraryItemId, show.name, show.coverUrl)).ToList();
    }
}
