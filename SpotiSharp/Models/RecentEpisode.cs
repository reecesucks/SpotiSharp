namespace SpotiSharp.Models;

public class RecentEpisode
{
    public string EpisodeId { get; private set; }
    public string EpisodeName { get; private set; }
    public string ShowName { get; private set; }
    public string ImageUrl { get; private set; }
    public DateTime ReleaseDate { get; private set; }

    public RecentEpisode(string episodeId, string episodeName, string showName, string imageUrl, DateTime releaseDate)
    {
        EpisodeId = episodeId;
        EpisodeName = episodeName;
        ShowName = showName;
        ImageUrl = imageUrl;
        ReleaseDate = releaseDate;
    }
}
