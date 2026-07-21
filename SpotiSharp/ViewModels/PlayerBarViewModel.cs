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

    private void RefreshPlayerValues()
    {
        var currentlyPlayingContext = APICaller.Instance?.GetCurrentPlaybackContext();

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

        Models.PlaybackStateStore.Instance.Update(
            currentlyPlayingContext?.IsPlaying ?? false,
            currentlyPlayingContext?.Device?.Id,
            currentItemUri,
            currentlyPlayingContext?.ProgressMs ?? 0,
            currentItemDurationMs);

        IsPlaying = currentlyPlayingContext?.IsPlaying ?? false;
        HasCurrentSong = currentlyPlayingContext?.Item != null;

        if (currentlyPlayingContext?.Item == null)
        {
            SongName = "Unauthorized";
            return;
        }

        switch (currentlyPlayingContext.Item)
        {
            case FullTrack fullTrack:
            {
                SongName = fullTrack.Name;
                SongImageURL = fullTrack.Album.Images.ElementAtOrDefault(0)?.Url ?? string.Empty;
                _currentTrackUri = fullTrack.Uri;
                if (_currentTrackId != fullTrack.Id)
                {
                    _currentTrackId = fullTrack.Id;
                    IsTrackPlaying = true;
                    var liked = APICaller.Instance?.IsTrackLiked(fullTrack.Id);
                    if (liked.HasValue && _currentTrackId == fullTrack.Id) IsSongLiked = liked.Value;
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

        IsShuffleOn = currentlyPlayingContext.ShuffleState;
        IsRepeatOn = currentlyPlayingContext.RepeatState == "track" || currentlyPlayingContext.RepeatState == "context";
    }

    private void TogglePlayingFunc()
    {
        IsPlaying = !IsPlaying;
        Task.Run(() => APICaller.Instance?.TogglePlaybackStatus());
    }

    private void SongBackFunc()
    {
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

            if (APICaller.Instance?.SkipToNextSong() ?? false) RefreshPlayerValues();
        });
    }

    private void ChangeRepeatFunc()
    {
        Task.Run(() => APICaller.Instance?.ChangePlaybackRepeatType());
    }

    private void ChangeShuffleFunc()
    {
        IsShuffleOn = !IsShuffleOn;
        Task.Run(() => APICaller.Instance?.TogglePlaybackShuffle());
    }

    private string _currentTrackUri;
    private string _currentTrackId;

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