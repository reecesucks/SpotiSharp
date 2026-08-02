using System.Windows.Input;
using SpotiSharpBackend;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class AuthenticationPageViewModel : BaseViewModel
{
    private string? _profilePictureURL;
    
    public string? ProfilePictureURL
    {
        get { return _profilePictureURL; }
        set { SetProperty(ref _profilePictureURL, value); }
    }
    
    private Color _authenticationStatusColor;
    
    public Color AuthenticationStatusColor
    {
        get { return _authenticationStatusColor; }
        set { SetProperty(ref _authenticationStatusColor, value); }
    }
    
    private string _userName;
    
    public string UserName
    {
        get { return _userName; }
        set { SetProperty(ref _userName, value); }
    }
    
    private string _clientId;

    public string ClientId
    {
        get { return _clientId; }
        set { SetProperty(ref _clientId, value); }
    }

    private bool _isChecking;

    public bool IsChecking
    {
        get { return _isChecking; }
        private set { SetProperty(ref _isChecking, value); }
    }

    public AuthenticationPageViewModel()
    {
        ConnectToSpotifyAPI = new Command(() => { if (ClientId != null && ClientId != string.Empty) Authentication.Authenticate(ClientId); });
        OpenSpotifyDevDashBoard = new Command(() => Browser.Default.OpenAsync("https://developer.spotify.com/dashboard/", BrowserLaunchMode.SystemPreferred));
        Authentication.OnAuthenticate += OnAuthenticated;
        ClientId = StorageHandler.ClientId;
    }

    private void OnAuthenticated()
    {
        DiagnosticLog.Write($"[Auth] OnAuthenticated fired, client={(Authentication.SpotifyClient != null ? "up" : "null")}");
        _ = RefreshProfileAsync();

        if (Authentication.SpotifyClient != null) NavigateToLanding();
    }

    private static void NavigateToLanding()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var landing = AppState.Instance.IsMobile ? "//MenuPage" : "//MainPage";
            try
            {
                await Shell.Current.GoToAsync(landing);
                DiagnosticLog.Write($"[Auth] navigated to {landing}");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Auth] navigation to {landing} failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
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

        await RefreshProfileAsync();

        DiagnosticLog.Write($"[Auth] page appeared, client={(Authentication.SpotifyClient != null ? "up" : "null")}, hasStoredSession={Authentication.HasStoredSession}");
        if (Authentication.SpotifyClient != null) NavigateToLanding();
    }

    private async Task RefreshProfileAsync()
    {
        var profile = await Task.Run(() => new Profile());
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UserName = profile.UserName ?? "Not Authenticated";
            ProfilePictureURL = profile.ProfilePictureURL;
            AuthenticationStatusColor = profile.IsAuthenticated ? Brush.Green.Color : Brush.Red.Color;
        });
    }
    
    public ICommand ConnectToSpotifyAPI { private set; get; }
    
    public ICommand OpenSpotifyDevDashBoard { private set; get; }

}