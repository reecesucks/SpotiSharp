namespace SpotiSharp.Models;

public static class RadioAudiobookshelfBackgroundService
{
    public static Action StartRequested { get; set; }
    public static Action StopRequested { get; set; }

    public static void Start() => StartRequested?.Invoke();
    public static void Stop() => StopRequested?.Invoke();
}
