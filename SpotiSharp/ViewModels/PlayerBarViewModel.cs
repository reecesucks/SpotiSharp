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

    private PlayerBarViewModel()
    {
        _playerBarViewModel = this;
        TogglePlaying = new Command(TogglePlayingFunc);
        SongBack = new Command(SongBackFunc);
        SongSkip = new Command(SongSkipFunc);
        ChangeRepeat = new Command(ChangeRepeatFunc);
        ChangeShuffle = new Command(ChangeShuffleFunc);
        UiLoop.Instance.OnRefreshUi += RefreshPlayerValues;
    }

    private void RefreshPlayerValues()
    {
        // Song
        var currentlyPlayingContext = APICaller.Instance?.GetCurrentPlaybackContext();

        Models.PlaybackStateStore.Instance.Update(
            currentlyPlayingContext?.IsPlaying ?? false,
            currentlyPlayingContext?.Device?.Id);

        IsPlaying = currentlyPlayingContext?.IsPlaying ?? false;

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
                break;
            }
            case FullEpisode fullEpisode:
            {
                SongName = fullEpisode.Name;
                SongImageURL = fullEpisode.Images.ElementAtOrDefault(0)?.Url ?? string.Empty;
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

    public ICommand TogglePlaying { private set; get; }
    public ICommand SongBack { private set; get; }
    public ICommand SongSkip { private set; get; }
    public ICommand ChangeRepeat { private set; get; }
    public ICommand ChangeShuffle { private set; get; }
}