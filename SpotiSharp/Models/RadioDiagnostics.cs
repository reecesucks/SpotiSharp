using System.Text;
using SpotiSharpBackend;
using SpotifyAPI.Web;

namespace SpotiSharp.Models;

/// <summary>The radio page's debug tools: bug report files, and comparing Spotify's queue with ours.</summary>
public static class RadioDiagnostics
{
    /// <summary>
    /// Set by the platform to write a file somewhere that outlives the app's own data (the public
    /// Downloads folder on Android). Takes a file name and content, returns where it went.
    /// </summary>
    public static Func<string, string, string?>? SavePublicFile { get; set; }

    /// <summary>
    /// Everything SpotiSharp knows locally, without any network call, so it can be captured the
    /// instant the listener notices something wrong.
    /// </summary>
    public static string DescribeLocalState()
    {
        var text = new StringBuilder();
        var snapshot = PlaybackStateStore.Instance.Snapshot;

        text.AppendLine($"App Remote connected: {PlaybackStateStore.HasActivePushSource?.Invoke() == true}");
        text.AppendLine($"Last playback sample: {(snapshot.IsPlaying ? "playing" : "paused")} {RadioConductor.Label(snapshot.CurrentItemUri)} " +
                        $"at {snapshot.ProgressMs / 1000}s of {snapshot.DurationMs / 1000}s, fresh within 10s: {PlaybackStateStore.Instance.IsFresh(TimeSpan.FromSeconds(10))}");
        text.Append(RadioConductor.Instance.DescribeState());

        var queues = AppRemotePlayback.SnapshotQueues();
        text.AppendLine($"Queued by SpotiSharp, not seen playing yet: {DescribeUris(queues.NotYetPlayed)}");
        text.AppendLine($"Radio's queue mirror: {DescribeUris(queues.RadioMirror)}");
        text.AppendLine($"Removed songs to skip when reached: {DescribeUris(queues.ToSkip)}");
        return text.ToString();
    }

    /// <summary>Writes the captured state, Spotify's queue and the whole rolling log to a file. Returns where it went, or null.</summary>
    public static async Task<string?> SaveBugReportAsync(DateTime tappedAt, string stateAtTap, string note)
    {
        DiagnosticLog.Write($"[Mark] saving bug report{(string.IsNullOrWhiteSpace(note) ? "" : $": {note}")}");

        return await Task.Run(() =>
        {
            var report = new StringBuilder();
            report.AppendLine("SpotiSharp radio bug report");
            report.AppendLine($"Tapped: {tappedAt:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Note: {(string.IsNullOrWhiteSpace(note) ? "(none)" : note)}");
            report.AppendLine($"App: {AppInfo.Current.VersionString} build {AppInfo.Current.BuildString}, " +
                              $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}, {DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}");
            report.AppendLine();
            report.AppendLine("===== state when tapped =====");
            report.AppendLine(stateAtTap);
            report.AppendLine("===== queue check when saved =====");
            report.AppendLine(DescribeQueues());
            report.AppendLine("===== log =====");
            report.Append(DiagnosticLog.ReadAll());

            var fileName = $"radio-bug-{tappedAt:yyyyMMdd-HHmmss}.txt";
            try
            {
                var savedTo = SavePublicFile != null
                    ? SavePublicFile(fileName, report.ToString())
                    : SaveToAppData(fileName, report.ToString());

                DiagnosticLog.Write($"[Mark] bug report saved to {savedTo ?? "nowhere (save returned null)"}");
                return savedTo;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Mark] bug report save failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        });
    }

    private static string SaveToAppData(string fileName, string content)
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "bug-reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Spotify's queue (from the Web API) next to what SpotiSharp queued and what the radio plans. Blocks on a network call.</summary>
    public static string DescribeQueues()
    {
        var text = new StringBuilder();

        var remaining = RadioConductor.Instance.RemainingItems ?? new List<RadioItem>();
        var upcoming = remaining.Skip(1).ToList();
        var run = upcoming.TakeWhile(item => !item.IsPodcastSegment).ToList();
        var nextPodcast = upcoming.FirstOrDefault(item => item.IsPodcastSegment);
        var runUris = run.Select(item => item.PlayUri).ToHashSet();
        var radioUris = remaining.Select(item => item.PlayUri).ToHashSet();

        text.AppendLine("SPOTIFY (Web API):");
        AppendSpotifyQueue(text, runUris, radioUris);

        text.AppendLine();
        text.AppendLine("QUEUED BY SPOTISHARP, NOT SEEN PLAYING YET (should match Spotify's \"Next in queue\"):");
        var notYetPlayed = AppRemotePlayback.SnapshotQueues().NotYetPlayed;
        if (notYetPlayed.Count == 0) text.AppendLine("  (nothing)");
        for (int i = 0; i < notYetPlayed.Count; i++) text.AppendLine($"  {i + 1}. {Tag(notYetPlayed[i], runUris, radioUris)} {RadioConductor.Label(notYetPlayed[i])}");

        text.AppendLine();
        if (remaining.Count == 0)
        {
            text.AppendLine("RADIO PLAN: radio isn't active");
            return text.ToString();
        }

        text.AppendLine($"RADIO PLAN (now on {RadioConductor.Label(remaining[0].PlayUri)}):");
        if (remaining[0].IsPodcastSegment) text.AppendLine("  (a podcast is on, so Spotify's queue should be empty until the next song run starts)");
        for (int i = 0; i < run.Count; i++) text.AppendLine($"  {i + 1}. {RadioConductor.Label(run[i].PlayUri)}");
        text.AppendLine(nextPodcast != null
            ? $"  then podcast from {nextPodcast.PositionMs / 1000}s: {RadioConductor.Label(nextPodcast.PlayUri)}"
            : "  then the end of the radio");

        return text.ToString();
    }

    private static void AppendSpotifyQueue(StringBuilder text, HashSet<string> runUris, HashSet<string> radioUris)
    {
        var api = APICaller.Instance;
        if (api == null)
        {
            text.AppendLine("  (not signed in to the Web API)");
            return;
        }

        QueueResponse? response;
        try
        {
            response = api.GetQueue();
        }
        catch (Exception ex)
        {
            text.AppendLine($"  (call threw {ex.GetType().Name}: {ex.Message})");
            return;
        }

        if (response == null)
        {
            text.AppendLine("  (call failed)");
            return;
        }

        if (response.CurrentlyPlaying == null)
        {
            text.AppendLine("  Web API sees nothing playing, so it probably can't see the phone and its queue can't be trusted.");
        }
        else
        {
            text.AppendLine($"  now: {DescribePlayable(response.CurrentlyPlaying)}");
        }

        var queue = response.Queue ?? new List<IPlayableItem>();
        if (queue.Count == 0) text.AppendLine("  (queue empty)");
        for (int i = 0; i < queue.Count; i++)
        {
            text.AppendLine($"  {i + 1}. {Tag(UriOf(queue[i]), runUris, radioUris)} {DescribePlayable(queue[i])}");
        }
        text.AppendLine("  (the Web API mixes \"Next in queue\" with Spotify's own picks and doesn't say which is which)");
    }

    private static string Tag(string? uri, HashSet<string> runUris, HashSet<string> radioUris)
    {
        if (uri != null && runUris.Contains(uri)) return "[run]";
        if (uri != null && radioUris.Contains(uri)) return "[radio, later]";
        return "[NOT IN RADIO]";
    }

    private static string? UriOf(IPlayableItem item) => item switch
    {
        FullTrack track => track.Uri,
        FullEpisode episode => episode.Uri,
        _ => null
    };

    private static string DescribePlayable(IPlayableItem item) => item switch
    {
        FullTrack track => $"{track.Uri} \"{track.Name} / {string.Join(", ", track.Artists?.Select(artist => artist.Name) ?? Enumerable.Empty<string>())}\"",
        FullEpisode episode => $"{episode.Uri} \"{episode.Name} / {episode.Show?.Name}\"",
        _ => item?.Type.ToString() ?? "unknown"
    };

    private static string DescribeUris(List<string> uris) =>
        uris.Count == 0 ? "[]" : "[" + string.Join(", ", uris.Select(RadioConductor.Label)) + "]";
}
