using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public class PlaybackStateStore
{
    private static PlaybackStateStore? _instance;
    public static PlaybackStateStore Instance => _instance ??= new PlaybackStateStore();

    private volatile PlaybackSnapshot _snapshot = PlaybackSnapshot.Empty;

    private long _freshAtUtcTicks;

    public PlaybackSnapshot Snapshot => _snapshot;

    public bool IsFresh(TimeSpan maxAge) =>
        DateTime.UtcNow.Ticks - Interlocked.Read(ref _freshAtUtcTicks) <= maxAge.Ticks;

    public bool IsPlaying => _snapshot.IsPlaying;
    public string? ActiveDeviceId => _snapshot.ActiveDeviceId;
    public string? CurrentItemUri => _snapshot.CurrentItemUri;
    public int ProgressMs => _snapshot.ProgressMs;
    public int DurationMs => _snapshot.DurationMs;
    public bool ShuffleOn => _snapshot.ShuffleOn;

    public bool HasActiveDevice => _snapshot.HasActiveDevice;

    public static Func<bool>? HasActivePushSource { get; set; }

    private PlaybackStateStore() { }

    public void Update(bool isPlaying, string? activeDeviceId, string? currentItemUri, int progressMs, int durationMs, bool shuffleOn)
    {
        _snapshot = new PlaybackSnapshot(isPlaying, activeDeviceId, currentItemUri, progressMs, durationMs, shuffleOn);
        Interlocked.Exchange(ref _freshAtUtcTicks, DateTime.UtcNow.Ticks);
    }

    public void UpdateActiveDeviceId(string? activeDeviceId)
    {
        if (string.IsNullOrEmpty(activeDeviceId) || activeDeviceId == _snapshot.ActiveDeviceId) return;
        _snapshot = _snapshot with { ActiveDeviceId = activeDeviceId };
    }
}
