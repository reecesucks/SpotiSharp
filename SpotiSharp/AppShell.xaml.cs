using SpotiSharp.Models;
using SpotiSharp.Themes;
using SpotiSharpBackend;

namespace SpotiSharp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		ThemeSwitch.IsToggled = ThemeService.Current == AppThemeVariant.Spotify;

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

		// settings only holds desktop/collaboration config, hide it and its toolbar gear on mobile
		SettingsContent.FlyoutItemIsVisible = !isMobile;
		if (isMobile) ToolbarItems.Remove(SettingsToolbarItem);

		// the authentication page is only needed while logged out
		UpdateAuthenticationVisibility();
		Authentication.OnAuthenticate += () => MainThread.BeginInvokeOnMainThread(UpdateAuthenticationVisibility);

		_ = BackendConnector.Instance;
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

		UpdateAuthenticationVisibility();
	}

    private void OnThemeSwitchToggled(object sender, ToggledEventArgs e)
    {
        ThemeService.Apply(e.Value ? AppThemeVariant.Spotify : AppThemeVariant.Ipod);
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SettingsPage");
    }
}
