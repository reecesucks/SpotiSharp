namespace SpotiSharpBackend.Radio;

public static class RadioTuning
{
    public const int SEGMENT_LENGTH_MS = 15 * 60 * 1000;

    public const int END_TOLERANCE_MS = 2500;

    public const int RESUME_REWIND_MS = 10000;

    public const int START_GRACE_MS = 12000;

    public const int DEAD_AIR_TIMEOUT_MS = 30000;

    public const int MAX_START_ATTEMPTS = 3;
    public const int MAX_UNAVAILABLE_SKIPS = 10;
}
