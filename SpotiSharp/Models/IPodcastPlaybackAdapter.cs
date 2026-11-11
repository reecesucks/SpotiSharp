using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>Issues playback for a podcast-segment <see cref="RadioAudiobookshelfItem"/> through
/// whichever engine produced it, so <see cref="RadioAudiobookshelfConductor"/> can call through this
/// without caring whether the segment came from Spotify or AudioBookshelf.</summary>
internal interface IPodcastPlaybackAdapter
{
    PlaybackAttempt Play(RadioAudiobookshelfItem item);
}

/// <summary>Resolves the <see cref="IPodcastPlaybackAdapter"/> for a segment's <see cref="PodcastSourceKind"/>.</summary>
internal static class PodcastPlaybackAdapters
{
    private static readonly IPodcastPlaybackAdapter SpotifyAdapter = new SpotifyPodcastPlaybackAdapter();
    private static readonly IPodcastPlaybackAdapter AudiobookshelfAdapter = new AudiobookshelfPodcastPlaybackAdapter();

    internal static IPodcastPlaybackAdapter For(PodcastSourceKind kind) =>
        kind == PodcastSourceKind.Audiobookshelf ? AudiobookshelfAdapter : SpotifyAdapter;
}
