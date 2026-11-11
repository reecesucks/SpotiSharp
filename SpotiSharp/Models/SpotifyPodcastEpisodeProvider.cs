using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>The default (and currently only fully-implemented) episode provider - Spotify's show
/// weights, binge tracking, up-next and excluded-episode-ids, moved verbatim out of
/// <see cref="RadioAudiobookshelfModel"/> so that model can call through <see cref="IPodcastEpisodeProvider"/>
/// instead of reaching into <see cref="RecentEpisodesModel"/> directly.</summary>
internal class SpotifyPodcastEpisodeProvider : IPodcastEpisodeProvider
{
    private const int EPISODE_COUNT = 3;

    // how many of a binge's upcoming episodes the podcasts view offers
    private const int BINGE_LOOKAHEAD = 2;

    /// <summary>
    /// Picks the episodes for this radio. The cached "unlistened" verdict is only as fresh as
    /// the episode cache, so what Spotify reports right now decides which are still unfinished;
    /// <paramref name="liveProgress"/> carries that on for resume positions and is null if
    /// the lookup failed, in which case the cached verdict is all we have.
    /// </summary>
    public List<IPodcastEpisodeSource> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress, out HashSet<string> staleBingeEpisodeIds,
        out List<RecentEpisode> moreEpisodes)
    {
        liveProgress = null;
        staleBingeEpisodeIds = new HashSet<string>();
        moreEpisodes = new List<RecentEpisode>();

        var cached = RecentEpisodesModel.GetDiskCachedEpisodesAcrossAllShows();
        var episodes = cached != null && cached.Count > 0 && cached.All(episode => episode.DurationMs > 0 && !string.IsNullOrEmpty(episode.ShowId))
            ? cached
            : RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows();
        if (episodes == null) return null;

        var excludedEpisodeIds = RadioAudiobookshelfConfigModel.Config.ExcludedEpisodeIds;
        if (excludedEpisodeIds.Count > 0) episodes = episodes.Where(episode => !excludedEpisodeIds.Contains(episode.EpisodeId)).ToList();

        var configuredShowWeights = RadioAudiobookshelfConfigModel.Config.ShowWeights;
        var showWeights = ActiveWeights(configuredShowWeights);
        var bingeShowIds = RadioAudiobookshelfConfigModel.Config.BingeShows.Keys.ToList();

        var bingeEpisodes = new List<RecentEpisode>();
        var bingeLookahead = new List<RecentEpisode>();
        if (bingeShowIds.Count > 0)
        {
            var savedShows = PlaylistListModel.SavedShows;
            foreach (var showId in bingeShowIds)
            {
                bool excluded = showWeights.Count > 0
                    ? !showWeights.ContainsKey(showId)
                    : RadioAudiobookshelfConfigModel.IsExplicitlyOff(configuredShowWeights, showId);
                if (excluded) continue;
                var show = savedShows.FirstOrDefault(savedShow => savedShow.Id == showId);
                var next = BingeProgressModel.FindNextEpisodes(showId, show?.Name ?? string.Empty, ImageHelper.Thumbnail(show?.Images),
                    BINGE_LOOKAHEAD, excludedEpisodeIds);
                if (next.Count > 0) bingeEpisodes.Add(next[0]);
                bingeLookahead.AddRange(next);
            }
            episodes = episodes.Where(episode => !bingeShowIds.Contains(episode.ShowId)).ToList();
        }

        // Picked by the listener to lead this radio; they stand outside the normal pick so the
        // pool below can't hand them out a second time.
        var upNext = RadioAudiobookshelfConfigModel.GetUpNext()
            .Where(episode => !excludedEpisodeIds.Contains(episode.EpisodeId))
            .ToList();
        var upNextIds = upNext.Select(episode => episode.EpisodeId).ToHashSet();
        episodes = episodes.Where(episode => !upNextIds.Contains(episode.EpisodeId)).ToList();
        bingeEpisodes = bingeEpisodes.Where(episode => !upNextIds.Contains(episode.EpisodeId)).ToList();

        // What Spotify reports right now, for the recent pool and the binge picks alike -
        // it decides what is still unfinished, and feeds ResumeStartFor further down.
        var progress = APICaller.Instance?.GetEpisodeProgress(
            episodes.Select(episode => episode.EpisodeId)
                .Concat(bingeLookahead.Select(episode => episode.EpisodeId))
                .Concat(upNextIds)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList());
        liveProgress = progress;

        foreach (var episode in bingeEpisodes)
        {
            int resumeMs = progress?.GetValueOrDefault(episode.EpisodeId)?.ResumePositionMs ?? episode.ResumePositionMs;
            BingeProgressModel.NoteResumeProgress(episode.ShowId, episode.EpisodeId, resumeMs);
            if (BingeProgressModel.IsStale(episode.ShowId)) staleBingeEpisodeIds.Add(episode.EpisodeId);
        }

        if (progress != null)
        {
            bool Unfinished(RecentEpisode episode) =>
                !EpisodeHelper.IsListened(progress.GetValueOrDefault(episode.EpisodeId), episode.DurationMs);

            episodes = episodes.Where(Unfinished).ToList();
            upNext = upNext.Where(Unfinished).ToList();
        }

        List<RecentEpisode> chosen;
        if (showWeights.Count == 0)
        {
            chosen = bingeEpisodes
                .Concat(episodes.Where(episode => !RadioAudiobookshelfConfigModel.IsExplicitlyOff(configuredShowWeights, episode.ShowId)))
                .Take(EPISODE_COUNT)
                .ToList();
        }
        else
        {
            var random = new Random();
            chosen = bingeEpisodes
                .Concat(episodes.Where(episode => showWeights.ContainsKey(episode.ShowId)))
                .OrderByDescending(episode => Math.Pow(random.NextDouble(), 1.0 / EffectiveWeight(showWeights[episode.ShowId])))
                .Take(EPISODE_COUNT)
                .ToList();
        }

        var ordered = chosen
            .OrderByDescending(episode => bingeShowIds.Contains(episode.ShowId))
            .ThenByDescending(episode => episode.ReleaseDate);
        var radioEpisodes = upNext.Concat(ordered).ToList();

        // Everything that could have gone in but didn't, for the podcasts view to offer: each
        // binge's next couple in listening order, then the rest newest first.
        var inRadio = radioEpisodes.Select(episode => episode.EpisodeId).ToHashSet();
        moreEpisodes = bingeLookahead
            .Concat(episodes.OrderByDescending(episode => episode.ReleaseDate))
            .Where(episode => !inRadio.Contains(episode.EpisodeId))
            .GroupBy(episode => episode.EpisodeId)
            .Select(group => group.First())
            .ToList();

        return radioEpisodes.Cast<IPodcastEpisodeSource>().ToList();
    }

    private static Dictionary<string, int> ActiveWeights(Dictionary<string, int> weights)
    {
        return weights.Where(entry => entry.Value > 0).ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static double EffectiveWeight(int weight) => (double)weight * weight;
}
