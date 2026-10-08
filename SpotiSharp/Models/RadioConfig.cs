namespace SpotiSharp.Models;

public class RadioConfig
{
    public Dictionary<string, int> PlaylistWeights { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ShowWeights { get; set; } = new Dictionary<string, int>();

    public Dictionary<string, BingeProgress> BingeShows { get; set; } = new Dictionary<string, BingeProgress>();

    public Dictionary<string, RadioAlbumMode> AlbumModes { get; set; } = new Dictionary<string, RadioAlbumMode>();

    public List<string> EnabledPlaylistIds { get; set; } = new List<string>();

    public List<string> EnabledShowIds { get; set; } = new List<string>();

    public HashSet<string> ExcludedEpisodeIds { get; set; } = new HashSet<string>();

    public int SongsPerSection { get; set; } = 3;

    public int PodcastSegmentMinutes { get; set; } = 15;

    public bool FullPodcastEpisodes { get; set; } = false;
}
