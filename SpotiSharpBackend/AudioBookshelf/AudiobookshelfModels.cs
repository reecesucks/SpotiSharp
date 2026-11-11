using System.Text.Json.Serialization;

namespace SpotiSharpBackend.AudioBookshelf;

public class AudiobookshelfLibrary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;
}

internal class AudiobookshelfLibrariesResponse
{
    [JsonPropertyName("libraries")]
    public List<AudiobookshelfLibrary> Libraries { get; set; } = new();
}

internal class AudiobookshelfLibraryItemsResponse
{
    [JsonPropertyName("results")]
    public List<AudiobookshelfLibraryItem> Results { get; set; } = new();
}

public class AudiobookshelfLibraryItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("media")]
    public AudiobookshelfMedia? Media { get; set; }
}

public class AudiobookshelfMedia
{
    [JsonPropertyName("metadata")]
    public AudiobookshelfMediaMetadata? Metadata { get; set; }

    [JsonPropertyName("coverPath")]
    public string? CoverPath { get; set; }

    // Only populated when the library item is fetched expanded (?expanded=1).
    [JsonPropertyName("episodes")]
    public List<AudiobookshelfEpisodeDto>? Episodes { get; set; }
}

public class AudiobookshelfMediaMetadata
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    // The podcast's source RSS feed URL - only present on a podcast library item, used to pull the
    // full back-catalog (including episodes AudioBookshelf hasn't downloaded yet) via
    // AudiobookshelfApiClient.GetPodcastFeedEpisodesAsync, since media.episodes above only ever
    // holds episodes already downloaded to the server.
    [JsonPropertyName("feedUrl")]
    public string? FeedUrl { get; set; }
}

internal class AudiobookshelfFeedResponse
{
    [JsonPropertyName("podcast")]
    public AudiobookshelfFeedPodcastDto? Podcast { get; set; }
}

internal class AudiobookshelfFeedPodcastDto
{
    [JsonPropertyName("episodes")]
    public List<AudiobookshelfFeedEpisodeDto> Episodes { get; set; } = new();
}

/// <summary>One episode as it appears in the podcast's raw RSS feed (POST /api/podcasts/feed), which -
/// unlike <see cref="AudiobookshelfEpisodeDto"/> from the library item's media.episodes - includes every
/// episode the feed has ever published, downloaded or not. Captures unknown fields via
/// <see cref="Extra"/> so posting one of these back to POST /podcasts/:id/download-episodes (which needs
/// the object echoed back close to verbatim) doesn't silently drop something the server sent.</summary>
public class AudiobookshelfFeedEpisodeDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("pubDate")]
    public string? PubDate { get; set; }

    [JsonPropertyName("publishedAt")]
    public long? PublishedAtMs { get; set; }

    [JsonPropertyName("season")]
    public string? Season { get; set; }

    [JsonPropertyName("episode")]
    public string? Episode { get; set; }

    [JsonPropertyName("enclosure")]
    public AudiobookshelfEnclosureDto? Enclosure { get; set; }

    [JsonPropertyName("guid")]
    public string? Guid { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}

public class AudiobookshelfEpisodeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("pubDate")]
    public string? PubDate { get; set; }

    // The server's own already-parsed pubDate, as epoch milliseconds - more reliable than re-parsing
    // the raw pubDate string ourselves, since AudioBookshelf has already handled whatever RSS date
    // format the feed used. Null if the feed's pubDate didn't parse there either.
    [JsonPropertyName("publishedAt")]
    public long? PublishedAtMs { get; set; }

    [JsonPropertyName("season")]
    public string? Season { get; set; }

    [JsonPropertyName("episode")]
    public string? Episode { get; set; }

    [JsonPropertyName("duration")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("audioFile")]
    public AudiobookshelfAudioFile? AudioFile { get; set; }

    // Only present on episodes AudioBookshelf has not yet downloaded - the raw RSS enclosure/guid it
    // needs back verbatim in a POST /download-episodes request body to fetch that episode's audio.
    [JsonPropertyName("enclosure")]
    public AudiobookshelfEnclosureDto? Enclosure { get; set; }

    [JsonPropertyName("guid")]
    public string? Guid { get; set; }

    // Not populated by AudioBookshelf today for podcast episodes as far as we've seen, but the server
    // owner is planning to start tagging ad segments here (or similar) - fetched and cached alongside
    // the episode regardless so Radio can use it once that's in place, without another round of plumbing.
    [JsonPropertyName("chapters")]
    public List<AudiobookshelfChapterDto>? Chapters { get; set; }
}

public class AudiobookshelfAudioFile
{
    [JsonPropertyName("ino")]
    public string Ino { get; set; } = string.Empty;
}

public class AudiobookshelfEnclosureDto
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("length")]
    public string? Length { get; set; }
}

public class AudiobookshelfChapterDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    // The server sends these as strings ("645.2"), not JSON numbers - FlexibleDoubleConverter
    // parses either so a server that switches to numbers later doesn't break this.
    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double StartSeconds { get; set; }

    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double EndSeconds { get; set; }

    [JsonPropertyName("tags")]
    public AudiobookshelfChapterTagsDto? Tags { get; set; }
}

public class AudiobookshelfChapterTagsDto
{
    // "start" for ordinary content, "splice" marking a point where an ad was cut out of the
    // audio - see AudiobookshelfChapter for what Radio does with a "splice" tag.
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

internal class FlexibleDoubleConverter : JsonConverter<double>
{
    public override double Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            return double.TryParse(reader.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }
        return reader.GetDouble();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, double value, System.Text.Json.JsonSerializerOptions options) => writer.WriteNumberValue(value);
}

public class AudiobookshelfMediaProgress
{
    [JsonPropertyName("libraryItemId")]
    public string LibraryItemId { get; set; } = string.Empty;

    [JsonPropertyName("episodeId")]
    public string? EpisodeId { get; set; }

    [JsonPropertyName("currentTime")]
    public double CurrentTimeSeconds { get; set; }

    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

internal class AudiobookshelfMeResponse
{
    [JsonPropertyName("mediaProgress")]
    public List<AudiobookshelfMediaProgress> MediaProgress { get; set; } = new();
}

internal class AudiobookshelfProgressUpdate
{
    [JsonPropertyName("currentTime")]
    public double CurrentTime { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}
