using System.Text.RegularExpressions;
using SpotiSharp.Helpers;
using SpotiSharpBackend;
using Constants = SpotiSharp.Consts.Constants;

namespace SpotiSharp.Models;

public class RadioAudiobookshelfModel
{
    internal static int SegmentLengthMs => RadioAudiobookshelfConfigModel.PodcastSegmentLengthMs;
    internal static int SongsPerSection => RadioAudiobookshelfConfigModel.GetSongsPerSection();
    private const int ALBUM_SONG_COUNT = 4;

    // longest playlist/album name shown as a song's source before it's shortened
    private const int SOURCE_LABEL_MAX_CHARS = 9;

    private const int RESUME_IGNORE_THRESHOLD_MS = 30 * 1000;

    private const string RADIO_CACHE_KEY = "radio";

    internal static List<RadioAudiobookshelfItem> CachedRadio => DiskCacheHelper.Load<List<RadioAudiobookshelfItem>>(RADIO_CACHE_KEY);

    internal static void SaveRadio(List<RadioAudiobookshelfItem> radio)
    {
        DiskCacheHelper.Save(RADIO_CACHE_KEY, radio);
    }

    private static readonly Regex RotationTag = new Regex(@"#R-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static List<RadioAudiobookshelfItem> Generate()
    {
        PlaylistListModel.RefreshPlayLists();

        var episodes = GetEpisodes(out var liveProgress, out var staleBingeEpisodeIds, out var moreEpisodes);
        if (episodes == null) return null;

        var songPool = BuildSongPool();
        if (songPool == null) return null;

        var radio = new List<RadioAudiobookshelfItem>();
        int songIndex = 0;

        bool fullEpisodes = RadioAudiobookshelfConfigModel.GetFullPodcastEpisodes();

        foreach (var episode in episodes)
        {
            int startMs = ResumeStartFor(episode, liveProgress, staleBingeEpisodeIds);

            // Once an episode has splice metadata at all, it's the only thing that gets to decide
            // where this episode breaks for music - never fall back to the fixed timer for it, even
            // when every splice point turns out to be behind where we're resuming (already played
            // through on a previous visit): that just means play the rest straight through, not that
            // the timer should take over.
            bool hasSpliceData = !fullEpisodes && episode.SpliceStartMs != null && episode.SpliceStartMs.Count > 0;

            if (hasSpliceData)
            {
                var spliceStartsMs = episode.SpliceStartMs.Where(spliceMs => spliceMs > startMs && spliceMs < episode.DurationMs).ToList();

                var segmentStartsMs = new List<int> { startMs };
                segmentStartsMs.AddRange(spliceStartsMs);
                int totalSegments = segmentStartsMs.Count;

                for (int segmentIndex = 0; segmentIndex < totalSegments; segmentIndex++)
                {
                    AddSongs(radio, songPool, ref songIndex, SongsPerSection);
                    radio.Add(RadioAudiobookshelfItem.ForPodcastSegmentAt(
                        episode, segmentStartsMs[segmentIndex], segmentIndex, totalSegments,
                        isFinalSegment: segmentIndex == totalSegments - 1));
                }
                continue;
            }

            int remainingMs = Math.Max(0, episode.DurationMs - startMs);
            int segmentCount = fullEpisodes ? 1 : SegmentCountFor(remainingMs);

            int totalFixedSegments = fullEpisodes ? 1 : Math.Max(segmentCount, SegmentCountFor(episode.DurationMs));
            int firstSegmentNumber = totalFixedSegments - segmentCount;

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                AddSongs(radio, songPool, ref songIndex, SongsPerSection);
                radio.Add(RadioAudiobookshelfItem.ForPodcastSegment(
                    episode, segmentIndex, SegmentLengthMs, startMs,
                    firstSegmentNumber + segmentIndex, totalFixedSegments,
                    isFinalSegment: segmentIndex == segmentCount - 1));
            }
        }

        InsertAlbumSongs(radio);

        // Up next has done its job once it's in a radio.
        RadioAudiobookshelfConfigModel.ClearUpNext();
        DiskCacheHelper.Save(MORE_EPISODES_CACHE_KEY, moreEpisodes);

        SaveRadio(radio);
        return radio;
    }

    private static int SegmentCountFor(int spanMs)
    {
        if (spanMs <= 0) return 1;

        int segmentLengthMs = SegmentLengthMs;
        int fullSegments = spanMs / segmentLengthMs;
        int leftoverMs = spanMs - fullSegments * segmentLengthMs;

        if (leftoverMs > segmentLengthMs / 3 || fullSegments == 0) fullSegments++;

        return fullSegments;
    }

    private static void InsertAlbumSongs(List<RadioAudiobookshelfItem> radio)
    {
        var albumModes = RadioAudiobookshelfConfigModel.Config.AlbumModes
            .Where(entry => entry.Value != RadioAudiobookshelfAlbumMode.Off)
            .ToList();
        if (albumModes.Count == 0) return;

        var savedAlbums = SavedAlbumsModel.CachedAlbums;
        var random = new Random();

        foreach (var (albumId, mode) in albumModes)
        {
            var album = savedAlbums.FirstOrDefault(savedAlbum => savedAlbum.AlbumId == albumId);
            if (album == null) continue;

            var songs = AlbumSongsModel.GetSongsCachedFirst(albumId);
            if (songs == null || songs.Count == 0) continue;

            // a random handful, kept in album track order
            var picked = songs
                .OrderBy(_ => random.Next())
                .Take(ALBUM_SONG_COUNT)
                .OrderBy(song => songs.IndexOf(song))
                .Select(song => RadioAudiobookshelfItem.ForSong(song.SongTitle, song.SongArtists, album.AlbumImageUrl, song.SongUri, ShortSourceName(album.AlbumName)))
                .ToList();

            if (mode == RadioAudiobookshelfAlbumMode.Consecutive)
            {
                radio.InsertRange(random.Next(radio.Count + 1), picked);
            }
            else
            {
                foreach (var item in picked)
                {
                    radio.Insert(random.Next(radio.Count + 1), item);
                }
            }
        }
    }

    private static int ResumeStartFor(IPodcastEpisodeSource episode, Dictionary<string, EpisodeProgress> liveProgress, ISet<string> staleBingeEpisodeIds)
    {
        if (staleBingeEpisodeIds != null && staleBingeEpisodeIds.Contains(episode.EpisodeId)) return 0;

        int resume = liveProgress != null && liveProgress.TryGetValue(episode.EpisodeId, out var live)
            ? live.ResumePositionMs
            : episode.ResumePositionMs;

        // Both guards mean "no useful resume point", so the episode plays from the start.
        // Finished episodes must never reach here - Spotify reports them either reset to zero
        // or past the end, so both would silently queue a full replay. GetEpisodes drops them.
        if (resume < RESUME_IGNORE_THRESHOLD_MS || resume >= episode.DurationMs) return 0;
        return resume;
    }

    private static void AddSongs(List<RadioAudiobookshelfItem> radio, List<RadioAudiobookshelfItem> songPool, ref int songIndex, int amount)
    {
        for (int i = 0; i < amount && songIndex < songPool.Count; i++)
        {
            radio.Add(songPool[songIndex]);
            songIndex++;
        }
    }

    private static readonly IPodcastEpisodeProvider SpotifyEpisodeProvider = new SpotifyPodcastEpisodeProvider();
    private static readonly IPodcastEpisodeProvider AudiobookshelfEpisodeProvider = new AudiobookshelfPodcastEpisodeProvider();

    // Either/or, not "both at once" - StorageHandler.UseAudiobookshelfPodcasts is the single switch
    // on the main settings page, read fresh each generation so flipping it takes effect immediately.
    private static IPodcastEpisodeProvider EpisodeProvider =>
        StorageHandler.UseAudiobookshelfPodcasts ? AudiobookshelfEpisodeProvider : SpotifyEpisodeProvider;

    /// <summary>
    /// Picks the episodes for this radio via <see cref="EpisodeProvider"/>, so the segment-building
    /// and resume logic below stays source-agnostic.
    /// </summary>
    private static List<IPodcastEpisodeSource> GetEpisodes(out Dictionary<string, EpisodeProgress> liveProgress, out HashSet<string> staleBingeEpisodeIds,
        out List<RecentEpisode> moreEpisodes)
    {
        return EpisodeProvider.GetEpisodes(out liveProgress, out staleBingeEpisodeIds, out moreEpisodes);
    }

    private const string MORE_EPISODES_CACHE_KEY = "radio_more_episodes";

    /// <summary>The episodes the last generation left out, for the podcasts view's "More episodes".</summary>
    internal static List<RecentEpisode> CachedMoreEpisodes => DiskCacheHelper.Load<List<RecentEpisode>>(MORE_EPISODES_CACHE_KEY);

    // the playlists currently feeding the radio's song pool
    internal static List<string> SourcePlaylistIds()
    {
        return SourcePlaylistWeights().Keys.ToList();
    }

    private static Dictionary<string, int> SourcePlaylistWeights()
    {
        var livePlaylistIds = PlaylistListModel.PlayLists.Select(playlist => playlist.PlayListId).ToHashSet();

        var configuredPlaylistWeights = RadioAudiobookshelfConfigModel.Config.PlaylistWeights;
        var playlistWeights = ActiveWeights(configuredPlaylistWeights);
        if (playlistWeights.Count == 0)
        {
            return PlaylistListModel.PlayLists
                .Where(playlist => RotationTag.IsMatch(playlist.PlayListTitle ?? string.Empty))
                .Where(playlist => !RadioAudiobookshelfConfigModel.IsExplicitlyOff(configuredPlaylistWeights, playlist.PlayListId))
                .ToDictionary(playlist => playlist.PlayListId, _ => 1);
        }

        return playlistWeights
            .Where(entry => livePlaylistIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static List<RadioAudiobookshelfItem> BuildSongPool()
    {
        var playlistWeights = SourcePlaylistWeights();

        var playlistTitles = new Dictionary<string, string>();
        foreach (var playlist in PlaylistListModel.PlayLists) playlistTitles[playlist.PlayListId] = playlist.PlayListTitle;

        var songPulls = new Dictionary<string, (RadioSong Track, double Pull, List<string> SourceIds)>();
        foreach (var (playlistId, weight) in playlistWeights)
        {
            var tracks = RotationTracksModel.GetTracks(playlistId);
            if (tracks == null) return null;

            double pull = EffectiveWeight(weight) * RotationMultiplier(playlistTitles.GetValueOrDefault(playlistId));

            foreach (var track in tracks)
            {
                if (songPulls.TryGetValue(track.SongUri, out var existing))
                {
                    if (existing.Pull > pull) continue;
                    if (existing.Pull == pull)
                    {
                        existing.SourceIds.Add(playlistId);
                        continue;
                    }
                }
                songPulls[track.SongUri] = (track, pull, new List<string> { playlistId });
            }
        }

        var random = new Random();
        return songPulls.Values
            .OrderByDescending(entry => Math.Pow(random.NextDouble(), 1.0 / entry.Pull))
            .Select(entry => RadioAudiobookshelfItem.ForSong(entry.Track.SongTitle, entry.Track.SongArtists, entry.Track.SongImageUrl, entry.Track.SongUri,
                SongSourceLabel(entry.SourceIds, playlistTitles)))
            .ToList();
    }

    private static int RotationMultiplier(string playlistTitle)
    {
        var match = RotationTag.Match(playlistTitle ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[1].Value, out var level) ? level + 1 : 1;
    }


    private static string SongSourceLabel(List<string> playlistIds, Dictionary<string, string> playlistTitles)
    {
        int rotationLevel = -1;
        foreach (var playlistId in playlistIds)
        {
            var match = RotationTag.Match(playlistTitles.GetValueOrDefault(playlistId) ?? string.Empty);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var level)) rotationLevel = Math.Max(rotationLevel, level);
        }

        if (rotationLevel >= 0) return $"R{rotationLevel}";
        if (playlistIds.Contains(Constants.LIKED_PLALIST_ID)) return "Liked";
        return ShortSourceName(playlistTitles.GetValueOrDefault(playlistIds[0]));
    }

    private static string ShortSourceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        name = name.Trim();
        return name.Length <= SOURCE_LABEL_MAX_CHARS ? name : name.Substring(0, SOURCE_LABEL_MAX_CHARS - 1).TrimEnd() + "…";
    }

    private static Dictionary<string, int> ActiveWeights(Dictionary<string, int> weights)
    {
        return weights.Where(entry => entry.Value > 0).ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static double EffectiveWeight(int weight) => (double)weight * weight;
}
