namespace SpotiSharpBackend;

/// <summary>
/// How far through an episode the user is, as Spotify currently reports it.
/// <see cref="FullyPlayed"/> is Spotify's own "done" flag and is the only definitive
/// signal - a resume position alone cannot distinguish "finished" from "never started",
/// because Spotify resets the position once an episode completes.
/// </summary>
public class EpisodeProgress
{
    public int ResumePositionMs { get; }
    public bool FullyPlayed { get; }

    public EpisodeProgress(int resumePositionMs, bool fullyPlayed)
    {
        ResumePositionMs = resumePositionMs;
        FullyPlayed = fullyPlayed;
    }
}
