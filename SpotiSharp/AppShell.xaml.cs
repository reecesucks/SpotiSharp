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

		_ = BackendConnector.Instance;
	}

	protected override bool OnBackButtonPressed()
	{
		if (Navigation?.NavigationStack?.Count > 1) return base.OnBackButtonPressed();

		// Back returns to the menu instead of exiting, unless we are already on it.
		if (CurrentItem?.Route != "MenuPage")
		{
			_ = GoToAsync("//MenuPage");
			return true;
		}

		return base.OnBackButtonPressed();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		await BackendConnector.Instance.StorageLoadTask;

		// The full-screen menu replaces the flyout, so land there once authenticated.
		if (!await Authentication.RestoreSessionAsync())
			await Shell.Current.GoToAsync("//AuthenticationPage");
		else
			await Shell.Current.GoToAsync("//MenuPage", animate: false);
	}
}
