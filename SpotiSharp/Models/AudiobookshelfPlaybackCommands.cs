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

    /// <summary>Starts the foreground service (if it isn't already running) so it holds its Doze/
    /// background-network exemption for the rest of the radio session - call this from any direct,
    /// guaranteed-foreground user action that begins playing the radio (tapping any item, not just a
    /// podcast one), the same moment Spotify's own app already has its own service running from. A
    /// podcast segment reached later, possibly while backgrounded, then needs no start of its own -
    /// see <c>AudiobookshelfPlayer.Load</c>, which still calls this too, but only as a no-op by then.</summary>
    public static Action? EnsureServiceActive { get; set; }

    public static Func<int>? GetPositionMs { get; set; }
    public static Func<int>? GetDurationMs { get; set; }
    public static Func<bool>? GetIsPlaying { get; set; }

    /// <summary>ExoPlayer's own "intends to play" flag - true while playing AND while buffering/stalled
    /// mid-stream (unlike <see cref="GetIsPlaying"/>, which goes false during buffering too), false only
    /// once something has actually paused the player. Covers every way that can happen - the player bar,
    /// the lock-screen/notification controls, a headset button - since they all end up calling the same
    /// underlying ExoPlayer instance rather than routing through <see cref="Pause"/>/<see cref="Play"/>.
    /// This is what lets a stall watchdog tell a silent stream wedge from a deliberate pause.</summary>
    public static Func<bool>? GetPlayWhenReady { get; set; }
}
