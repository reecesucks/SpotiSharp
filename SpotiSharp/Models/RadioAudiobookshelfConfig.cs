namespace SpotiSharp.Models;

public class RadioAudiobookshelfConfig
{
    public Dictionary<string, int> PlaylistWeights { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ShowWeights { get; set; } = new Dictionary<string, int>();

    public Dictionary<string, BingeProgress> BingeShows { get; set; } = new Dictionary<string, BingeProgress>();

    public Dictionary<string, RadioAudiobookshelfAlbumMode> AlbumModes { get; set; } = new Dictionary<string, RadioAudiobookshelfAlbumMode>();

    public List<string> EnabledPlaylistIds { get; set; } = new List<string>();

    public List<string> EnabledShowIds { get; set; } = new List<string>();

    public HashSet<string> ExcludedEpisodeIds { get; set; } = new HashSet<string>();

    public int SongsPerSection { get; set; } = 3;

    public int PodcastSegmentMinutes { get; set; } = 15;

    public bool FullPodcastEpisodes { get; set; } = false;

    public RadioAudiobookshelfListenMode ListenMode { get; set; } = RadioAudiobookshelfListenMode.Mix;

    // Episodes picked from "More episodes" to lead the next generated radio, in the order they
    // were picked. Whole episodes rather than ids, since a binge look-ahead isn't in any cache.
    public List<RecentEpisode> UpNextEpisodes { get; set; } = new List<RecentEpisode>();
}
