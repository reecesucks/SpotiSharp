namespace SpotiSharp.Models;

/// <summary>What <see cref="RadioAudiobookshelfItem.ForPodcastSegment"/> needs from a podcast episode,
/// regardless of where it came from - lets the radio build a podcast-segment queue item from either
/// Spotify's <see cref="RecentEpisode"/> or an AudioBookshelf <see cref="AudiobookshelfEpisode"/> without
/// RadioAudiobookshelfItem/RadioAudiobookshelfModel knowing which. <see cref="PlayUri"/> is still a Spotify
/// URI concept (it's what <see cref="AppRemotePlayback"/>/<see cref="SpotifyQueueMirror"/> play) - swapping
/// the episode source over to AudioBookshelf will also need the playback side to route AudioBookshelf
/// items to <see cref="AudiobookshelfPlaybackCommands"/> instead, which this interface alone doesn't cover.
/// <see cref="PodcastEpisodeSourceConverter"/> lets a value typed as this interface (the "More episodes"
/// picker, "up next") round-trip through <see cref="SpotiSharp.Helpers.DiskCacheHelper"/> as whichever
/// concrete type it actually is.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(PodcastEpisodeSourceConverter))]
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

/// <summary>Dispatches an <see cref="IPodcastEpisodeSource"/>-typed cache entry (the "more episodes"
/// list, the "up next" picks) to whichever concrete type it actually holds - "LibraryItemId" only
/// ever appears on an <see cref="AudiobookshelfEpisode"/>, never on a <see cref="RecentEpisode"/>, so
/// a cache file saved before AudioBookshelf episodes could appear there (plain RecentEpisode shape,
/// no type tag at all) still reads back correctly as one.</summary>
internal class PodcastEpisodeSourceConverter : System.Text.Json.Serialization.JsonConverter<IPodcastEpisodeSource>
{
    public override IPodcastEpisodeSource Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        using var doc = System.Text.Json.JsonDocument.ParseValue(ref reader);
        var json = doc.RootElement.GetRawText();
        return doc.RootElement.TryGetProperty("LibraryItemId", out _)
            ? System.Text.Json.JsonSerializer.Deserialize<AudiobookshelfEpisode>(json, options)
            : System.Text.Json.JsonSerializer.Deserialize<RecentEpisode>(json, options);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, IPodcastEpisodeSource value, System.Text.Json.JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
