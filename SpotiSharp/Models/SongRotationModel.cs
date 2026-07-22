using System.Text.RegularExpressions;
using SpotifyAPI.Web;
using SpotiSharpBackend;
using Constants = SpotiSharp.Consts.Constants;

namespace SpotiSharp.Models;

public class SongRotationModel
{
    private static readonly Regex RotationTag = new Regex(@"#R-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IncreaseRotation(string trackUri)
    {
        var api = APICaller.Instance;
        if (api == null) return false;

        var rotationPlaylists = GetRotationPlaylistsWithMembership(api, trackUri);
        if (rotationPlaylists == null) return false;

        var containing = rotationPlaylists.Where(entry => entry.ContainsTrack).ToList();
        int targetLevel = containing.Count == 0 ? 1 : containing.Max(entry => entry.Level) + 1;

        var targetPlaylist = rotationPlaylists.FirstOrDefault(entry => entry.Level == targetLevel);
        if (targetPlaylist == null)
        {
            var created = api.CreatePlaylist($"#R-{targetLevel}");
            if (created == null) return false;
            if (!api.AddTrackToPlaylist(created.Id, trackUri)) return false;
            RotationTracksModel.Invalidate(created.Id);
        }
        else if (!targetPlaylist.ContainsTrack)
        {
            if (!api.AddTrackToPlaylist(targetPlaylist.PlaylistId, trackUri)) return false;
            RotationTracksModel.Invalidate(targetPlaylist.PlaylistId);
        }

        foreach (var entry in rotationPlaylists.Where(entry => entry.Level < targetLevel && !entry.ContainsTrack))
        {
            if (api.AddTrackToPlaylist(entry.PlaylistId, trackUri)) RotationTracksModel.Invalidate(entry.PlaylistId);
        }

        return true;
    }

    public static bool DecreaseRotation(string trackUri)
    {
        var api = APICaller.Instance;
        if (api == null) return false;

        var rotationPlaylists = GetRotationPlaylistsWithMembership(api, trackUri);
        if (rotationPlaylists == null) return false;

        var containing = rotationPlaylists.Where(entry => entry.ContainsTrack).ToList();

        if (containing.Count == 0) return RemoveFromSourcePlaylists(api, trackUri);

        int highestLevel = containing.Max(entry => entry.Level);
        foreach (var entry in containing.Where(entry => entry.Level == highestLevel))
        {
            if (!api.RemoveTrackFromPlaylist(entry.PlaylistId, trackUri)) return false;
            RotationTracksModel.Invalidate(entry.PlaylistId);
        }

        return true;
    }

    private static bool RemoveFromSourcePlaylists(APICaller api, string trackUri)
    {
        bool removedAny = false;

        foreach (var playlistId in RadioModel.SourcePlaylistIds())
        {
            var tracks = RotationTracksModel.GetTracks(playlistId);
            if (tracks == null || tracks.All(track => track.SongUri != trackUri)) continue;

            bool removed;
            if (playlistId == Constants.LIKED_PLALIST_ID)
            {
                removed = api.UnlikeTrack(TrackIdFromUri(trackUri));
            }
            else if (api.IsPlaylistOwnedByCurrentUser(playlistId))
            {
                removed = api.RemoveTrackFromPlaylist(playlistId, trackUri);
            }
            else
            {
                continue;
            }

            if (!removed) continue;

            RotationTracksModel.Invalidate(playlistId);
            removedAny = true;
        }

        return removedAny;
    }

    private static string TrackIdFromUri(string trackUri)
    {
        int lastSeparator = trackUri.LastIndexOf(':');
        return lastSeparator >= 0 ? trackUri.Substring(lastSeparator + 1) : trackUri;
    }

    private static List<RotationPlaylist> GetRotationPlaylistsWithMembership(APICaller api, string trackUri)
    {
        var userPlaylists = api.GetAllUserPlaylists();
        if (userPlaylists == null) return null;

        var currentUserId = api.GetCurrentUserId();

        var result = new List<RotationPlaylist>();
        foreach (var playlist in userPlaylists)
        {
            var match = RotationTag.Match(playlist.Name ?? string.Empty);
            if (!match.Success) continue;

            // GetAllUserPlaylists also returns followed playlists; only manage rotation lists we own
            if (currentUserId != null && playlist.Owner?.Id != currentUserId) continue;

            var trackUris = api.GetTracksByPlaylistId(playlist.Id)
                .Select(playlistTrack => playlistTrack.Track)
                .OfType<FullTrack>()
                .Select(track => track.Uri);

            result.Add(new RotationPlaylist(int.Parse(match.Groups[1].Value), playlist.Id, trackUris.Contains(trackUri)));
        }
        return result;
    }

    private class RotationPlaylist
    {
        public int Level { get; }
        public string PlaylistId { get; }
        public bool ContainsTrack { get; }

        public RotationPlaylist(int level, string playlistId, bool containsTrack)
        {
            Level = level;
            PlaylistId = playlistId;
            ContainsTrack = containsTrack;
        }
    }
}
