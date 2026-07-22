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

		SettingsContent.FlyoutItemIsVisible = false;

		UpdateAuthenticationVisibility();
		Authentication.OnAuthenticate += () => MainThread.BeginInvokeOnMainThread(UpdateAuthenticationVisibility);

		UpdateFlyoutWidth();
		DeviceDisplay.Current.MainDisplayInfoChanged += (_, _) =>
			MainThread.BeginInvokeOnMainThread(UpdateFlyoutWidth);

		_ = BackendConnector.Instance;
	}

	protected override bool OnBackButtonPressed()
	{
		if (Navigation?.NavigationStack?.Count > 1) return base.OnBackButtonPressed();

		if (!FlyoutIsPresented)
		{
			FlyoutIsPresented = true;
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

		FlyoutIsPresented = true;

		await BackendConnector.Instance.StorageLoadTask;

		if (!await Authentication.RestoreSessionAsync())
		{
			FlyoutIsPresented = false;
			await Shell.Current.GoToAsync("//AuthenticationPage");
		}

		UpdateAuthenticationVisibility();
	}

	private async void OnSettingsGearTapped(object sender, TappedEventArgs e)
	{
		FlyoutIsPresented = false;
		await GoToAsync("//SettingsPage");
	}
}
