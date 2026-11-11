using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>Picks the episodes a radio generation offers, so <see cref="RadioAudiobookshelfModel"/>'s
/// segment-building/resume logic (<c>ResumeStartFor</c>, <c>SegmentCountFor</c>) stays source-agnostic -
/// it only ever sees <see cref="IPodcastEpisodeSource"/>, never a concrete <see cref="RecentEpisode"/>
/// or AudioBookshelf episode. <paramref name="moreEpisodes"/> stays Spotify-shaped for now: the
/// podcasts-only "More episodes" view (<see cref="RadioAudiobookshelfItem.ForMoreEpisode"/>) is still
/// Spotify-only and out of scope for this seam.</summary>
internal interface IPodcastEpisodeProvider
{
    List<IPodcastEpisodeSource> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress,
        out HashSet<string> staleBingeEpisodeIds, out List<RecentEpisode> moreEpisodes);
}
