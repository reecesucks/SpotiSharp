using SpotifyAPI.Web;
using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

public class SongEditable : Song
{
    public int Index { get; private set; }

    public SongEditable(int index, FullTrack fullTrack)
    {
        Index = index;
        SongId = fullTrack.Id;
        SongImageURL = ImageHelper.Thumbnail(fullTrack.Album.Images);
        SongTitle = fullTrack.Name;
        SongArtists = string.Join(", ", fullTrack.Artists);
    }
}