using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;

/// <summary>Routes an AudioBookshelf-sourced segment to <see cref="AudiobookshelfPlaybackCommands"/>
/// instead of Spotify's App Remote/Web API - it needs a stream URL + auth headers, not a Spotify URI.
/// <see cref="RadioAudiobookshelfItem"/> only carries <see cref="RadioAudiobookshelfItem.PlayUri"/>
/// (the synthetic "audiobookshelf:episode:{id}" key <see cref="AudiobookshelfEpisode"/> hands out as
/// <see cref="IPodcastEpisodeSource.PlayUri"/>), so this re-resolves the real episode - metadata,
/// download state, stream URL - from that id at play time, the same way
/// <see cref="AudiobookshelfPodcastsViewModel"/> does for a direct tap.</summary>
internal class AudiobookshelfPodcastPlaybackAdapter : IPodcastPlaybackAdapter
{
    private const string UriPrefix = "audiobookshelf:episode:";

    public PlaybackAttempt Play(RadioAudiobookshelfItem item)
    {
        if (AudiobookshelfPlaybackCommands.Load == null) return PlaybackAttempt.Failed;

        var episodeId = ParseEpisodeId(item.PlayUri);
        if (episodeId == null)
        {
            DiagnosticLog.Write($"[Radio] AudioBookshelf segment has an unrecognised PlayUri: {item.PlayUri}");
            return PlaybackAttempt.Failed;
        }

        var episode = (AudiobookshelfPodcastsModel.GetDiskCachedEpisodes() ?? new List<AudiobookshelfEpisode>())
            .FirstOrDefault(candidate => candidate.EpisodeId == episodeId);
        if (episode == null)
        {
            DiagnosticLog.Write($"[Radio] AudioBookshelf episode {episodeId} isn't in the cached episode list, can't play it");
            return PlaybackAttempt.Failed;
        }

        var nowPlayingInfo = new AudiobookshelfNowPlayingInfo(episode.EpisodeName, episode.ShowName, episode.ShowCoverUrl);

        var localPath = AudiobookshelfDownloadsModel.GetLocalPath(episode.EpisodeId);
        if (localPath != null)
        {
            DiagnosticLog.Write($"[Radio] playing AudioBookshelf segment {episodeId}@{item.PositionMs}ms from downloaded file: {localPath}");
            AudiobookshelfPlaybackCommands.Load(localPath, new Dictionary<string, string>(), nowPlayingInfo, item.PositionMs);
        }
        else
        {
            var streamUrl = AudiobookshelfApiClient.BuildStreamUrl(episode.LibraryItemId, episode.AudioFileIno);
            DiagnosticLog.Write($"[Radio] playing AudioBookshelf segment {episodeId}@{item.PositionMs}ms by streaming: {streamUrl}");
            AudiobookshelfPlaybackCommands.Load(streamUrl, AudiobookshelfApiClient.BuildAuthHeaders(), nowPlayingInfo, item.PositionMs);
        }

        return PlaybackAttempt.Success;
    }

    internal static string? ParseEpisodeId(string? playUri) =>
        playUri != null && playUri.StartsWith(UriPrefix, StringComparison.Ordinal) ? playUri.Substring(UriPrefix.Length) : null;
}
