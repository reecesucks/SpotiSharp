namespace SpotiSharp.Models;

public class RadioSong
{
    public string SongUri { get; private set; }
    public string SongTitle { get; private set; }
    public string SongArtists { get; private set; }
    public string SongImageUrl { get; private set; }

    public RadioSong(string songUri, string songTitle, string songArtists, string songImageUrl)
    {
        SongUri = songUri;
        SongTitle = songTitle;
        SongArtists = songArtists;
        SongImageUrl = songImageUrl;
    }
}
