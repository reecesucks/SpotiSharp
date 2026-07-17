namespace SpotiSharp.Models;

public class RecentEpisode
{
    public string EpisodeId { get; private set; }
    public string EpisodeName { get; private set; }
    public string ShowId { get; private set; }
    public string ShowName { get; private set; }
    public string ShowImageUrl { get; private set; }
    public DateTime ReleaseDate { get; private set; }
    public int DurationMs { get; private set; }

    public RecentEpisode(string episodeId, string episodeName, string showId, string showName, string showImageUrl, DateTime releaseDate, int durationMs)
    {
        EpisodeId = episodeId;
        EpisodeName = episodeName;
        ShowId = showId;
        ShowName = showName;
        ShowImageUrl = showImageUrl;
        ReleaseDate = releaseDate;
        DurationMs = durationMs;
    }
}
