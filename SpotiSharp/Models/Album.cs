namespace SpotiSharp.Models;

public class Album
{
    public string AlbumId { get; private set; }
    public string AlbumName { get; private set; }
    public string AlbumImageUrl { get; private set; }
    public string ReleaseDate { get; private set; }
    public string ArtistNames { get; private set; }

    public Album(string albumId, string albumName, string albumImageUrl, string releaseDate, string artistNames)
    {
        AlbumId = albumId;
        AlbumName = albumName;
        AlbumImageUrl = albumImageUrl;
        ReleaseDate = releaseDate;
        ArtistNames = artistNames;
    }
}
