using SpotifyAPI.Web;
using SpotiSharpBackend;

namespace SpotiSharp.Helpers;

public static class EpisodeHelper
{
    private const double UNPLAYED_THRESHOLD = 0.05;

    public static bool IsListened(SimpleEpisode episode, double unplayedThreshold = UNPLAYED_THRESHOLD)
    {
        if (episode?.ResumePoint == null) return false;

        return IsListened(episode.DurationMs, episode.ResumePoint.ResumePositionMs, episode.ResumePoint.FullyPlayed, unplayedThreshold);
    }

    public static bool IsListened(EpisodeProgress progress, int durationMs, double unplayedThreshold = UNPLAYED_THRESHOLD)
    {
        if (progress == null) return false;

        return IsListened(durationMs, progress.ResumePositionMs, progress.FullyPlayed, unplayedThreshold);
    }

    private static bool IsListened(int durationMs, int resumePositionMs, bool fullyPlayed, double unplayedThreshold)
    {
        return fullyPlayed || (durationMs - resumePositionMs) <= durationMs * unplayedThreshold;
    }
}
