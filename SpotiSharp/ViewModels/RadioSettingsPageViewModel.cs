using System.Windows.Input;
using SpotiSharp.Helpers;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioSettingsPageViewModel : BaseViewModel
{
    public ICommand GoBack { get; }
    public ICommand ClearCache { get; }

    public List<object> Pages { get; }

    public RadioSettingsPageViewModel()
    {
        GoBack = new Command(async () => await Shell.Current.GoToAsync(".."));
        ClearCache = new Command(async () => await ClearCacheAsync());
        Pages = new List<object>
        {
            new RadioSourceListViewModel("Playlists", LoadPlaylistToggles, () => PlaylistListModel.RefreshPlayLists()),
            new RadioSourceListViewModel("Podcasts", LoadPodcastToggles, () => PlaylistListModel.RefreshSavedShows()),
            new RadioAlbumListViewModel()
        };
    }

    private static async Task ClearCacheAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Clear cache",
            "Delete all cached data (playlists, albums, podcasts, radio)? Your radio settings are kept. Reopen pages to reload.",
            "Clear",
            "Cancel");
        if (!confirm) return;

        await Task.Run(CacheManager.ClearContentCaches);

        await Shell.Current.DisplayAlert(
            "Cache cleared",
            "Cached data was deleted. Regenerate the radio and reopen lists to reload fresh data.",
            "OK");
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
                ImageHelper.Thumbnail(show.Images),
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
