namespace SpotiSharp.Models;

public class PlaybackStateStore
{
    private static PlaybackStateStore? _instance;
    public static PlaybackStateStore Instance => _instance ??= new PlaybackStateStore();

    public bool IsPlaying { get; private set; }
    public string? ActiveDeviceId { get; private set; }
    public string? CurrentItemUri { get; private set; }
    public int ProgressMs { get; private set; }
    public int DurationMs { get; private set; }

    public bool HasActiveDevice => !string.IsNullOrEmpty(ActiveDeviceId);

    private PlaybackStateStore() { }

    public void Update(bool isPlaying, string? activeDeviceId, string? currentItemUri, int progressMs, int durationMs)
    {
        IsPlaying = isPlaying;
        ActiveDeviceId = activeDeviceId;
        CurrentItemUri = currentItemUri;
        ProgressMs = progressMs;
        DurationMs = durationMs;
    }
}
