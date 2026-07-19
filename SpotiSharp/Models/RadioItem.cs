namespace SpotiSharp.Models;

public class RadioItem
{
    public bool IsPodcastSegment { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string ImageUrl { get; }
    public string PlayUri { get; }

    public int PositionMs { get; }

    public List<bool> SegmentPips { get; }

    public RadioItem(bool isPodcastSegment, string title, string subtitle, string imageUrl, string playUri, int positionMs, List<bool> segmentPips)
    {
        IsPodcastSegment = isPodcastSegment;
        Title = title;
        Subtitle = subtitle;
        ImageUrl = imageUrl;
        PlayUri = playUri;
        PositionMs = positionMs;
        SegmentPips = segmentPips;
    }

    internal static RadioItem ForSong(string title, string artists, string imageUrl, string trackUri)
    {
        return new RadioItem(false, title, artists, imageUrl, trackUri, 0, new List<bool>());
    }

    internal static RadioItem ForPodcastSegment(RecentEpisode episode, int segmentIndex, int segmentCount, int segmentLengthMs)
    {
        var pips = Enumerable.Range(0, segmentCount).Select(index => index == segmentIndex).ToList();
        var subtitle = segmentCount > 1
            ? $"{episode.ShowName} · Part {segmentIndex + 1} of {segmentCount}"
            : episode.ShowName;

        return new RadioItem(
            true,
            episode.EpisodeName,
            subtitle,
            episode.ShowImageUrl,
            $"spotify:episode:{episode.EpisodeId}",
            segmentIndex * segmentLengthMs,
            pips);
    }
}
