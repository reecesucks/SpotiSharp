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
}
