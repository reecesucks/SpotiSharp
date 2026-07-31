namespace SpotiSharp.Models;

public static class PlaybackCommands
{
    public static Action? Pause { get; set; }
    public static Action? Resume { get; set; }
    public static Action? SkipNext { get; set; }
    public static Action? SkipPrevious { get; set; }
    public static Action<bool>? SetShuffle { get; set; }
    public static Action? ToggleRepeat { get; set; }
}
