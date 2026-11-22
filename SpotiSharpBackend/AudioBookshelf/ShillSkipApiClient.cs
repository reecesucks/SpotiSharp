using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpotiSharpBackend.AudioBookshelf;

/// <summary>One ad ShillSkip cut out of an episode, in both the original file's time (<see
/// cref="OriginalStart"/>/<see cref="OriginalEnd"/>) and as a point in the already-cut output file -
/// <see cref="SpliceAt"/> - where that ad used to be, i.e. where the radio can pause the episode,
/// play some music, and resume the episode at that same point with nothing lost.</summary>
public sealed record ShillSkipEpisodeCut(
    TimeSpan OriginalStart,
    TimeSpan OriginalEnd,
    TimeSpan SpliceAt,
    string Kind,
    double Confidence);

/// <summary>One episode's row from ShillSkip's own GET /episodes - every episode it has ever had a
/// job for, not capped to "recently released" the way <see cref="AudiobookshelfApiClient.GetAllPodcastsAsync"/>'s
/// per-show episode list is used. <see cref="LibraryItemId"/>/<see cref="EpisodeId"/> are AudioBookshelf's
/// own ids, so they line up directly with everything else in this namespace. <see cref="Cuts"/> is this
/// endpoint's own splice-point data - authoritative once <see cref="HasCuts"/> is true, no need to wait
/// on AudioBookshelf's own chapter tagging to catch up.</summary>
public sealed record ShillSkipEpisodeSummary(
    string EpisodeId,
    string LibraryItemId,
    string LibraryId,
    string Title,
    string PodcastTitle,
    DateTimeOffset? PublishedAt,
    string Status,
    int AdsDetected,
    int AdsRemoved,
    bool HasCuts,
    bool Started,
    bool Finished,
    IReadOnlyList<ShillSkipEpisodeCut> Cuts,
    // Natural pauses (e.g. the show's own intro/outro stings) ShillSkip noticed in the audio -
    // unrelated to ad removal, so these exist even on an episode with no Cuts at all. Just
    // candidate points; RadioAudiobookshelfModel only uses the ones that land close to the
    // configured segment length, same normalization the ad-cut splice points get.
    IReadOnlyList<TimeSpan>? MusicBreaks = null);

/// <summary>Thin client for ShillSkip's loopback-only GET /episodes, reached here through the same
/// Cloudflare tunnel as the AudioBookshelf server. Entirely separate from <see cref="AudiobookshelfApiClient"/> -
/// ShillSkip tracks its own ad-processing jobs, not AudioBookshelf's catalog, and this is read-only.</summary>
public static class ShillSkipApiClient
{
    private const string BaseUrl = "https://shillskip.reecesucks.com/";

    // Temporary - ShillSkip gates /episodes with this shared key (header X-Status-Key) instead of a
    // loopback check, since cloudflared's own connection to the port looks like loopback too.
    private const string EpisodesApiKey = "testauth";

    private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>Every episode ShillSkip knows about, optionally limited to ones nobody has started
    /// listening to yet. Never throws - an unreachable ShillSkip (its own tunnel down, the Mac asleep)
    /// shouldn't take the AudioBookshelf episode cache down with it; callers fall back when this comes
    /// back empty.</summary>
    public static async Task<List<ShillSkipEpisodeSummary>> GetEpisodesAsync(bool? unplayed = null)
    {
        try
        {
            var query = unplayed.HasValue ? $"episodes?unplayed={(unplayed.Value ? "true" : "false")}" : "episodes";
            using var request = new HttpRequestMessage(HttpMethod.Get, query);
            request.Headers.Add("X-Status-Key", EpisodesApiKey);

            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            var episodes = await JsonSerializer.DeserializeAsync<List<ShillSkipEpisodeSummary>>(stream, JsonOptions);
            return episodes ?? new List<ShillSkipEpisodeSummary>();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[ShillSkip] GET /episodes failed: {ex.Message}");
            return new List<ShillSkipEpisodeSummary>();
        }
    }
}
