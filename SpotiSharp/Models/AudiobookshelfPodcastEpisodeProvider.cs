using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>The AudioBookshelf episode provider - picked instead of <see cref="SpotifyPodcastEpisodeProvider"/>
/// when the main settings page's "Radio podcast source" switch (<see cref="StorageHandler.UseAudiobookshelfPodcasts"/>)
/// is set to AudioBookshelf. No show-weighting yet (AudioBookshelf has no equivalent UI for it today),
/// but a show pinned as "binge" gets its next unfinished episode (<see cref="AudiobookshelfPodcastsModel.RefreshBingeNextEpisodesAsync"/>)
/// instead of whatever's newest - the rest of the slots are filled with the most recently released
/// episodes across non-binged AudioBookshelf shows, reading the same disk-cached list the standalone
/// Podcasts page shows. Both caches are kept warm by <see cref="RadioAudiobookshelfContentPrefetcher"/>.</summary>
internal class AudiobookshelfPodcastEpisodeProvider : IPodcastEpisodeProvider
{
    private const int EPISODE_COUNT = 3;

    public List<IPodcastEpisodeSource> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress, out HashSet<string> staleBingeEpisodeIds,
        out List<RecentEpisode> moreEpisodes)
    {
        // AudioBookshelf tracks resume position per episode already (AudiobookshelfProgressSyncModel),
        // not through a separate live-progress lookup the way Spotify's episode-progress API works -
        // AudiobookshelfEpisode.ResumePositionMs (used by RadioAudiobookshelfModel.ResumeStartFor) is
        // already that server-synced value, so there's nothing further to report here.
        liveProgress = null;
        staleBingeEpisodeIds = new HashSet<string>();
        moreEpisodes = new List<RecentEpisode>();

        var bingeShowIds = RadioAudiobookshelfConfigModel.GetAllBingeShowIds();
        var bingeNext = AudiobookshelfPodcastsModel.GetDiskCachedBingeNextEpisodes();
        var bingeEpisodes = bingeShowIds
            .Select(showId => bingeNext.GetValueOrDefault(showId))
            .Where(episode => episode != null)
            .Cast<AudiobookshelfEpisode>()
            .ToList();

        var episodes = AudiobookshelfPodcastsModel.GetDiskCachedEpisodes() ?? new List<AudiobookshelfEpisode>();
        var bingeShowIdSet = bingeShowIds.ToHashSet();
        var recentPool = episodes
            .Where(episode => episode.DurationMs > 0 && !bingeShowIdSet.Contains(episode.LibraryItemId))
            .OrderByDescending(episode => episode.ReleaseDate);

        if (bingeEpisodes.Count == 0 && !recentPool.Any()) return new List<IPodcastEpisodeSource>();

        return bingeEpisodes
            .Concat(recentPool)
            .Take(EPISODE_COUNT)
            .Cast<IPodcastEpisodeSource>()
            .ToList();
    }
}
