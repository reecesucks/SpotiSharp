namespace SpotiSharp.Models;

/// <summary>Cross-platform hook into the Android-only foreground service that keeps the app process
/// alive while an episode download is in flight - mirrors <see cref="RadioBackgroundService"/>'s role
/// for Spotify radio. Without this, a download running as a plain background task gets killed along
/// with the whole process once the app is backgrounded for a while, silently losing progress (the
/// partial file survives and resumes fine, but the user sees no indication anything was happening).</summary>
public static class AudiobookshelfDownloadBackgroundService
{
    public static Action? StartRequested { get; set; }
    public static Action? StopRequested { get; set; }

    public static void Start() => StartRequested?.Invoke();
    public static void Stop() => StopRequested?.Invoke();
}
