namespace SpotiSharp.Models;

/// <summary>What <see cref="RadioAudiobookshelfItem.ForPodcastSegment"/> needs from a podcast episode,
/// regardless of where it came from - lets the radio build a podcast-segment queue item from either
/// Spotify's <see cref="RecentEpisode"/> or an AudioBookshelf <see cref="AudiobookshelfEpisode"/> without
/// RadioAudiobookshelfItem/RadioAudiobookshelfModel knowing which. <see cref="PlayUri"/> is still a Spotify
/// URI concept (it's what <see cref="AppRemotePlayback"/>/<see cref="SpotifyQueueMirror"/> play) - swapping
/// the episode source over to AudioBookshelf will also need the playback side to route AudioBookshelf
/// items to <see cref="AudiobookshelfPlaybackCommands"/> instead, which this interface alone doesn't cover.</summary>
public interface IPodcastEpisodeSource
{
    string EpisodeId { get; }
    string EpisodeName { get; }
    string ShowName { get; }
    string ShowImageUrl { get; }
    DateTime ReleaseDate { get; }
    int DurationMs { get; }
    int ResumePositionMs { get; }
    string PlayUri { get; }

    /// <summary>Which engine this episode plays through, so <see cref="RadioAudiobookshelfConductor"/>
    /// can route via the matching <see cref="IPodcastPlaybackAdapter"/>.</summary>
    PodcastSourceKind SourceKind { get; }

    /// <summary>Ad-removal splice points (ms from the start), ascending. Empty for a source with no
    /// such metadata (every Spotify episode, and an AudioBookshelf one the server hasn't tagged) - in
    /// which case <see cref="RadioAudiobookshelfModel"/> segments the episode the old way, on a fixed
    /// timer, instead of at these boundaries.</summary>
    IReadOnlyList<int> SpliceStartMs { get; }
}
