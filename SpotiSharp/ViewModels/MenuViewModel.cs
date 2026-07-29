using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SpotiSharp.ViewModels;

/// <summary>One selectable entry in the main menu — a page title and its Shell route.</summary>
public class MenuDestination
{
    public string Title { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}

/// <summary>
/// Backs <c>MenuPage</c>, the full-screen menu that replaces the Shell flyout.
/// Destinations mirror the device-aware visibility the flyout used to apply.
/// </summary>
public class MenuViewModel : BaseViewModel
{
    public ObservableCollection<MenuDestination> Destinations { get; } = new();

    public ICommand OpenCommand { get; }

    public MenuViewModel()
    {
        OpenCommand = new Command<MenuDestination>(Navigate);
        BuildDestinations();
    }

    private void BuildDestinations()
    {
        Destinations.Clear();

        if (AppState.Instance.IsMobile)
        {
            Add("Radio", "RadioPage");
            Add("Playlists", "PlaylistsPage");
            Add("Artists", "ArtistsPage");
            Add("Albums", "AlbumsPage");
            Add("Podcasts", "PodcastsPage");
        }
        else
        {
            Add("Home", "MainPage");
            Add("Create Playlists", "PlaylistCreatorPage");
            Add("Manage Playlists", "ManagePlayListsPage");
        }

        Add("Settings", "SettingsPage");
    }

    /// <summary>Navigate to a destination. Used by both a touch tap and (later) a keypad Select.</summary>
    public async void Navigate(MenuDestination destination)
    {
        if (destination is null) return;
        await Shell.Current.GoToAsync($"//{destination.Route}");
    }

    private void Add(string title, string route) =>
        Destinations.Add(new MenuDestination { Title = title, Route = route });
}
