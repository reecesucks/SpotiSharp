using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels;

public class MainPageViewModel : BaseViewModel
{
    private bool _isUserIsNotAuthenticated;

    public bool IsUserIsNotAuthenticated
    {
        get { return _isUserIsNotAuthenticated; }
        set { SetProperty(ref _isUserIsNotAuthenticated, value); }
    }

    private bool _isChecking;

    public bool IsChecking
    {
        get { return _isChecking; }
        private set { SetProperty(ref _isChecking, value); }
    }

    public MainPageViewModel()
    {
        Authentication.OnAuthenticate += OnAuthenticated;
    }

    private void OnAuthenticated()
    {
        MainThread.BeginInvokeOnMainThread(() => IsUserIsNotAuthenticated = Authentication.SpotifyClient == null);
    }

    internal override void OnAppearing()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await BackendConnector.Instance.StorageLoadTask;

        if (Authentication.SpotifyClient == null && Authentication.HasStoredSession)
        {
            IsChecking = true;
            await Authentication.RestoreSessionAsync();
            IsChecking = false;
        }

        IsUserIsNotAuthenticated = Authentication.SpotifyClient == null;

        if (!IsUserIsNotAuthenticated && AppState.Instance.IsMobile)
        {
            await Shell.Current.GoToAsync("//RadioPage");
        }
    }
}