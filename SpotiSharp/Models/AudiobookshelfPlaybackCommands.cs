namespace SpotiSharp.Models;

/// <summary>
/// Cross-platform hook into the Android-only Media3/ExoPlayer wrapper (<c>AudiobookshelfPlayer</c>
/// under Platforms/Android), mirroring how <see cref="PlaybackCommands"/> bridges to the Spotify
/// App Remote SDK. Deliberately its own bag, not reusing <see cref="PlaybackCommands"/> — nothing
/// here is wired into Radio/Spotify yet.
/// </summary>
/// <summary>Display metadata for the lock-screen/notification media controls — separate from the
/// stream URL/headers since those are plumbing, not something to show the user.</summary>
public readonly record struct AudiobookshelfNowPlayingInfo(string Title, string ShowName, string ArtworkUrl);

public static class AudiobookshelfPlaybackCommands
{
    /// <summary>Loads and starts playing a stream URL with the given HTTP request headers (e.g. Authorization),
    /// seeking to startPositionMs first (0 for "from the beginning").</summary>
    public static Action<string, IReadOnlyDictionary<string, string>, AudiobookshelfNowPlayingInfo, int>? Load { get; set; }

    public static Action? Play { get; set; }
    public static Action? Pause { get; set; }
    public static Action<int>? SeekTo { get; set; }

    public static Func<int>? GetPositionMs { get; set; }
    public static Func<int>? GetDurationMs { get; set; }
    public static Func<bool>? GetIsPlaying { get; set; }

    /// <summary>Set by the player whenever <see cref="Pause"/> was called from the user tapping pause
    /// (the player bar or the lock-screen/notification controls) rather than from Radio's own Load/Play
    /// flow - so a tick seeing "not playing" can tell a listener's deliberate pause from a silent stall
    /// and only treat the latter as something to recover from.</summary>
    public static bool UserInitiatedPause { get; set; }
}
