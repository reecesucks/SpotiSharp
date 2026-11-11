using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>The AudioBookshelf episode provider - picked instead of <see cref="SpotifyPodcastEpisodeProvider"/>
/// when the main settings page's "Radio podcast source" switch (<see cref="StorageHandler.UseAudiobookshelfPodcasts"/>)
/// is set to AudioBookshelf. No show-weighting or binge-tracking yet (AudioBookshelf has no equivalent
/// UI for either today) - just the most recently released episodes across your AudioBookshelf shows,
/// reading the same disk-cached list the standalone Podcasts page shows, kept warm by
/// <see cref="RadioAudiobookshelfContentPrefetcher"/>.</summary>
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

        var episodes = AudiobookshelfPodcastsModel.GetDiskCachedEpisodes();
        if (episodes == null || episodes.Count == 0) return new List<IPodcastEpisodeSource>();

        return episodes
            .Where(episode => episode.DurationMs > 0)
            .OrderByDescending(episode => episode.ReleaseDate)
            .Take(EPISODE_COUNT)
            .Cast<IPodcastEpisodeSource>()
            .ToList();
    }
}
