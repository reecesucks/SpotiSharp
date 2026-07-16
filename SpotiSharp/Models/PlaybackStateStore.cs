namespace SpotiSharp.Models;

public class PlaybackStateStore
{
    private static PlaybackStateStore? _instance;
    public static PlaybackStateStore Instance => _instance ??= new PlaybackStateStore();

    public bool IsPlaying { get; private set; }
    public string? ActiveDeviceId { get; private set; }

    public bool HasActiveDevice => !string.IsNullOrEmpty(ActiveDeviceId);

    private PlaybackStateStore() { }

    public void Update(bool isPlaying, string? activeDeviceId)
    {
        IsPlaying = isPlaying;
        ActiveDeviceId = activeDeviceId;
    }
}
