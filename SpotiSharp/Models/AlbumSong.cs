namespace SpotiSharp.Models;

public class AlbumSong
{
    public string SongId { get; private set; }
    public string SongUri { get; private set; }
    public string SongTitle { get; private set; }
    public string SongArtists { get; private set; }
    public int TrackNumber { get; private set; }

    public AlbumSong(string songId, string songUri, string songTitle, string songArtists, int trackNumber)
    {
        SongId = songId;
        SongUri = songUri;
        SongTitle = songTitle;
        SongArtists = songArtists;
        TrackNumber = trackNumber;
    }
}
