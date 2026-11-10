using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SpotiSharpBackend.AudioBookshelf;

/// <summary>
/// Thin REST client for a self-hosted AudioBookshelf server. Entirely separate from
/// <see cref="APICaller"/> (Spotify) by design — this is the start of the AudioBookshelf
/// podcast path that will eventually replace Spotify podcasts, not a shared abstraction.
/// Reads the server URL/API key fresh from <see cref="StorageHandler"/> on every call so
/// a settings change takes effect immediately without reconnecting anything.
/// </summary>
public static class AudiobookshelfApiClient
{
    private const string PODCAST_MEDIA_TYPE = "podcast";

    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    // Episode downloads are hour-plus audio files - the 15s timeout above is for quick API calls,
    // not for a body that can legitimately take minutes to pull over the network.
    private static readonly HttpClient DownloadHttp = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(StorageHandler.AudiobookshelfServerUrl) &&
        !string.IsNullOrWhiteSpace(StorageHandler.AudiobookshelfApiKey);

    /// <summary>Hits /api/libraries to confirm the server URL and API key work. Returns an error message, or null on success.</summary>
    public static async Task<string?> TestConnectionAsync()
    {
        try
        {
            await GetLibrariesAsync();
            return null;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Audiobookshelf] connection test failed: {ex.Message}");
            return ex.Message;
        }
    }

    public static async Task<List<AudiobookshelfLibrary>> GetLibrariesAsync()
    {
        var response = await SendAsync<AudiobookshelfLibrariesResponse>("api/libraries");
        return response?.Libraries ?? new List<AudiobookshelfLibrary>();
    }

    /// <summary>All podcast library items (shows) across every podcast-type library on the server.</summary>
    public static async Task<List<AudiobookshelfLibraryItem>> GetAllPodcastsAsync()
    {
        var result = new List<AudiobookshelfLibraryItem>();
        var libraries = await GetLibrariesAsync();

        foreach (var library in libraries.Where(library => library.MediaType == PODCAST_MEDIA_TYPE))
        {
            var items = await SendAsync<AudiobookshelfLibraryItemsResponse>($"api/libraries/{library.Id}/items");
            if (items?.Results != null) result.AddRange(items.Results);
        }

        return result;
    }

    /// <summary>Episodes for one podcast library item, fetched expanded so <c>media.episodes</c> is populated.
    /// Includes episodes AudioBookshelf already knows about from the feed but has not downloaded yet
    /// (no <see cref="AudiobookshelfEpisodeDto.AudioFile"/>) - that is what the binge-ahead downloader
    /// (<see cref="SpotiSharp.Models.RadioAudiobookshelfBingeAheadModel"/>) uses to find what's missing.</summary>
    public static async Task<List<AudiobookshelfEpisodeDto>> GetEpisodesAsync(string libraryItemId)
    {
        var item = await SendAsync<AudiobookshelfLibraryItem>($"api/items/{libraryItemId}?expanded=1");
        return item?.Media?.Episodes ?? new List<AudiobookshelfEpisodeDto>();
    }

    /// <summary>Queues the given episodes for download on the server (AudioBookshelf's own
    /// POST /podcasts/:id/download-episodes - the episode objects must be the same ones the server handed
    /// back from <see cref="GetEpisodesAsync"/>, enclosure/guid included, since that's what it uses to
    /// fetch the audio). Fire-and-forget on the server side - this call just returns once it's queued,
    /// not once the file is actually downloaded. Never throws - returns whether the request was accepted.</summary>
    public static async Task<bool> DownloadEpisodesAsync(string libraryItemId, IReadOnlyList<AudiobookshelfEpisodeDto> episodes)
    {
        if (episodes == null || episodes.Count == 0) return false;

        try
        {
            await SendPostAsync($"api/podcasts/{libraryItemId}/download-episodes", episodes);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Audiobookshelf] download-episodes request failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Resume positions for the current API-key user, keyed by (libraryItemId, episodeId).</summary>
    public static async Task<List<AudiobookshelfMediaProgress>> GetMediaProgressAsync()
    {
        var me = await SendAsync<AudiobookshelfMeResponse>("api/me");
        return me?.MediaProgress ?? new List<AudiobookshelfMediaProgress>();
    }

    /// <summary>The direct-file stream URL for one episode's audio. Auth is sent as a header (<see cref="BuildAuthHeaders"/>),
    /// not a query token, since this is for Media3/ExoPlayer, which can set headers freely.</summary>
    public static string BuildStreamUrl(string libraryItemId, string audioFileIno)
    {
        var serverUrl = StorageHandler.AudiobookshelfServerUrl.TrimEnd('/');
        return $"{serverUrl}/api/items/{libraryItemId}/file/{audioFileIno}";
    }

    public static IReadOnlyDictionary<string, string> BuildAuthHeaders()
    {
        return new Dictionary<string, string> { ["Authorization"] = $"Bearer {StorageHandler.AudiobookshelfApiKey}" };
    }

    /// <summary>Saves playback position back to the server, keyed by (libraryItemId, episodeId) same as
    /// <see cref="GetMediaProgressAsync"/> reads it. Never throws - returns whether it succeeded, so a
    /// caller (see <see cref="SpotiSharp.Models.AudiobookshelfProgressSyncModel"/>) can queue a failed
    /// save (e.g. offline) and retry it later instead of losing it.</summary>
    public static async Task<bool> SaveProgressAsync(string libraryItemId, string episodeId, int currentPositionMs, int durationMs)
    {
        try
        {
            var payload = new AudiobookshelfProgressUpdate
            {
                CurrentTime = currentPositionMs / 1000.0,
                Duration = durationMs > 0 ? durationMs / 1000.0 : null
            };
            await SendPatchAsync($"api/me/progress/{libraryItemId}/{episodeId}", payload);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Audiobookshelf] progress save failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Downloads one episode's audio file straight to disk for offline playback, streaming the
    /// response instead of buffering it in memory - these are hour-plus audio files. If
    /// <paramref name="resumeFromByte"/> is positive, asks the server for just the remainder via an HTTP
    /// Range request and appends to the existing file, so a connection drop partway through a 2-hour
    /// file doesn't mean starting over. Falls back to a full restart if the server doesn't honor Range
    /// (replies 200 instead of 206). <paramref name="onProgress"/>, if given, is called periodically with
    /// (bytes downloaded overall, total bytes) - total is null if the server didn't say.</summary>
    public static async Task DownloadEpisodeFileAsync(string libraryItemId, string audioFileIno, string destinationPath, long resumeFromByte = 0,
        Action<long, long?>? onProgress = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BuildStreamUrl(libraryItemId, audioFileIno)}");
        AddAuthHeader(request);
        if (resumeFromByte > 0) request.Headers.Range = new RangeHeaderValue(resumeFromByte, null);

        using var response = await DownloadHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var resumed = resumeFromByte > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
        if (resumeFromByte > 0 && !resumed)
            DiagnosticLog.Write("[Audiobookshelf] server did not honor the resume request - restarting download from the beginning");

        // The total content length is either the whole file (fresh download) or, on a successful
        // resume, just the remainder - either way, add what's already on disk to get the true total.
        var totalBytes = response.Content.Headers.ContentLength.HasValue
            ? response.Content.Headers.ContentLength.Value + (resumed ? resumeFromByte : 0)
            : (long?)null;
        var bytesSoFar = resumed ? resumeFromByte : 0;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(destinationPath, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesSoFar += bytesRead;
            onProgress?.Invoke(bytesSoFar, totalBytes);
        }
    }

    private static async Task<T?> SendAsync<T>(string relativePath) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(relativePath));
        AddAuthHeader(request);

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    private static async Task SendPatchAsync<T>(string relativePath, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, BuildUri(relativePath));
        AddAuthHeader(request);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SendPostAsync<T>(string relativePath, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(relativePath));
        AddAuthHeader(request);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static Uri BuildUri(string relativePath)
    {
        var serverUrl = StorageHandler.AudiobookshelfServerUrl;
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException("AudioBookshelf server URL is not configured.");

        var baseUri = serverUrl.EndsWith('/') ? serverUrl : serverUrl + "/";
        return new Uri(new Uri(baseUri), relativePath);
    }

    private static void AddAuthHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(StorageHandler.AudiobookshelfApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", StorageHandler.AudiobookshelfApiKey);
    }
}
