namespace SpotiSharp.Models;

public static class PlaybackCommands
{
    public static Action? Pause { get; set; }

    public static Action? Resume { get; set; }

    public static Action? SkipNext { get; set; }

    /// <summary>Same native "skip next" as <see cref="SkipNext"/>, but reporting whether it actually
    /// landed - used only by the queue-flush path (<see cref="AppRemotePlayback"/>), which needs to
    /// know when Spotify has nothing left to skip past so it can stop instead of blindly sending the
    /// rest of a 50-skip flush.</summary>
    public static Func<Task<bool>>? SkipNextAsync { get; set; }

    public static Action? SkipPrevious { get; set; }

    public static Action<bool>? SetShuffle { get; set; }

    public static Action? ToggleRepeat { get; set; }

    public static Action<int>? SeekTo { get; set; }

    public static Func<Task<bool>>? WakeSpotify { get; set; }

    public static Func<string, Task<bool>>? PlayUri { get; set; }

    public static Func<string, Task<bool>>? QueueUri { get; set; }
}
