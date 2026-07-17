namespace SpotiSharp.Models;

public class RadioConfig
{
    public Dictionary<string, int> PlaylistWeights { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ShowWeights { get; set; } = new Dictionary<string, int>();

    public List<string> EnabledPlaylistIds { get; set; } = new List<string>();
    public List<string> EnabledShowIds { get; set; } = new List<string>();
}
