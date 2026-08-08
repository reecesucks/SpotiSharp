namespace SpotiSharp.Models;

public static class PlaybackCommands
{
    public static Action? Pause { get; set; }

    public static Action? Resume { get; set; }

    public static Action? SkipNext { get; set; }

    public static Action? SkipPrevious { get; set; }

    public static Action<bool>? SetShuffle { get; set; }

    public static Action? ToggleRepeat { get; set; }

    public static Action<int>? SeekTo { get; set; }

    public static Func<Task<bool>>? WakeSpotify { get; set; }

    public static Func<string, Task<bool>>? PlayUri { get; set; }

    public static Func<string, Task<bool>>? QueueUri { get; set; }
}
