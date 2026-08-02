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

    public void NotifyPlaybackStarting(string title, string imageUrl, string trackUri)
    {
        NotifyPlaybackStarting();

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
            _nextPollAllowedUtc = DateTime.MaxValue;
            if (!_pollingStopped) DiagnosticLog.Write("[Poll] paused 20+ minutes, stopping until playback starts again");
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
    }

    private bool _pollsWereFailing;

    private void RefreshPlayerValues()
    {
        var now = DateTime.UtcNow;
        if (now < _nextPollAllowedUtc) return;

        RefreshPlayerValuesCore();
        ScheduleNextPoll(now);
    }

    private void RefreshPlayerValuesCore()
    {
        CurrentlyPlayingContext currentlyPlayingContext = null;
        var api = APICaller.Instance;
        if (api == null || !api.TryGetCurrentPlaybackContext(out currentlyPlayingContext))
        {
            if (!_pollsWereFailing)
            {
                _pollsWereFailing = true;
                DiagnosticLog.Write($"[Poll] playback poll failing (client={(Authentication.SpotifyClient != null ? "up" : "null")}, cooldown={Ratelimiter.InCooldown})");
            }

            // A failed poll says nothing about playback. Keep the last snapshot and UI as they
            // are — writing an empty snapshot here reads as 30s of dead air to the radio, which
            // then shuts itself off in the middle of a network blip.
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
    }

    private static bool HasAppRemote
    {
        get
        {
            if (Models.PlaybackStateStore.HasActivePushSource?.Invoke() != true) return false;

            var activeDeviceId = Models.PlaybackStateStore.Instance.ActiveDeviceId;
            var phoneDeviceId = Models.PlaybackDeviceLookup.LastKnownPhoneDeviceId;
            return string.IsNullOrEmpty(activeDeviceId)
                || string.IsNullOrEmpty(phoneDeviceId)
                || activeDeviceId == phoneDeviceId;
        }
    }

    private void TogglePlayingFunc()
    {
        bool target = !IsPlaying;
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
        if (HasAppRemote)
        {
            Models.PlaybackCommands.ToggleRepeat?.Invoke();
            return;
        }

        Task.Run(() => APICaller.Instance?.ChangePlaybackRepeatType());
    }

    private void ChangeShuffleFunc()
    {
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