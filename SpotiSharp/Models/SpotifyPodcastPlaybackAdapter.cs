using SpotiSharpBackend;

namespace SpotiSharp.Models;

/// <summary>The default podcast playback adapter - exactly what <see cref="RadioAudiobookshelfConductor"/>
/// did inline before the engine became pluggable: App Remote first, falling back to the Web API. Moved
/// here verbatim so the conductor can call through <see cref="IPodcastPlaybackAdapter"/> for every
/// podcast segment regardless of source, without changing Spotify's behavior.</summary>
internal class SpotifyPodcastPlaybackAdapter : IPodcastPlaybackAdapter
{
    public PlaybackAttempt Play(RadioAudiobookshelfItem item)
    {
        if (PlaybackCommands.PlayUri != null)
        {
            _ = RadioAudiobookshelfConductor.PlayViaAppRemoteAsync(item, null);
            return PlaybackAttempt.Success;
        }

        DiagnosticLog.Write("[Radio] App Remote path unavailable, falling back to Web API");
        return RadioAudiobookshelfConductor.IssueViaWebApi(item, new List<string>());
    }
}
