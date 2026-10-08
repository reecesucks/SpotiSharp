using SpotifyAPI.Web;
using SpotiSharp.Helpers;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class BingeProgressModel
{
    private const int PAGE_SIZE = 50;

    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

    private const int PROGRESS_NOISE_THRESHOLD_MS = 10_000;

    internal static BingeProgress CreateFromCurrentPlayback(string showId)
    {
        var context = APICaller.Instance?.GetCurrentPlaybackContext();
        if (context?.Item is not FullEpisode episode || episode.Show?.Id != showId) return null;

        int? indexFromOldest = FindIndexFromOldest(showId, episode.Id);
        if (indexFromOldest == null) return null;

        return new BingeProgress
        {
            LastFinishedIndexFromOldest = indexFromOldest.Value - 1,
            NextEpisodeName = episode.Name,
            LastTrackedEpisodeId = episode.Id,
            LastTrackedResumePositionMs = context.ProgressMs,
            LastActivityUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Records fresh resume-position evidence for the show's current pick, taken during radio
    /// generation since that is the only point the app re-checks Spotify's live progress. The
    /// recency stamp only moves when this shows real forward progress since the last check -
    /// otherwise merely re-checking an untouched resume point would look like activity on its own.
    /// </summary>
    internal static void NoteResumeProgress(string showId, string episodeId, int resumePositionMs)
    {
        var binge = RadioConfigModel.GetBinge(showId);
        if (binge == null) return;

        if (binge.LastTrackedEpisodeId != episodeId)
        {
            binge.LastTrackedEpisodeId = episodeId;
            binge.LastTrackedResumePositionMs = resumePositionMs;
            RadioConfigModel.SaveConfig();
            return;
        }

        if (resumePositionMs > binge.LastTrackedResumePositionMs + PROGRESS_NOISE_THRESHOLD_MS)
        {
            binge.LastTrackedResumePositionMs = resumePositionMs;
            binge.LastActivityUtc = DateTime.UtcNow;
            RadioConfigModel.SaveConfig();
        }
    }

    /// <summary>
    /// Whether this show has gone untouched long enough that its resume point should be treated
    /// as abandoned. A show never tracked yet (existing binges from before this field existed, or
    /// one only just pinned) has nothing to judge staleness from, so it is treated as fresh.
    /// </summary>
    internal static bool IsStale(string showId)
    {
        var binge = RadioConfigModel.GetBinge(showId);
        if (binge?.LastActivityUtc == null) return false;
        return DateTime.UtcNow - binge.LastActivityUtc.Value > StaleAfter;
    }

    internal static RecentEpisode FindNextEpisode(string showId, string showName, string showImageUrl, ISet<string> excludedEpisodeIds = null)
    {
        var binge = RadioConfigModel.GetBinge(showId);
        if (binge == null) return null;

        var probe = APICaller.Instance?.GetPodcastEpisodesPage(showId, 0, 1);
        if (probe == null) return null;
        int total = probe.Total ?? 0;

        int searchIndex = binge.LastFinishedIndexFromOldest + 1;
        while (searchIndex < total)
        {
            int offset = Math.Max(0, total - searchIndex - PAGE_SIZE);
            var page = APICaller.Instance?.GetPodcastEpisodesPage(showId, offset, PAGE_SIZE);
            if (page?.Items == null) return null;

            int searchIndexBeforePage = searchIndex;
            for (int i = page.Items.Count - 1; i >= 0; i--)
            {
                int itemIndex = total - 1 - (offset + i);
                if (itemIndex < searchIndex) continue;

                var episode = page.Items[i];
                // "not interested" never reaches Spotify's own played state, so it's skipped here the
                // same way an already-listened episode is - otherwise the binge marker never moves
                // past it and this show quietly drops out of every future radio generation.
                if (episode == null || EpisodeHelper.IsListened(episode) || (episode.Id != null && excludedEpisodeIds?.Contains(episode.Id) == true))
                {
                    searchIndex = itemIndex + 1;
                    continue;
                }

                AdvanceMarker(binge, itemIndex - 1, episode.Name);
                return new RecentEpisode(
                    episode.Id,
                    episode.Name,
                    showId,
                    showName,
                    showImageUrl,
                    RecentEpisodesModel.ParseReleaseDate(episode.ReleaseDate),
                    episode.DurationMs,
                    episode.ResumePoint?.ResumePositionMs ?? 0);
            }

            if (searchIndex == searchIndexBeforePage) return null;
        }

        return null;
    }

    private static void AdvanceMarker(BingeProgress binge, int lastFinishedIndex, string nextEpisodeName)
    {
        if (lastFinishedIndex <= binge.LastFinishedIndexFromOldest) return;

        binge.LastFinishedIndexFromOldest = lastFinishedIndex;
        binge.NextEpisodeName = nextEpisodeName;
        binge.LastActivityUtc = DateTime.UtcNow;
        RadioConfigModel.SaveConfig();
    }

    private static int? FindIndexFromOldest(string showId, string episodeId)
    {
        int offset = 0;
        int total = int.MaxValue;
        while (offset < total)
        {
            var page = APICaller.Instance?.GetPodcastEpisodesPage(showId, offset, PAGE_SIZE);
            if (page?.Items == null) return null;
            total = page.Total ?? 0;

            for (int i = 0; i < page.Items.Count; i++)
            {
                if (page.Items[i]?.Id == episodeId) return total - 1 - (offset + i);
            }
            offset += PAGE_SIZE;
        }
        return null;
    }
}
