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
            new RadioSourceListViewModel("Podcasts", LoadPodcastToggles),
            new RadioAlbumListViewModel()
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
                RadioConfigModel.SetShowWeight,
                BingeStatusFor(show.Id),
                ToggleBingeAsync))
            .ToList();
    }

    private static string BingeStatusFor(string showId)
    {
        var binge = RadioConfigModel.GetBinge(showId);
        return binge == null ? null : $"Binge · next: {binge.NextEpisodeName}";
    }

    private static async Task ToggleBingeAsync(RadioSourceWeightViewModel item)
    {
        if (RadioConfigModel.GetBinge(item.Id) != null)
        {
            RadioConfigModel.ClearBinge(item.Id);
            item.SetBingeStatus(null);
            return;
        }

        var binge = await Task.Run(() => BingeProgressModel.CreateFromCurrentPlayback(item.Id));
        if (binge == null)
        {
            await Shell.Current.DisplayAlert(
                "Set binge point",
                "Start playing the episode you are up to in this show, then tap the pin again.",
                "OK");
            return;
        }

        RadioConfigModel.SetBinge(item.Id, binge);
        item.SetBingeStatus(BingeStatusFor(item.Id));
    }
}
