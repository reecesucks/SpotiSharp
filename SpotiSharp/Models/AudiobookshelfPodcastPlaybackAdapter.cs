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

    public PlaybackAttempt Play(RadioAudiobookshelfItem item, int? resumePositionMs = null)
    {
        if (AudiobookshelfPlaybackCommands.Load == null) return PlaybackAttempt.Failed;

        var episodeId = ParseEpisodeId(item.PlayUri);
        if (episodeId == null)
        {
            DiagnosticLog.Write($"[Radio] AudioBookshelf segment has an unrecognised PlayUri: {item.PlayUri}");
            return PlaybackAttempt.Failed;
        }

        var episode = AudiobookshelfPodcastsModel.FindCachedEpisode(episodeId);
        if (episode == null)
        {
            DiagnosticLog.Write($"[Radio] AudioBookshelf episode {episodeId} isn't in the cached episode list, can't play it");
            return PlaybackAttempt.Failed;
        }

        var nowPlayingInfo = new AudiobookshelfNowPlayingInfo(episode.EpisodeName, episode.ShowName, episode.ShowCoverUrl);
        var startPositionMs = resumePositionMs ?? item.PositionMs;

        var localPath = AudiobookshelfDownloadsModel.GetLocalPath(episode.EpisodeId);
        if (localPath != null)
        {
            DiagnosticLog.Write($"[Radio] playing AudioBookshelf segment {episodeId}@{startPositionMs}ms from downloaded file: {localPath}");
            AudiobookshelfPlaybackCommands.Load(localPath, new Dictionary<string, string>(), nowPlayingInfo, startPositionMs);
        }
        else
        {
            var streamUrl = AudiobookshelfApiClient.BuildStreamUrl(episode.LibraryItemId, episode.AudioFileIno);
            DiagnosticLog.Write($"[Radio] playing AudioBookshelf segment {episodeId}@{startPositionMs}ms by streaming: {streamUrl}");
            AudiobookshelfPlaybackCommands.Load(streamUrl, AudiobookshelfApiClient.BuildAuthHeaders(), nowPlayingInfo, startPositionMs);

            // The cached ino above can be stale (AudioBookshelf replaced the episode's underlying file
            // mid-session - e.g. a server-side chapter/ad-splice tool reprocessing it - which the
            // once-per-session disk cache never sees). Checking live and re-issuing Load if it changed
            // happens off the UI thread: this call path runs synchronously from the tap gesture, and an
            // earlier version that checked live inline with a blocking network call froze input long
            // enough for Android to ANR-kill the app.
            RetryWithLiveInoIfStale(episode, startPositionMs, nowPlayingInfo, episode.AudioFileIno);
        }

        // Nothing else tells the player bar this is now an AudioBookshelf episode - without this,
        // PlayerBarViewModel.IsPodcastSource never flips on for Radio-driven playback, and the podcast
        // progress bar stays hidden even while an AB segment is actually playing.
        SpotiSharp.ViewModels.PlayerBarViewModel.Instance.ApplyAudiobookshelfState(
            episode.EpisodeId, episode.EpisodeName, episode.ShowCoverUrl, isPlaying: true, startPositionMs, episode.DurationMs);

        return PlaybackAttempt.Success;
    }

    internal static string? ParseEpisodeId(string? playUri) =>
        playUri != null && playUri.StartsWith(UriPrefix, StringComparison.Ordinal) ? playUri.Substring(UriPrefix.Length) : null;

    private static void RetryWithLiveInoIfStale(AudiobookshelfEpisode episode, int positionMs, AudiobookshelfNowPlayingInfo nowPlayingInfo, string playedIno)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var episodes = await AudiobookshelfApiClient.GetEpisodesAsync(episode.LibraryItemId);
                var liveIno = episodes.FirstOrDefault(candidate => candidate.Id == episode.EpisodeId)?.AudioFile?.Ino;
                if (string.IsNullOrEmpty(liveIno) || liveIno == playedIno) return;

                var streamUrl = AudiobookshelfApiClient.BuildStreamUrl(episode.LibraryItemId, liveIno);
                DiagnosticLog.Write($"[Radio] AudioBookshelf episode {episode.EpisodeId}'s cached file reference was stale (was {playedIno}, now {liveIno}) - reloading: {streamUrl}");
                AudiobookshelfPlaybackCommands.Load?.Invoke(streamUrl, AudiobookshelfApiClient.BuildAuthHeaders(), nowPlayingInfo, positionMs);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Radio] live AudioBookshelf file check failed, keeping the cached stream: {ex.Message}");
            }
        });
    }
}
