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

		var isMobile = AppState.Instance.IsMobile;
		PlaylistsContent.FlyoutItemIsVisible = isMobile;
		ArtistsContent.FlyoutItemIsVisible = isMobile;
		PodcastsContent.FlyoutItemIsVisible = isMobile;

		_ = BackendConnector.Instance;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		await BackendConnector.Instance.StorageLoadTask;

		if (!await Authentication.TryRestoreSessionAsync())
		{
			await Shell.Current.GoToAsync("//AuthenticationPage");
		}
	}

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SettingsPage");
    }
}
