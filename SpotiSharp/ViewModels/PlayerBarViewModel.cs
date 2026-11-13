using System.Windows.Input;
using SpotifyAPI.Web;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels;

public class PlayerBarViewModel : BaseViewModel
{
    private static PlayerBarViewModel _playerBarViewModel;
    public static PlayerBarViewModel Instance => _playerBarViewModel ??= new PlayerBarViewModel();

    private string _songName;

    public string SongName
    {
        get { return _songName; }
        set { SetProperty(ref _songName, value); }
    }

    private string _songImageURL;

    public string SongImageURL
    {
        get { return _songImageURL; }
        set { SetProperty(ref _songImageURL, value); }
    }

    private bool _isPlaying;

    public bool IsPlaying
    {
        get { return _isPlaying; }
        private set { SetProperty(ref _isPlaying, value); }
    }

    private bool _isShuffleOn;

    public bool IsShuffleOn
    {
        get { return _isShuffleOn; }
        private set { SetProperty(ref _isShuffleOn, value); }
    }

    private bool _isRepeatOn;

    public bool IsRepeatOn
    {
        get { return _isRepeatOn; }
        private set { SetProperty(ref _isRepeatOn, value); }
    }

    private bool _hasCurrentSong;

    public bool HasCurrentSong
    {
        get { return _hasCurrentSong; }
        private set { SetProperty(ref _hasCurrentSong, value); }
    }

    private bool _isSongLiked;

    public bool IsSongLiked
    {
        get { return _isSongLiked; }
        private set { SetProperty(ref _isSongLiked, value); }
    }

    private bool _isTrackPlaying;

    public bool IsTrackPlaying
    {
        get { return _isTrackPlaying; }
        private set { SetProperty(ref _isTrackPlaying, value); }
    }

    private int _progressMs;

    public int ProgressMs
    {
        get { return _progressMs; }
        private set { SetProperty(ref _progressMs, value); }
    }

    private int _durationMs;

    public int DurationMs
    {
        get { return _durationMs; }
        private set
        {
            if (!SetProperty(ref _durationMs, value)) return;
            OnPropertyChanged(nameof(SeekMaxMs));
        }
    }

    private bool _isDraggingSeek;

    private int _seekDisplayMs;

    /// <summary>What the podcast progress bar shows while dragging - a separate value from
    /// <see cref="ProgressMs"/> so the live position ticking in underneath doesn't fight the
    /// listener's finger mid-drag. Mirrors <see cref="ProgressMs"/> whenever not being dragged.</summary>
    public int SeekDisplayMs
    {
        get { return _seekDisplayMs; }
        private set { SetProperty(ref _seekDisplayMs, value); }
    }

    /// <summary>How far forward the podcast progress bar can be dragged. While Radio is conducting
    /// an AudioBookshelf segment, this is the segment's own end - not the whole episode's duration -
    /// so a drag can only ever reach the current section's boundary, never into a section the radio
    /// hasn't decided to play yet. Floored at the current position so Maximum never ends up below
    /// Value (e.g. a stale read right at a segment transition).</summary>
    public int SeekMaxMs
    {
        get
        {
            var segmentEndMs = IsPodcastSource ? Models.RadioAudiobookshelfConductor.Instance.CurrentAudiobookshelfSegmentEndMs() : null;
            var max = segmentEndMs ?? DurationMs;
            return Math.Max(max, Math.Max(SeekDisplayMs, 1));
        }
    }

    /// <summary>True while the bar is showing an AudioBookshelf episode - the podcast progress bar
    /// only makes sense there (Spotify's own now-playing surfaces already have one).</summary>
    public bool IsPodcastSource => _isAudiobookshelfSource;

    /// <summary>Drag started on the podcast progress bar - freezes <see cref="SeekDisplayMs"/> against
    /// the live position ticking in underneath until <see cref="CommitSeekDrag"/>.</summary>
    public void BeginSeekDrag() => _isDraggingSeek = true;

    /// <summary>Drag released - sends the seek to AudioBookshelf and resumes tracking live position.</summary>
    public void CommitSeekDrag(int targetMs)
    {
        targetMs = Math.Max(0, targetMs);
        _isDraggingSeek = false;
        SeekDisplayMs = targetMs;
        if (!IsPodcastSource) return;

        ProgressMs = targetMs;
        Models.AudiobookshelfPlaybackCommands.SeekTo?.Invoke(targetMs);
    }

    private void UpdateProgress(int progressMs, int durationMs)
    {
        ProgressMs = progressMs;
        DurationMs = durationMs;
        if (!_isDraggingSeek) SeekDisplayMs = progressMs;
        OnPropertyChanged(nameof(SeekMaxMs));
    }

    private static readonly TimeSpan PendingStateWindow = TimeSpan.FromSeconds(5);

    private DateTime _playStatePendingUntil;
    private bool _expectedIsPlaying;

    private DateTime _shufflePendingUntil;
    private bool _expectedShuffle;

    private void ApplyIsPlaying(bool reported)
    {
        if (DateTime.UtcNow < _playStatePendingUntil)
        {
            if (reported != _expectedIsPlaying) return;
            _playStatePendingUntil = DateTime.MinValue;
        }

        IsPlaying = reported;
    }

    private void ApplyShuffle(bool reported)
    {
        if (DateTime.UtcNow < _shufflePendingUntil)
        {
            if (reported != _expectedShuffle) return;
            _shufflePendingUntil = DateTime.MinValue;
        }

        IsShuffleOn = reported;
    }

    private static readonly TimeSpan BackoffAfter = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BackedOffInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StopAfter = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan RadioHeartbeatInterval = TimeSpan.FromSeconds(30);

    private DateTime _idleSinceUtc = DateTime.UtcNow;
    private DateTime _nextPollAllowedUtc = DateTime.MinValue;
    private bool _pollingBackedOff;
    private bool _pollingStopped;

    public void NotifyPlaybackStarting()
    {
        _idleSinceUtc = DateTime.UtcNow;
        _nextPollAllowedUtc = DateTime.MinValue;
        if (_pollingBackedOff || _pollingStopped) DiagnosticLog.Write("[Poll] resuming active polling");
        _pollingBackedOff = false;
        _pollingStopped = false;
    }

    /// <summary>Hands the player bar back from AudioBookshelf to Spotify - needed before any Spotify
    /// playback that isn't a user tap through <see cref="NotifyPlaybackStarting(string,string,string)"/>
    /// (e.g. Radio auto-advancing from an AB segment into a Spotify one), since the poll/push guards in
    /// <see cref="RefreshPlayerValuesCore"/>/<see cref="ApplyPushedState"/> deliberately stay sticky on
    /// an AB source otherwise. Also actually pauses AudioBookshelf's own ExoPlayer here - the two are
    /// entirely separate audio tracks (same reasoning, in reverse, as the Android AudiobookshelfPlayer
    /// pausing Spotify before it starts), so without this the AB episode keeps playing underneath
    /// whatever Spotify starts next instead of yielding.</summary>
    public void NotifySpotifyPlaybackStarting()
    {
        if (!_isAudiobookshelfSource) return;
        _isAudiobookshelfSource = false;
        Models.AudiobookshelfPlaybackCommands.Pause?.Invoke();
        OnPropertyChanged(nameof(IsPodcastSource));
    }

    public void NotifyPlaybackStarting(string title, string imageUrl, string trackUri)
    {
        NotifyPlaybackStarting();
        NotifySpotifyPlaybackStarting();

        SongName = title;
        SongImageURL = imageUrl ?? string.Empty;
        HasCurrentSong = true;
        IsTrackPlaying = true;
        IsSongLiked = false;
        _currentTrackUri = trackUri;
        _currentTrackId = null;
        _lastKnownUri = trackUri;
        _lastKnownProgressMs = 0;

        IsPlaying = true;
        _expectedIsPlaying = true;
        _playStatePendingUntil = DateTime.UtcNow.Add(PendingStateWindow);

        PersistLastPlayed(trackUri, title, SongImageURL, 0, isTrack: true);
    }

    private void ScheduleNextPoll(DateTime now)
    {
        if (IsPlaying)
        {
            _idleSinceUtc = now;
            _nextPollAllowedUtc = DateTime.MinValue;
            _pollingBackedOff = false;
            _pollingStopped = false;
            return;
        }

        var idleFor = now - _idleSinceUtc;
        if (idleFor >= StopAfter)
        {
            bool radioConducting = Models.RadioConductor.Instance.IsActive;
            _nextPollAllowedUtc = radioConducting ? now + RadioHeartbeatInterval : DateTime.MaxValue;
            if (!_pollingStopped)
            {
                DiagnosticLog.Write(radioConducting
                    ? "[Poll] paused 20+ minutes, dropping to a 30s heartbeat while the radio is active"
                    : "[Poll] paused 20+ minutes, stopping until playback starts again");
            }
            _pollingStopped = true;
        }
        else if (idleFor >= BackoffAfter)
        {
            _nextPollAllowedUtc = now + BackedOffInterval;
            if (!_pollingBackedOff) DiagnosticLog.Write("[Poll] paused 30s+, backing off to a 15s poll interval");
            _pollingBackedOff = true;
        }
        else
        {
            _nextPollAllowedUtc = DateTime.MinValue;
        }
    }

    private PlayerBarViewModel()
    {
        _playerBarViewModel = this;
        TogglePlaying = new Command(TogglePlayingFunc);
        SongBack = new Command(SongBackFunc);
        SongSkip = new Command(SongSkipFunc);
        ChangeRepeat = new Command(ChangeRepeatFunc);
        ChangeShuffle = new Command(ChangeShuffleFunc);
        RotationUp = new Command(() => ChangeRotationFunc(increase: true));
        RotationDown = new Command(() => ChangeRotationFunc(increase: false));
        ToggleSongLiked = new Command(ToggleSongLikedFunc);
        UiLoop.Instance.OnRefreshUi += RefreshPlayerValues;

        RestoreLastPlayed();
    }

    private void RestoreLastPlayed()
    {
        var last = Models.LastPlayedModel.Load();
        if (last == null || string.IsNullOrEmpty(last.Uri)) return;

        SongName = last.Title;
        SongImageURL = last.ImageUrl ?? string.Empty;
        HasCurrentSong = true;
        IsTrackPlaying = last.IsTrack;
        _lastKnownUri = last.Uri;
        _lastKnownProgressMs = last.ProgressMs;
        _lastPersistedUri = last.Uri;
        if (last.IsTrack) _currentTrackUri = last.Uri;
    }

    private string _lastPersistedUri;
    private DateTime _lastPersistedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan PersistThrottle = TimeSpan.FromSeconds(10);

    private void PersistLastPlayed(string uri, string title, string imageUrl, int progressMs, bool isTrack)
    {
        if (string.IsNullOrEmpty(uri)) return;

        var now = DateTime.UtcNow;
        var uriChanged = uri != _lastPersistedUri;
        if (!uriChanged && now - _lastPersistedAtUtc < PersistThrottle) return;

        _lastPersistedUri = uri;
        _lastPersistedAtUtc = now;

        var snapshot = new Models.LastPlayedSnapshot(uri, title, imageUrl, progressMs, isTrack);
        Task.Run(() => Models.LastPlayedModel.Save(snapshot));
    }

    private bool _pollsWereFailing;

    private void RefreshPlayerValues()
    {
        if (Models.PlaybackStateStore.HasActivePushSource?.Invoke() == true) return;

        var now = DateTime.UtcNow;
        if (now < _nextPollAllowedUtc) return;

        RefreshPlayerValuesCore();
        ScheduleNextPoll(now);
    }

    private void RefreshPlayerValuesCore()
    {
        // AudioBookshelf playback has nothing to do with Spotify's account/device state, so a Spotify
        // poll must not stomp the bar while an AB episode owns it - not even transiently, since
        // GetIsPlaying() reads ExoPlayer's IsPlaying, which goes false during buffering/pausing too
        // (gating on it here used to drop _isAudiobookshelfSource on the very next tick and never
        // recover, hiding the podcast progress bar for the rest of the episode).
        if (_isAudiobookshelfSource)
        {
            // Nothing else drives a continuous position update while Radio is the one playing the
            // episode (only the standalone Podcasts page's own poll calls ApplyAudiobookshelfState) -
            // without this the podcast progress bar would freeze at whatever position it last had.
            var positionMs = Models.AudiobookshelfPlaybackCommands.GetPositionMs?.Invoke() ?? ProgressMs;
            var durationMs = Models.AudiobookshelfPlaybackCommands.GetDurationMs?.Invoke() ?? DurationMs;
            UpdateProgress(positionMs, durationMs);
            return;
        }

        CurrentlyPlayingContext currentlyPlayingContext = null;
        var api = APICaller.Instance;
        if (api == null || !api.TryGetCurrentPlaybackContext(out currentlyPlayingContext))
        {
            if (!_pollsWereFailing)
            {
                _pollsWereFailing = true;
                DiagnosticLog.Write($"[Poll] playback poll failing (client={(Authentication.SpotifyClient != null ? "up" : "null")}, cooldown={Ratelimiter.InCooldown})");
            }

            if (Authentication.SpotifyClient == null)
            {
                HasCurrentSong = false;
                SongName = "Unauthorized";
            }
            return;
        }

        if (_pollsWereFailing)
        {
            _pollsWereFailing = false;
            DiagnosticLog.Write("[Poll] playback poll recovered");
        }

        _isAudiobookshelfSource = false;
        OnPropertyChanged(nameof(IsPodcastSource));

        string currentItemUri = null;
        int currentItemDurationMs = 0;
        if (currentlyPlayingContext?.Item is FullTrack playingTrack)
        {
            currentItemUri = playingTrack.Uri;
            currentItemDurationMs = playingTrack.DurationMs;
        }
        else if (currentlyPlayingContext?.Item is FullEpisode playingEpisode)
        {
            currentItemUri = playingEpisode.Uri;
            currentItemDurationMs = playingEpisode.DurationMs;
        }

        if (Models.PlaybackStateStore.HasActivePushSource?.Invoke() != true)
        {
            Models.PlaybackStateStore.Instance.Update(
                currentlyPlayingContext?.IsPlaying ?? false,
                currentlyPlayingContext?.Device?.Id,
                currentItemUri,
                currentlyPlayingContext?.ProgressMs ?? 0,
                currentItemDurationMs,
                currentlyPlayingContext?.ShuffleState ?? false);
        }
        else
        {
            Models.PlaybackStateStore.Instance.UpdateActiveDeviceId(currentlyPlayingContext?.Device?.Id);
        }

        if (currentlyPlayingContext?.Item == null)
        {
            if (!string.IsNullOrEmpty(_lastKnownUri))
            {
                ApplyIsPlaying(false);
                HasCurrentSong = true;
                return;
            }

            HasCurrentSong = false;
            SongName = "Unauthorized";
            return;
        }

        ApplyIsPlaying(currentlyPlayingContext.IsPlaying);
        HasCurrentSong = true;
        if (!string.IsNullOrEmpty(currentItemUri))
        {
            _lastKnownUri = currentItemUri;
            _lastKnownProgressMs = currentlyPlayingContext.ProgressMs;
        }

        switch (currentlyPlayingContext.Item)
        {
            case FullTrack fullTrack:
            {
                SongName = fullTrack.Name;
                SongImageURL = fullTrack.Album?.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty;
                _currentTrackUri = fullTrack.Uri;
                if (_currentTrackId != fullTrack.Id)
                {
                    _currentTrackId = fullTrack.Id;
                    IsTrackPlaying = true;
                    if (string.IsNullOrEmpty(fullTrack.Id))
                    {
                        IsSongLiked = false;
                    }
                    else
                    {
                        var liked = APICaller.Instance?.IsTrackLiked(fullTrack.Id);
                        if (liked.HasValue && _currentTrackId == fullTrack.Id) IsSongLiked = liked.Value;
                    }
                }
                break;
            }
            case FullEpisode fullEpisode:
            {
                SongName = fullEpisode.Name;
                SongImageURL = fullEpisode.Images.ElementAtOrDefault(0)?.Url ?? string.Empty;
                _currentTrackUri = null;
                _currentTrackId = null;
                IsTrackPlaying = false;
                IsSongLiked = false;
                break;
            }
        }

        ApplyShuffle(currentlyPlayingContext.ShuffleState);
        IsRepeatOn = currentlyPlayingContext.RepeatState == "track" || currentlyPlayingContext.RepeatState == "context";

        PersistLastPlayed(_lastKnownUri, SongName, SongImageURL, _lastKnownProgressMs, IsTrackPlaying);
    }

    /// <summary>
    /// True while the bar is showing/controlling an AudioBookshelf episode instead of Spotify.
    /// Spotify's own update paths (<see cref="ApplyPushedState"/>, the Web API poll) clear it again
    /// the moment they have something real to show, so the bar always reflects whichever source most
    /// recently had something playing.
    /// </summary>
    private bool _isAudiobookshelfSource;

    /// <summary>Updates the bar from the AudioBookshelf/Media3 player. Separate from <see cref="ApplyPushedState"/>
    /// by design — AudioBookshelf has no shuffle/repeat/skip/like concept here, so this only touches what's shared.</summary>
    public void ApplyAudiobookshelfState(string episodeId, string name, string imageUrl, bool isPlaying, int progressMs, int durationMs = 0)
    {
        if (string.IsNullOrEmpty(episodeId)) return;

        _isAudiobookshelfSource = true;
        OnPropertyChanged(nameof(IsPodcastSource));
        ApplyIsPlaying(isPlaying);
        if (isPlaying) NotifyPlaybackStarting();

        HasCurrentSong = true;
        SongName = name;
        SongImageURL = imageUrl ?? string.Empty;
        _lastKnownUri = episodeId;
        _lastKnownProgressMs = progressMs;
        UpdateProgress(progressMs, durationMs > 0 ? durationMs : DurationMs);

        _currentTrackUri = null;
        _currentTrackId = null;
        IsTrackPlaying = false;
        IsSongLiked = false;
    }

    /// <summary>Updates the bar from an App Remote player state push. Must be called on the main thread.</summary>
    public void ApplyPushedState(string uri, string name, bool isEpisode, bool isPlaying, int progressMs, bool shuffleOn, bool repeatOn)
    {
        if (string.IsNullOrEmpty(uri)) return;

        // Same reasoning as the Web API poll guard above: App Remote keeps pushing state for whatever
        // Spotify last had loaded even while the user is actually listening to an AudioBookshelf episode.
        if (_isAudiobookshelfSource) return;

        _isAudiobookshelfSource = false;
        OnPropertyChanged(nameof(IsPodcastSource));
        ApplyIsPlaying(isPlaying);
        if (isPlaying) NotifyPlaybackStarting();

        HasCurrentSong = true;
        SongName = name;
        _lastKnownUri = uri;
        _lastKnownProgressMs = progressMs;

        if (isEpisode)
        {
            _currentTrackUri = null;
            _currentTrackId = null;
            IsTrackPlaying = false;
            IsSongLiked = false;
        }
        else
        {
            _currentTrackUri = uri;
            IsTrackPlaying = true;

            var trackId = uri.Split(':').LastOrDefault();
            if (_currentTrackId != trackId)
            {
                _currentTrackId = trackId;
                IsSongLiked = false;
                Task.Run(() =>
                {
                    var liked = APICaller.Instance?.IsTrackLiked(trackId);
                    if (liked.HasValue && _currentTrackId == trackId) IsSongLiked = liked.Value;
                });
            }
        }

        ApplyShuffle(shuffleOn);
        IsRepeatOn = repeatOn;

        PersistLastPlayed(uri, SongName, SongImageURL, progressMs, !isEpisode);
    }

    private static bool HasAppRemote => Models.PlaybackStateStore.HasActivePushSource?.Invoke() == true;

    private void TogglePlayingFunc()
    {
        bool target = !IsPlaying;

        if (_isAudiobookshelfSource)
        {
            IsPlaying = target;
            if (target) Models.AudiobookshelfPlaybackCommands.Play?.Invoke();
            else Models.AudiobookshelfPlaybackCommands.Pause?.Invoke();
            return;
        }

        DiagnosticLog.Write($"[PlayerBar] play/pause tapped: sending {(target ? "resume" : "pause")} via {(HasAppRemote ? "App Remote" : "Web API")}");
        IsPlaying = target;
        _expectedIsPlaying = target;
        _playStatePendingUntil = DateTime.UtcNow.Add(PendingStateWindow);
        if (target) NotifyPlaybackStarting();

        if (HasAppRemote)
        {
            if (target) Models.PlaybackCommands.Resume?.Invoke();
            else Models.PlaybackCommands.Pause?.Invoke();
            return;
        }

        if (target && Models.PlaybackCommands.WakeSpotify != null)
        {
            _ = ResumeAfterWakeAsync();
            return;
        }

        ToggleViaWebApi(target);
    }

    private async Task ResumeAfterWakeAsync()
    {
        bool woke = await Models.PlaybackCommands.WakeSpotify!();
        DiagnosticLog.Write($"[PlayerBar] App Remote was down, woke Spotify: {woke}");

        if (woke && HasAppRemote)
        {
            _playStatePendingUntil = DateTime.UtcNow.Add(PendingStateWindow);
            Models.PlaybackCommands.Resume?.Invoke();
            return;
        }

        ToggleViaWebApi(target: true);
    }

    private void ToggleViaWebApi(bool target)
    {
        var resumeUri = _lastKnownUri;
        var resumeProgressMs = _lastKnownProgressMs;

        Task.Run(() =>
        {
            var api = APICaller.Instance;
            if (api == null) return;
            if (api.TogglePlaybackStatus()) return;

            if (!target || string.IsNullOrEmpty(resumeUri)) return;
            var deviceId = Models.RadioConductor.ResolveDeviceId(api);
            if (!string.IsNullOrEmpty(deviceId)) api.PlayUriAtPosition(resumeUri, resumeProgressMs, deviceId);
        });
    }

    private void SongBackFunc()
    {
        if (_isAudiobookshelfSource) return;

        if (HasAppRemote)
        {
            Models.PlaybackCommands.SkipPrevious?.Invoke();
            return;
        }

        Task.Run(() =>
        {
            if (APICaller.Instance?.SkipToPreviousSong() ?? false) RefreshPlayerValues();
        });
    }

    private void SongSkipFunc()
    {
        if (_isAudiobookshelfSource) return;

        Task.Run(() =>
        {
            if (Models.RadioConductor.Instance.AdvanceManually())
            {
                RefreshPlayerValues();
                return;
            }

            if (HasAppRemote)
            {
                Models.PlaybackCommands.SkipNext?.Invoke();
                return;
            }

            if (APICaller.Instance?.SkipToNextSong() ?? false) RefreshPlayerValues();
        });
    }

    private void ChangeRepeatFunc()
    {
        if (_isAudiobookshelfSource) return;

        if (HasAppRemote)
        {
            Models.PlaybackCommands.ToggleRepeat?.Invoke();
            return;
        }

        Task.Run(() => APICaller.Instance?.ChangePlaybackRepeatType());
    }

    private void ChangeShuffleFunc()
    {
        if (_isAudiobookshelfSource) return;

        bool target = !IsShuffleOn;
        IsShuffleOn = target;
        _expectedShuffle = target;
        _shufflePendingUntil = DateTime.UtcNow.Add(PendingStateWindow);

        if (HasAppRemote)
        {
            Models.PlaybackCommands.SetShuffle?.Invoke(target);
            return;
        }

        Task.Run(() => APICaller.Instance?.TogglePlaybackShuffle());
    }

    private string _currentTrackUri;
    private string _currentTrackId;

    private string _lastKnownUri;
    private int _lastKnownProgressMs;

    private void ToggleSongLikedFunc()
    {
        var trackId = _currentTrackId;
        if (trackId == null) return;

        bool newState = !IsSongLiked;
        IsSongLiked = newState;

        Task.Run(() =>
        {
            bool success = newState
                ? APICaller.Instance?.LikeTrack(trackId) ?? false
                : APICaller.Instance?.UnlikeTrack(trackId) ?? false;
            if (!success && trackId == _currentTrackId) IsSongLiked = !newState;
        });
    }

    private bool _isRotationUpBusy;

    public bool IsRotationUpBusy
    {
        get { return _isRotationUpBusy; }
        private set { SetProperty(ref _isRotationUpBusy, value); }
    }

    private bool _isRotationDownBusy;

    public bool IsRotationDownBusy
    {
        get { return _isRotationDownBusy; }
        private set { SetProperty(ref _isRotationDownBusy, value); }
    }

    private void ChangeRotationFunc(bool increase)
    {
        var trackUri = _currentTrackUri;
        if (trackUri == null || IsRotationUpBusy || IsRotationDownBusy) return;
        if (increase) IsRotationUpBusy = true;
        else IsRotationDownBusy = true;

        Task.Run(() =>
        {
            try
            {
                bool changed = increase
                    ? Models.SongRotationModel.IncreaseRotation(trackUri)
                    : Models.SongRotationModel.DecreaseRotation(trackUri);
                if (changed) Models.PlaylistListModel.RefreshPlayLists();
            }
            finally
            {
                if (increase) IsRotationUpBusy = false;
                else IsRotationDownBusy = false;
            }
        });
    }

    public ICommand TogglePlaying { private set; get; }
    public ICommand SongBack { private set; get; }
    public ICommand SongSkip { private set; get; }
    public ICommand ChangeRepeat { private set; get; }
    public ICommand ChangeShuffle { private set; get; }
    public ICommand RotationUp { private set; get; }
    public ICommand RotationDown { private set; get; }
    public ICommand ToggleSongLiked { private set; get; }
}