using SpotiSharp.Consts;

namespace SpotiSharp.Models;

public class DetailPlaylistModel
{
    public string GetPlaylistName(string playlistId)
    {
        if (playlistId == Constants.LIKED_PLALIST_ID) return "Liked Songs";

        var playlist = SpotiSharpBackend.APICaller.Instance?.GetPlaylistById(playlistId);
        return playlist?.Name ?? "Couldn't load playlist name.";
    }
}