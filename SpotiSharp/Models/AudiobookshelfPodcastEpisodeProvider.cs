using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;

/// <summary>The AudioBookshelf episode provider - picked instead of <see cref="SpotifyPodcastEpisodeProvider"/>
/// when the main settings page's "Radio podcast source" switch (<see cref="StorageHandler.UseAudiobookshelfPodcasts"/>)
/// is set to AudioBookshelf. Every show - regular or pinned as "binge" - contributes at most one
/// candidate episode (its own oldest-unplayed, or a binge's pin-respecting next-up via
/// <see cref="AudiobookshelfPodcastsModel.RefreshBingeNextEpisodesAsync"/>), and EPISODE_COUNT of
/// those candidates are picked by one weighted-random draw across all of them - the same ShowWeights
/// dictionary Spotify's provider uses (keyed here by LibraryItemId instead of a Spotify show id),
/// with an untouched show at a neutral weight so it's still in the running. A show is never
/// guaranteed a slot just for being binged; weighting it is what makes it come up more often. Both
/// caches are kept warm by <see cref="RadioAudiobookshelfContentPrefetcher"/>.
/// An episode picked as "up next" from the radio page's "More episodes" list always leads the next
/// generation, outside both the weighting and the EPISODE_COUNT cap - same as Spotify's provider.</summary>
internal class AudiobookshelfPodcastEpisodeProvider : IPodcastEpisodeProvider
{
    private const int EPISODE_COUNT = 3;

    public List<IPodcastEpisodeSource> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress, out HashSet<string> staleBingeEpisodeIds,
        out List<IPodcastEpisodeSource> moreEpisodes)
    {
        staleBingeEpisodeIds = new HashSet<string>();
        moreEpisodes = new List<IPodcastEpisodeSource>();

        // The disk cache (and bingeNext below) is only warmed once per app session
        // (RadioAudiobookshelfContentPrefetcher), so both an episode's cached IsFinished flag and its
        // cached ResumePositionMs go stale the moment a listen happens later in the same session -
        // what AudioBookshelf reports right now is what actually decides both, the same reasoning
        // SpotifyPodcastEpisodeProvider already applies to its own episode pool via a fresh progress
        // lookup. RadioAudiobookshelfModel.ResumeStartFor already knows how to prefer this over the
        // cached value once it's not null.
        var progress = FetchLiveProgress();
        liveProgress = progress;

        bool isLiveFinished(AudiobookshelfEpisode episode) =>
            episode.IsFinished || (progress.TryGetValue(episode.EpisodeId, out var live) && live.FullyPlayed);

        var excludedEpisodeIds = RadioAudiobookshelfConfigModel.Config.ExcludedEpisodeIds;

        // Picked from "More episodes" on the radio page to lead this generation - shared with
        // SpotifyPodcastEpisodeProvider's own up-next list, so only the AudioBookshelf-shaped picks
        // are this provider's to act on. They stand outside the normal pick (and its EPISODE_COUNT
        // cap) so the pool below can't hand them out a second time.
        var upNext = RadioAudiobookshelfConfigModel.GetUpNext()
            .OfType<AudiobookshelfEpisode>()
            .Where(episode => !excludedEpisodeIds.Contains(episode.EpisodeId))
            .ToList();
        var upNextIds = upNext.Select(episode => episode.EpisodeId).ToHashSet();

        var bingeShowIds = RadioAudiobookshelfConfigModel.GetAllBingeShowIds();
        var bingeNext = AudiobookshelfPodcastsModel.GetDiskCachedBingeNextEpisodes();
        var bingeEpisodes = bingeShowIds
            .Select(showId => bingeNext.GetValueOrDefault(showId))
            .Where(episode => episode != null)
            .Cast<AudiobookshelfEpisode>()
            .Where(episode => !isLiveFinished(episode) && !upNextIds.Contains(episode.EpisodeId))
            .ToList();

        var episodes = AudiobookshelfPodcastsModel.GetDiskCachedEpisodes() ?? new List<AudiobookshelfEpisode>();
        var bingeShowIdSet = bingeShowIds.ToHashSet();

        var nonBingedUnplayed = episodes
            .Where(episode => episode.DurationMs > 0 && !isLiveFinished(episode) && !bingeShowIdSet.Contains(episode.LibraryItemId)
                && !upNextIds.Contains(episode.EpisodeId))
            .ToList();

        // One candidate per non-binged show: its oldest still-unplayed episode, so catching up on a
        // show works through its backlog in order instead of skipping straight to the newest release.
        var candidatesByShow = nonBingedUnplayed
            .GroupBy(episode => episode.LibraryItemId)
            .Select(group => group.OrderBy(episode => episode.ReleaseDate).First());

        // ShowWeights is one dictionary shared with SpotifyPodcastEpisodeProvider - a Spotify show id
        // and an AudioBookshelf LibraryItemId are different id spaces, so a weight set on a Spotify
        // show (from before switching sources, or just never touched since) must not count here.
        // Scoping to the LibraryItemIds actually seen here keeps Spotify's weights out of this pick.
        var audiobookshelfShowIds = bingeShowIdSet.Concat(nonBingedUnplayed.Select(episode => episode.LibraryItemId)).ToHashSet();
        var configuredShowWeights = RadioAudiobookshelfConfigModel.Config.ShowWeights
            .Where(entry => audiobookshelfShowIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        // A weight only biases the random pick toward (or away from, at 0/"off") the show it's set
        // on - it never gates every OTHER show out. Treating "weighted at all" as "only weighted
        // shows are eligible" (what this used to do, copying Spotify's provider verbatim) meant
        // weighting a single show - including one already pinned as "binge", which has its own
        // weight slider right next to the binge toggle - silently excluded every other show forever,
        // which is exactly what starved "chosen" to 0 once the one weighted/forced pick ran out.
        // An unweighted show gets a neutral weight of 1, same odds as any other untouched show.
        //
        // A binge show's own next episode (bingeEpisodes, from the pin-respecting lookahead above)
        // is one candidate in this same draw rather than being forced in ahead of it - otherwise
        // every generation reliably has the same binge episode plus whatever few slots are left,
        // which is what made repeated generations feel identical instead of "semi random, weighted".
        // Each show still only ever offers its own oldest-unplayed/pinned-next pick, never more than
        // one episode per show per generation.
        var random = new Random();
        var candidates = bingeEpisodes.Concat(candidatesByShow);
        var recentPool = candidates
            .Where(episode => !RadioAudiobookshelfConfigModel.IsExplicitlyOff(configuredShowWeights, episode.LibraryItemId))
            .OrderByDescending(episode => Math.Pow(random.NextDouble(), 1.0 / EffectiveWeight(configuredShowWeights.GetValueOrDefault(episode.LibraryItemId, 1))));

        var chosen = recentPool.Take(EPISODE_COUNT).ToList();

        DiagnosticLog.Write($"[Radio] AudioBookshelf episode pick: {upNext.Count} up next, {bingeShowIds.Count} pinned show(s), " +
            $"{bingeEpisodes.Count} binge candidate(s), {candidatesByShow.Count()} other candidate show(s), " +
            $"{configuredShowWeights.Count(entry => entry.Value > 0)} explicitly weighted, " +
            $"{chosen.Count} chosen, {nonBingedUnplayed.Count} non-binged unplayed total");

        // Everything that could have gone in but didn't, for the "More episodes" picker on the radio
        // page to offer (and let the listener force in via "up next") - every unplayed episode across
        // non-binged shows, oldest first, plus a binged show's own pick when the weighted take above
        // left it out.
        var chosenIds = chosen.Select(episode => episode.EpisodeId).Concat(upNextIds).ToHashSet();
        moreEpisodes = bingeEpisodes
            .Concat(nonBingedUnplayed.OrderBy(episode => episode.ReleaseDate))
            .Where(episode => !chosenIds.Contains(episode.EpisodeId))
            .GroupBy(episode => episode.EpisodeId)
            .Select(group => group.First())
            .Cast<IPodcastEpisodeSource>()
            .ToList();

        var radioEpisodes = upNext.Concat(chosen).ToList();
        if (radioEpisodes.Count == 0) return new List<IPodcastEpisodeSource>();

        return radioEpisodes.Cast<IPodcastEpisodeSource>().ToList();
    }

    // Linear, not squared like Spotify's provider - squaring is fine there because only explicitly
    // weighted shows ever entered that draw, so the ratios were always between two chosen weights.
    // Here every untouched show defaults into the same draw at weight 1, so squaring a show set to
    // even a middling weight (the slider goes 0-7) against that default-1 neutral made the weighted
    // show win upwards of 90-98% of the time - which is what "same podcast a dozen generations in a
    // row" actually was. Linear keeps a weighted show favored without drowning out the randomness.
    private static double EffectiveWeight(int weight) => weight;

    private static Dictionary<string, EpisodeProgress> FetchLiveProgress()
    {
        try
        {
            var progress = AudiobookshelfApiClient.GetMediaProgressAsync().GetAwaiter().GetResult();
            return progress
                .Where(entry => !string.IsNullOrEmpty(entry.EpisodeId))
                .GroupBy(entry => entry.EpisodeId)
                .ToDictionary(group => group.Key!, group => new EpisodeProgress((int)(group.First().CurrentTimeSeconds * 1000), group.First().IsFinished));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Radio] live AudioBookshelf progress lookup failed, trusting the cached episode list instead: {ex.Message}");
            return new Dictionary<string, EpisodeProgress>();
        }
    }
}
