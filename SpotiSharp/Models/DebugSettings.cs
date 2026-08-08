namespace SpotiSharp.Models;

public static class DebugSettings
{
    private const string ShowSegmentTimerKey = "debug_show_segment_timer";

    public static bool ShowSegmentTimer
    {
        get => Preferences.Default.Get(ShowSegmentTimerKey, false);
        set => Preferences.Default.Set(ShowSegmentTimerKey, value);
    }
}
