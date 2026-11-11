using System.Text.RegularExpressions;
using System.Windows.Input;
using SpotiSharp.Helpers;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfSettingsPageViewModel : BaseViewModel
{
    private const int MAX_PLAYLIST_WEIGHT = 7;

    private static readonly Regex RotationTag = new Regex(@"#R-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ICommand GoBack { get; }
    public ICommand ClearCache { get; }

    public List<object> Pages { get; }

    public RadioAudiobookshelfSettingsPageViewModel()
    {
        GoBack = new Command(async () => await Shell.Current.GoToAsync(".."));
        ClearCache = new Command(async () => await ClearCacheAsync());
        Pages = new List<object>
        {
            new RadioAudiobookshelfSourceListViewModel("Playlists", LoadPlaylistToggles, () => PlaylistListModel.RefreshPlayLists()),
            new RadioAudiobookshelfSourceListViewModel("Podcasts", LoadPodcastToggles, () => PlaylistListModel.RefreshSavedShows()),
            new RadioAudiobookshelfSourceListViewModel("Podcasts (AudioBookshelf)", LoadAudiobookshelfPodcastToggles,
                () => AudiobookshelfPodcastsModel.RefreshEpisodesAsync().GetAwaiter().GetResult()),
            new RadioAudiobookshelfAlbumListViewModel(),
            new RadioAudiobookshelfMixSettingsViewModel()
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

    private static List<RadioAudiobookshelfSourceWeightViewModel> LoadPlaylistToggles()
    {
        var ordered = PlaylistListModel.OrderForDisplay(PlaylistListModel.PlayLists);

        var rotationPlaylists = ordered.Where(playlist => RotationTag.IsMatch(playlist.PlayListTitle ?? string.Empty)).ToList();
        var otherPlaylists = ordered.Except(rotationPlaylists);

        var items = otherPlaylists
            .Select(playlist => new RadioAudiobookshelfSourceWeightViewModel(
                playlist.PlayListId,
                playlist.PlayListTitle,
                playlist.PlayListImageURL,
                RadioAudiobookshelfConfigModel.GetPlaylistWeight(playlist.PlayListId),
                RadioAudiobookshelfConfigModel.SetPlaylistWeight))
            .ToList();

        if (rotationPlaylists.Count > 0) items.Add(BuildRotationToggle(rotationPlaylists));

        return items;
    }

    private static RadioAudiobookshelfSourceWeightViewModel BuildRotationToggle(List<Playlist> rotationPlaylists)
    {
        var rotationIds = rotationPlaylists.Select(playlist => playlist.PlayListId).ToList();

        int weight = rotationIds.Select(RadioAudiobookshelfConfigModel.GetPlaylistWeight).DefaultIfEmpty(0).Max();
        if (weight <= 0)
        {
            weight = MAX_PLAYLIST_WEIGHT;
            foreach (var id in rotationIds) RadioAudiobookshelfConfigModel.SetPlaylistWeight(id, weight);
        }

        return new RadioAudiobookshelfSourceWeightViewModel(
            rotationIds[0],
            "Rotation (#R)",
            rotationPlaylists[0].PlayListImageURL,
            weight,
            (_, newWeight) =>
            {
                foreach (var id in rotationIds) RadioAudiobookshelfConfigModel.SetPlaylistWeight(id, newWeight);
            });
    }

    private static List<RadioAudiobookshelfSourceWeightViewModel> LoadPodcastToggles()
    {
        return PlaylistListModel.SavedShows
            .Select(show => new RadioAudiobookshelfSourceWeightViewModel(
                show.Id,
                show.Name,
                ImageHelper.Thumbnail(show.Images),
                RadioAudiobookshelfConfigModel.GetShowWeight(show.Id),
                RadioAudiobookshelfConfigModel.SetShowWeight,
                BingeStatusFor(show.Id),
                ToggleBingeAsync))
            .ToList();
    }

    private static string BingeStatusFor(string showId)
    {
        var binge = RadioAudiobookshelfConfigModel.GetBinge(showId);
        return binge == null ? null : $"Binge · next: {binge.NextEpisodeName}";
    }

    private static async Task ToggleBingeAsync(RadioAudiobookshelfSourceWeightViewModel item)
    {
        if (RadioAudiobookshelfConfigModel.GetBinge(item.Id) != null)
        {
            RadioAudiobookshelfConfigModel.ClearBinge(item.Id);
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

        RadioAudiobookshelfConfigModel.SetBinge(item.Id, binge);
        item.SetBingeStatus(BingeStatusFor(item.Id));
    }

    private static List<RadioAudiobookshelfSourceWeightViewModel> LoadAudiobookshelfPodcastToggles()
    {
        var episodes = AudiobookshelfPodcastsModel.GetDiskCachedEpisodes() ?? new List<AudiobookshelfEpisode>();

        return episodes
            .GroupBy(episode => episode.LibraryItemId)
            .Select(group =>
            {
                var first = group.First();
                return new RadioAudiobookshelfSourceWeightViewModel(
                    first.LibraryItemId,
                    first.ShowName,
                    first.ShowCoverUrl,
                    0,
                    (_, _) => { }, // AudioBookshelf shows don't support weighting yet
                    BingeStatusFor(first.LibraryItemId),
                    ToggleAudiobookshelfBingeAsync,
                    showWeight: false);
            })
            .ToList();
    }

    private static async Task ToggleAudiobookshelfBingeAsync(RadioAudiobookshelfSourceWeightViewModel item)
    {
        if (RadioAudiobookshelfConfigModel.GetBinge(item.Id) != null)
        {
            RadioAudiobookshelfConfigModel.ClearBinge(item.Id);
            item.SetBingeStatus(null);
            return;
        }

        var binge = await RadioAudiobookshelfBingeAheadModel.CreateBingeAsync(item.Id);
        if (binge == null)
        {
            await Shell.Current.DisplayAlert(
                "Set binge point",
                "Couldn't find an unfinished episode for this show on the server.",
                "OK");
            return;
        }

        RadioAudiobookshelfConfigModel.SetBinge(item.Id, binge);
        item.SetBingeStatus(BingeStatusFor(item.Id));

        // Otherwise nothing downloads until the next app login - this is the whole point of pinning.
        await RadioAudiobookshelfBingeAheadModel.EnsureAheadAsync();
    }
}
