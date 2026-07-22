using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("DetailPlaylistPage", typeof(DetailPlaylistPage));
		Routing.RegisterRoute("DetailArtistPage", typeof(Views.DetailArtistPage));
		Routing.RegisterRoute("DetailAlbumPage", typeof(Views.DetailAlbumPage));
		Routing.RegisterRoute("RadioSettingsPage", typeof(Views.RadioSettingsPage));

		var isMobile = AppState.Instance.IsMobile;
		HomeContent.FlyoutItemIsVisible = !isMobile;
		RadioContent.FlyoutItemIsVisible = isMobile;
		PlaylistsContent.FlyoutItemIsVisible = isMobile;
		ArtistsContent.FlyoutItemIsVisible = isMobile;
		AlbumsContent.FlyoutItemIsVisible = isMobile;
		PodcastsContent.FlyoutItemIsVisible = isMobile;
		PlaylistCreatorContent.FlyoutItemIsVisible = !isMobile;
		ManagePlaylistsContent.FlyoutItemIsVisible = !isMobile;

		SettingsContent.FlyoutItemIsVisible = true;

		UpdateAuthenticationVisibility();
		Authentication.OnAuthenticate += () => MainThread.BeginInvokeOnMainThread(UpdateAuthenticationVisibility);

		UpdateFlyoutWidth();
		DeviceDisplay.Current.MainDisplayInfoChanged += (_, _) =>
			MainThread.BeginInvokeOnMainThread(UpdateFlyoutWidth);

		_ = BackendConnector.Instance;
	}

	private readonly List<string> _rootHistory = new();

	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);

		var route = args.Current?.Location?.OriginalString;
		if (string.IsNullOrEmpty(route)) return;

		if (route.Trim('/').Split('/').Length != 1) return;

		if (_rootHistory.Count == 0 || _rootHistory[^1] != route) _rootHistory.Add(route);
	}

	protected override bool OnBackButtonPressed()
	{
		if (Navigation?.NavigationStack?.Count > 1) return base.OnBackButtonPressed();

		if (_rootHistory.Count > 1)
		{
			_rootHistory.RemoveAt(_rootHistory.Count - 1);
			_ = GoToAsync(_rootHistory[^1]);
			return true;
		}

		return base.OnBackButtonPressed();
	}

	private void UpdateFlyoutWidth()
	{
		var display = DeviceDisplay.Current.MainDisplayInfo;
		if (display.Width <= 0 || display.Density <= 0) return;

		FlyoutWidth = display.Width / display.Density;
	}

	private void UpdateAuthenticationVisibility()
	{
		AuthenticationContent.FlyoutItemIsVisible = Authentication.SpotifyClient == null;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		await BackendConnector.Instance.StorageLoadTask;

		if (!await Authentication.RestoreSessionAsync())
		{
			await Shell.Current.GoToAsync("//AuthenticationPage");
		}
		else
		{
			FlyoutIsPresented = true;
		}

		UpdateAuthenticationVisibility();
	}
}
