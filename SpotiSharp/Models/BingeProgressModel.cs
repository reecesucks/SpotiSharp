using SpotifyAPI.Web;
using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class BingeProgressModel
{
    private const int PAGE_SIZE = 50;

    internal static BingeProgress CreateFromCurrentPlayback(string showId)
    {
        var context = APICaller.Instance?.GetCurrentPlaybackContext();
        if (context?.Item is not FullEpisode episode || episode.Show?.Id != showId) return null;

        int? indexFromOldest = FindIndexFromOldest(showId, episode.Id);
        if (indexFromOldest == null) return null;

        return new BingeProgress
        {
            LastFinishedIndexFromOldest = indexFromOldest.Value - 1,
            NextEpisodeName = episode.Name
        };
    }

    internal static RecentEpisode FindNextEpisode(string showId, string showName, string showImageUrl)
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
                if (episode == null || episode.ResumePoint?.FullyPlayed == true)
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
                    episode.DurationMs);
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
