namespace SpotiSharp.Models;

public class RecentEpisode
{
    public string EpisodeId { get; private set; }
    public string EpisodeName { get; private set; }
    public string ShowName { get; private set; }
    public string ShowImageUrl { get; private set; }
    public DateTime ReleaseDate { get; private set; }

    public RecentEpisode(string episodeId, string episodeName, string showName, string showImageUrl, DateTime releaseDate)
    {
        EpisodeId = episodeId;
        EpisodeName = episodeName;
        ShowName = showName;
        ShowImageUrl = showImageUrl;
        ReleaseDate = releaseDate;
    }
}
