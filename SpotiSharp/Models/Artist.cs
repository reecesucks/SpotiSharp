namespace SpotiSharp.Models;

public class Artist
{
    public string ArtistId { get; private set; }
    public string ArtistName { get; private set; }
    public string ArtistImageUrl { get; private set; }

    public Artist(string artistId, string artistName, string artistImageUrl)
    {
        ArtistId = artistId;
        ArtistName = artistName;
        ArtistImageUrl = artistImageUrl;
    }
}
