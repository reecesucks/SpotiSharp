namespace SpotiSharp.Models;

public static class PlaybackCommands
{
    public static Action? Pause { get; set; }
    public static Action? Resume { get; set; }
    public static Action? SkipNext { get; set; }
    public static Action? SkipPrevious { get; set; }
    public static Action<bool>? SetShuffle { get; set; }
    public static Action? ToggleRepeat { get; set; }

    // Wakes Spotify in the background (no UI) if it isn't already connected, resolving true once
    // connected or false on failure/timeout. Replaces deep-linking into Spotify's visible UI just
    // to make it register as a Connect device. Null on platforms with no App Remote connector.
    public static Func<Task<bool>>? WakeSpotify { get; set; }
}
