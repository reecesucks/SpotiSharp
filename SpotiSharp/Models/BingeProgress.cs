namespace SpotiSharp.Models;

public class BingeProgress
{
    // position counted from the show's first episode; everything at or below
    // this index is considered finished and is never searched again
    public int LastFinishedIndexFromOldest { get; set; }

    // shown in the radio settings so the current binge position is visible
    public string NextEpisodeName { get; set; }

    public string LastTrackedEpisodeId { get; set; }
    public int LastTrackedResumePositionMs { get; set; }
    public DateTime? LastActivityUtc { get; set; }

    // AudioBookshelf-only: the pinned/current episode's podcast-feed guid, used instead of
    // LastTrackedEpisodeId to re-find position in the feed. Unlike a Spotify episode id (permanent),
    // AudioBookshelf's own episode id is tied to a specific downloaded-episode row, which stops
    // existing the moment that download is deleted or the item is rescanned - after which nothing
    // can ever match LastTrackedEpisodeId against anything again. The feed guid survives that.
    public string AnchorGuid { get; set; }
}
