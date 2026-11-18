namespace SpotiSharpBackend.Radio;

public static class RadioTuning
{
    public const int SEGMENT_LENGTH_MS = 15 * 60 * 1000;

    public const int END_TOLERANCE_MS = 2500;

    public const int RESUME_REWIND_MS = 30000;

    public const int START_GRACE_MS = 12000;

    public const int DEAD_AIR_TIMEOUT_MS = 30000;

    public const int MAX_START_ATTEMPTS = 3;
    public const int MAX_UNAVAILABLE_SKIPS = 10;

    public const int MAX_PLAYTHROUGH_PROJECTION_MS = 2 * 60 * 1000;

    public const int SNAPSHOT_STALE_MS = 10000;

    public const int SKIP_LANDING_MS = 5000;

    // App Remote has no "clear queue" call, so emptying Spotify's real queue before setting a
    // fresh one means skipping forward through everything we believe is still in there. Each skip
    // needs a moment to land before the next is sent, or Spotify can drop a command fired too
    // close behind the last one.
    public const int QUEUE_FLUSH_SKIP_DELAY_MS = 300;
    public const int MAX_QUEUE_FLUSH_SKIPS = 50;

    /// <summary>
    /// How long Spotify has to stay on another radio song, after leaving ours part way through,
    /// before the radio takes it as the listener's choice and stops. One stray stale report
    /// shouldn't end the radio.
    /// </summary>
    public const int TAKEOVER_CONFIRM_MS = 3000;

    /// <summary>
    /// How long an AudioBookshelf segment's playback position can sit unchanged (not paused by the
    /// listener) before it's treated as a silently stalled stream rather than normal buffering.
    /// </summary>
    public const int AB_STALL_THRESHOLD_MS = 20000;

    public const int MAX_AB_STALL_RETRIES = 3;
}
