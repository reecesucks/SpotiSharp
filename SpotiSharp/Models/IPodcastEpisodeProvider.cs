using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>Picks the episodes a radio generation offers, so <see cref="RadioAudiobookshelfModel"/>'s
/// segment-building/resume logic (<c>ResumeStartFor</c>, <c>SegmentCountFor</c>) stays source-agnostic -
/// it only ever sees <see cref="IPodcastEpisodeSource"/>, never a concrete <see cref="RecentEpisode"/>
/// or AudioBookshelf episode. <paramref name="moreEpisodes"/> is likewise source-agnostic now, so the
/// "More episodes" picker (<see cref="RadioAudiobookshelfItem.ForMoreEpisode"/>) works for either.</summary>
internal interface IPodcastEpisodeProvider
{
    List<IPodcastEpisodeSource> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress,
        out HashSet<string> staleBingeEpisodeIds, out List<IPodcastEpisodeSource> moreEpisodes);
}
