using SpotifyAPI.Web;

namespace SpotiSharp.Helpers;

public static class EpisodeHelper
{
    private const double UNPLAYED_THRESHOLD = 0.05;

    public static bool IsListened(SimpleEpisode episode, double unplayedThreshold = UNPLAYED_THRESHOLD)
    {
        return episode.ResumePoint.FullyPlayed || (episode.DurationMs - episode.ResumePoint.ResumePositionMs) <= episode.DurationMs * unplayedThreshold;
    }
}
