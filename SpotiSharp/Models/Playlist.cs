namespace SpotiSharp.Models;

public class Playlist
{
    public string PlayListId { get; private set; }
    public string PlayListImageURL { get; private set; }
    public string PlayListTitle { get; private set; }
    public int SongAmount { get; private set; }

    public Playlist(string playListId, string playListImageURL, string playListTitle, int songAmount)
    {
        PlayListId = playListId;
        PlayListImageURL = playListImageURL;
        PlayListTitle = playListTitle;
        SongAmount = songAmount;
    }
}