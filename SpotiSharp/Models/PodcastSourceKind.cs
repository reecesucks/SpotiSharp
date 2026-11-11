namespace SpotiSharp.Models;

/// <summary>Which playback engine a podcast segment's <see cref="IPodcastEpisodeSource"/> came from,
/// carried on <see cref="RadioAudiobookshelfItem"/> so <see cref="RadioAudiobookshelfConductor"/> can
/// route playback through the right <see cref="IPodcastPlaybackAdapter"/> without caring which engine
/// backs it. Spotify is the default (0) so radios cached before this field existed still deserialize
/// as Spotify-sourced.</summary>
public enum PodcastSourceKind
{
    Spotify = 0,
    Audiobookshelf = 1,
}
