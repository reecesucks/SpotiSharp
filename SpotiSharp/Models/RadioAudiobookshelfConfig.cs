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

    // The AudioBookshelf Podcasts page's "Unplayed" toggle - on by default, including for configs
    // saved before this field existed (the property initializer below still runs on deserialization,
    // so an old save with no such field still comes back true).
    public bool PodcastsPageUnplayedOnly { get; set; } = true;

    // Episodes picked from "More episodes" to lead the next generated radio, in the order they
    // were picked. Whole episodes rather than ids, since a binge look-ahead isn't in any cache.
    // Spotify or AudioBookshelf - see PodcastEpisodeSourceConverter for how this round-trips either.
    public List<IPodcastEpisodeSource> UpNextEpisodes { get; set; } = new List<IPodcastEpisodeSource>();
}
