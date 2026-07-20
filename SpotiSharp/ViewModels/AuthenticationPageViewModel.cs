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
        _ = RefreshProfileAsync();

        // on mobile, drop the user onto the radio once they finish logging in
        if (Authentication.SpotifyClient != null && AppState.Instance.IsMobile)
        {
            MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//RadioPage"));
        }
    }

    internal override void OnAppearing()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await BackendConnector.Instance.StorageLoadTask;

        // wait out an in-flight session restore before deciding what to show
        if (Authentication.SpotifyClient == null && Authentication.HasStoredSession)
        {
            IsChecking = true;
            await Authentication.RestoreSessionAsync();
            IsChecking = false;
        }

        await RefreshProfileAsync();
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