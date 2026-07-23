using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public class PlaybackStateStore
{
    private static PlaybackStateStore? _instance;
    public static PlaybackStateStore Instance => _instance ??= new PlaybackStateStore();

    private volatile PlaybackSnapshot _snapshot = PlaybackSnapshot.Empty;

    public PlaybackSnapshot Snapshot => _snapshot;

    public bool IsPlaying => _snapshot.IsPlaying;
    public string? ActiveDeviceId => _snapshot.ActiveDeviceId;
    public string? CurrentItemUri => _snapshot.CurrentItemUri;
    public int ProgressMs => _snapshot.ProgressMs;
    public int DurationMs => _snapshot.DurationMs;

    public bool HasActiveDevice => _snapshot.HasActiveDevice;

    private PlaybackStateStore() { }

    public void Update(bool isPlaying, string? activeDeviceId, string? currentItemUri, int progressMs, int durationMs)
    {
        _snapshot = new PlaybackSnapshot(isPlaying, activeDeviceId, currentItemUri, progressMs, durationMs);
    }
}
