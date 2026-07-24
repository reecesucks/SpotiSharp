namespace SpotiSharpBackend.Radio;

public sealed record PlaybackSnapshot(
    bool IsPlaying,
    string? ActiveDeviceId,
    string? CurrentItemUri,
    int ProgressMs,
    int DurationMs,
    bool ShuffleOn = false)
{
    public static readonly PlaybackSnapshot Empty = new PlaybackSnapshot(false, null, null, 0, 0);

    public bool HasActiveDevice => !string.IsNullOrEmpty(ActiveDeviceId);
}
