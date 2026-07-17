using System.Text.RegularExpressions;
using SpotifyAPI.Web;
using SpotiSharpBackend;

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
        }
        else if (!targetPlaylist.ContainsTrack)
        {
            if (!api.AddTrackToPlaylist(targetPlaylist.PlaylistId, trackUri)) return false;
        }

        foreach (var entry in rotationPlaylists.Where(entry => entry.Level < targetLevel && !entry.ContainsTrack))
        {
            api.AddTrackToPlaylist(entry.PlaylistId, trackUri);
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
        if (containing.Count == 0) return false;

        int highestLevel = containing.Max(entry => entry.Level);
        foreach (var entry in containing.Where(entry => entry.Level == highestLevel))
        {
            if (!api.RemoveTrackFromPlaylist(entry.PlaylistId, trackUri)) return false;
        }

        return true;
    }

    private static List<RotationPlaylist> GetRotationPlaylistsWithMembership(APICaller api, string trackUri)
    {
        var userPlaylists = api.GetAllUserPlaylists();
        if (userPlaylists == null) return null;

        var result = new List<RotationPlaylist>();
        foreach (var playlist in userPlaylists)
        {
            var match = RotationTag.Match(playlist.Name ?? string.Empty);
            if (!match.Success) continue;

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
