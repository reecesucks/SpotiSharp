using SpotiSharpBackend;
using SpotifyAPI.Web;
using SpotiSharp.Consts;
using SpotiSharp.Helpers;

namespace SpotiSharp.Models;

public class SongsListModel
{
    public List<Song> Songs { get; private set; } = new List<Song>();
    
    public SongsListModel(string playlistId)
    {
        if (playlistId == Constants.LIKED_PLALIST_ID)
        {
            List<FullTrack> likedTracks = APICaller.Instance?.GetUserLikedSongs().Select(st => st.Track).ToList() ?? new List<FullTrack>();
            foreach (var likedTrack in likedTracks)
            {
                Songs.Add(new Song(likedTrack.Id, likedTrack.Uri, ImageHelper.Thumbnail(likedTrack.Album.Images), likedTrack.Name, string.Join(", ", likedTrack.Artists.Select(x => x.Name)), playlistId));
            }
            return;
        }
        var songs = APICaller.Instance?.GetTracksByPlaylistId(playlistId);
        if (songs == null) return;
        foreach (var playableObject in songs.ToList())
        {
            if (playableObject.Track.Type == ItemType.Track)
            {
                if (playableObject.Track is not FullTrack song) continue;
                Songs.Add(new Song(song.Id, song.Uri, ImageHelper.Thumbnail(song.Album.Images), song.Name, string.Join(", ", song.Artists.Select(x => x.Name)), playlistId));
            }
            else if (playableObject.Track.Type == ItemType.Episode)
            {
                if (playableObject.Track is not FullEpisode episode) continue;
                Songs.Add(new Song(episode.Id, episode.Uri, ImageHelper.Thumbnail(episode.Images), episode.Name, "", playlistId));
            }
        }
    }
}