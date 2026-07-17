using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioSettingsPageViewModel : BaseViewModel
{
    public ICommand GoBack { get; }

    public List<object> Pages { get; }

    public RadioSettingsPageViewModel()
    {
        GoBack = new Command(async () => await Shell.Current.GoToAsync(".."));
        Pages = new List<object>
        {
            new RadioSourceListViewModel("Playlists", LoadPlaylistToggles),
            new RadioSourceListViewModel("Podcasts", LoadPodcastToggles)
        };
    }

    private static List<RadioSourceWeightViewModel> LoadPlaylistToggles()
    {
        return PlaylistListModel.PlayLists
            .Select(playlist => new RadioSourceWeightViewModel(
                playlist.PlayListId,
                playlist.PlayListTitle,
                playlist.PlayListImageURL,
                RadioConfigModel.GetPlaylistWeight(playlist.PlayListId),
                RadioConfigModel.SetPlaylistWeight))
            .ToList();
    }

    private static List<RadioSourceWeightViewModel> LoadPodcastToggles()
    {
        return PlaylistListModel.SavedShows
            .Select(show => new RadioSourceWeightViewModel(
                show.Id,
                show.Name,
                show.Images?.ElementAtOrDefault(0)?.Url ?? string.Empty,
                RadioConfigModel.GetShowWeight(show.Id),
                RadioConfigModel.SetShowWeight))
            .ToList();
    }
}
