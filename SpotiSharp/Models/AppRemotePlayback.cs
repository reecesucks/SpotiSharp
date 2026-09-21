using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public static class AppRemotePlayback
{
    public static async Task<bool> TryPlayAsync(string uri, IEnumerable<string>? queueAfter = null)
    {
        if (string.IsNullOrEmpty(uri) || PlaybackCommands.PlayUri == null) return false;

        if (PlaybackCommands.WakeSpotify != null && !await PlaybackCommands.WakeSpotify()) return false;

        if (!await PlaybackCommands.PlayUri(uri)) return false;

        if (queueAfter != null && PlaybackCommands.QueueUri != null)
        {
            foreach (var queuedUri in queueAfter)
            {
                if (!string.IsNullOrEmpty(queuedUri)) await PlaybackCommands.QueueUri(queuedUri);
            }
        }

        return true;
    }

    /// <summary>Plays a radio item locally: a podcast segment from its rewound start, or a song followed by the rest of its run.</summary>
    public static async Task<bool> TryPlayItemAsync(RadioItem item, IReadOnlyList<string>? songRun)
    {
        if (item.IsPodcastSegment)
        {
            if (!await TryPlayAsync(item.PlayUri)) return false;

            var rewoundMs = Math.Max(0, item.PositionMs - RadioTuning.RESUME_REWIND_MS);
            if (rewoundMs > 0) PlaybackCommands.SeekTo?.Invoke(rewoundMs);

            return true;
        }

        if (songRun == null || songRun.Count == 0) return false;
        return await TryPlayAsync(songRun[0], songRun.Skip(1));
    }
}
