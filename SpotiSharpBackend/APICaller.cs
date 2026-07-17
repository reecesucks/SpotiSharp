using System.ComponentModel;
using System.Diagnostics;
using SpotifyAPI.Web;

namespace SpotiSharpBackend;

public class APICaller
{
    private static readonly APICaller ApiCaller = new();

    public static APICaller? Instance
    {
        get
        {
            if (Authentication.SpotifyClient != null && Ratelimiter.CanRequestCall())
            {
                return ApiCaller;
            }

            return null;
        }
    }

    public static Task<APICaller?> WaitForRateLimitWindowInstance => GetInstanceAsync();

    private static async Task<bool> DelayLoop()
    {
        await Task.Delay(100);
        return true;
    }
    
    public static async Task<APICaller?> GetInstanceAsync()
    {
        APICaller? instance = null;
        var retries = 0;
        do
        {
            instance = Instance;
            retries++;
        } while (instance == null && retries < 100 && await DelayLoop());

        return instance;
    }

    private const int MAX_RETRIES = 20;
    private const int TIME_OUT_IN_MILLI = 100;

    private List<FullArtist> _cachedArtists = new List<FullArtist>();

    private APICaller() {}
    
    private T HandleExceptionsNonAbstract<T>(Func<T> call) where T : new()
    {
        var result = HandleExceptions(call);
        return result == null ? new T() : result;
    }

    private T? HandleExceptions<T>(Func<T> call)
    {
        int currentRetries = 0;
        while (currentRetries < MAX_RETRIES)
        {
            try
            {
                if (Ratelimiter.RequestCall()) return call();
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"[APICaller] call failed (retry {currentRetries}): {ex.InnerException?.Message ?? ex.Message}");
            }
            currentRetries++;
            Thread.Sleep(TIME_OUT_IN_MILLI);
        }
        return default;
    }

    private List<T> ChunkRequest<T>(List<object> inputElements, int chunckSize, Func<List<object>, List<T>> func)
    {
        int chuncks = (int)Math.Ceiling(inputElements.Count / (double)chunckSize);
        var result = new List<T>();
        for (int i = 0; i < chuncks; i++)
        {
            int rangeStart = 0 + i * chunckSize;
            int rangeCount = chunckSize;
            if (i == chuncks - 1) rangeCount = inputElements.Count - (chunckSize * (chuncks - 1));
            
            List<object> subsetOfInputElements = inputElements.GetRange(rangeStart, rangeCount);
            result.AddRange(func(subsetOfInputElements));
            Thread.Sleep(100);
        }
        return result;
    }


    #region PlayList

    public FullPlaylist GetPlaylistById(string playlistId)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient?.Playlists.Get(playlistId, new PlaylistGetRequest(PlaylistGetRequest.AdditionalTypes.Track)).Result) 
               ?? new FullPlaylist();
    }

    public IList<FullPlaylist>? GetAllUserPlaylists()
    {
        var response = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Playlists.CurrentUsers().Result);
        return HandleExceptions(() => Authentication.SpotifyClient?.PaginateAll(response, new CustomPaginator()).Result);
        
    }

    public IList<PlaylistTrack<IPlayableItem>> GetTracksByPlaylistId(string playlistId)
    {
        FullPlaylist playlist = GetPlaylistById(playlistId);
        IList<PlaylistTrack<IPlayableItem>>? result;
        if (playlist.Tracks.Total > playlist.Tracks.Limit)
        {
            result = HandleExceptions(() => Authentication.SpotifyClient.PaginateAll(playlist.Tracks).Result);
        }
        else
        {
            result = playlist.Tracks.Items;
        }
        return result ?? new List<PlaylistTrack<IPlayableItem>>();
    }

    public List<string> GetTrackIdsByPlaylistId(string playlistId)
    {
        IList<PlaylistTrack<IPlayableItem>> songs = GetTracksByPlaylistId(playlistId);
        return (from song in songs
            let songAsTrack = song.Track as FullTrack
            select songAsTrack.Uri).ToList();
    }

    public List<string> CreatePlaylistWithTrackUris(string playlistName, List<string> trackUris)
    {
        if (!trackUris.Any()) return new List<string>();
        string userId = HandleExceptions(() => Authentication.SpotifyClient.UserProfile.Current().Result.Id);
        FullPlaylist playlist = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Playlists.Create(userId, new PlaylistCreateRequest(playlistName)).Result);

        var apiCallFunc = List<string>(List<object> trackUris) => HandleExceptionsNonAbstract(() =>
        {
            Authentication.SpotifyClient.Playlists.AddItems(playlist.Id, new PlaylistAddItemsRequest(trackUris.ConvertAll(tu => (string)tu)));
            return trackUris.ConvertAll(tu => (string)tu);
        });

        return trackUris.Count < 100 ?
            apiCallFunc(trackUris.ConvertAll(tu => (object)tu)) :
            ChunkRequest(trackUris.ConvertAll(tu => (object)tu), 100, apiCallFunc);
    }

    public FullPlaylist? CreatePlaylist(string playlistName)
    {
        string? userId = HandleExceptions(() => Authentication.SpotifyClient.UserProfile.Current().Result.Id);
        if (userId == null) return null;
        return HandleExceptions(() => Authentication.SpotifyClient.Playlists.Create(userId, new PlaylistCreateRequest(playlistName)).Result);
    }

    public bool AddTrackToPlaylist(string playlistId, string trackUri)
    {
        return HandleExceptions(() => Authentication.SpotifyClient.Playlists.AddItems(playlistId, new PlaylistAddItemsRequest(new List<string> { trackUri })).Result) != null;
    }

    public bool RemoveTrackFromPlaylist(string playlistId, string trackUri)
    {
        var request = new PlaylistRemoveItemsRequest
        {
            Tracks = new List<PlaylistRemoveItemsRequest.Item> { new PlaylistRemoveItemsRequest.Item { Uri = trackUri } }
        };
        return HandleExceptions(() => Authentication.SpotifyClient.Playlists.RemoveItems(playlistId, request).Result) != null;
    }

    #endregion

    #region Track

    public FullTrack GetTrackById(string trackId)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Tracks.Get(trackId).Result);
    }

    public List<FullTrack> GetMultipleTracksByTrackId(List<string> trackIds)
    {
        var apiCallFunc = List<FullTrack>(List<object> trackIds) => HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Tracks.GetSeveral(new TracksRequest(trackIds.ConvertAll(ti => (string)ti))).Result.Tracks);
        
        if (trackIds.Count <= 50) return trackIds.Any() ? 
            apiCallFunc(trackIds.ConvertAll(ti => (object)ti)) : 
            new List<FullTrack>();
        
        return ChunkRequest(trackIds.ConvertAll(ti => (object)ti), 50, apiCallFunc);
    }

    public TrackAudioFeatures GetAudioFeaturesByTrackId(string trackId)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Tracks.GetAudioFeatures(trackId).Result);
    }

    public List<TrackAudioFeatures> GetMultipleTrackAudioFeaturesByTrackIds(List<string> trackIds)
    {
        var apiCallFunc = List<TrackAudioFeatures>(List<object> trackIds) => HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Tracks.GetSeveralAudioFeatures(new TracksAudioFeaturesRequest(trackIds.ConvertAll(ti => (string)ti))).Result.AudioFeatures);
        if (trackIds.Count <= 100) return trackIds.Any() ? 
            apiCallFunc(trackIds.ConvertAll(ti => (object)ti)) : 
            new List<TrackAudioFeatures>();
        return ChunkRequest(trackIds.ConvertAll(ti => (object)ti), 100, apiCallFunc);
    }

    #endregion

    #region Artist

    public FullArtist GetArtistById(string id)
    {
        var cachedArtist = _cachedArtists.FirstOrDefault(a => a.Id == id);
        if (cachedArtist != null) return cachedArtist;
        var artist = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Artists.Get(id).Result);
        _cachedArtists.Add(artist);
        return artist;
    }

    public List<string> GetGenresByArtistId(string id)
    {
        return GetArtistById(id).Genres;
    }

    public List<FullArtist>? GetFollowedArtists()
    {
        return HandleExceptions(() =>
        {
            var result = new List<FullArtist>();
            string? after = null;
            do
            {
                var request = new FollowOfCurrentUserRequest(FollowOfCurrentUserRequest.Type.Artist) { Limit = 50, After = after };
                var response = Authentication.SpotifyClient.Follow.OfCurrentUser(request).Result;
                if (response?.Artists?.Items == null) break;
                result.AddRange(response.Artists.Items);
                after = response.Artists.Cursors?.After;
            } while (!string.IsNullOrEmpty(after));
            return result;
        });
    }

    public List<SimpleAlbum>? GetArtistAlbums(string artistId)
    {
        var response = HandleExceptions(() => Authentication.SpotifyClient.Artists.GetAlbums(artistId, new ArtistsAlbumsRequest
        {
            Limit = 50,
            IncludeGroupsParam = ArtistsAlbumsRequest.IncludeGroups.Album | ArtistsAlbumsRequest.IncludeGroups.Single
        }).Result);
        if (response == null) return null;

        return HandleExceptions(() => Authentication.SpotifyClient.PaginateAll(response).Result)?.ToList();
    }

    public List<SimpleTrack>? GetAlbumTracks(string albumId)
    {
        var response = HandleExceptions(() => Authentication.SpotifyClient.Albums.GetTracks(albumId, new AlbumTracksRequest { Limit = 50 }).Result);
        if (response == null) return null;

        return HandleExceptions(() => Authentication.SpotifyClient.PaginateAll(response).Result)?.ToList();
    }

    public bool? IsAlbumSaved(string albumId)
    {
        var result = HandleExceptions(() => Authentication.SpotifyClient.Library.CheckAlbums(new LibraryCheckAlbumsRequest(new List<string> { albumId })).Result);
        if (result == null || result.Count == 0) return null;
        return result[0];
    }

    public bool SaveAlbum(string albumId)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Library.SaveAlbums(new LibrarySaveAlbumsRequest(new List<string> { albumId })).Result);
    }

    public bool RemoveSavedAlbum(string albumId)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Library.RemoveAlbums(new LibraryRemoveAlbumsRequest(new List<string> { albumId })).Result);
    }

    #endregion

    #region Player

    public CurrentlyPlayingContext GetCurrentPlaybackContext()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result);
    }
    
    public CurrentlyPlaying GetCurrentSong()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest()).Result);
    }

    public bool SetCurrentPlayingSong(string songUri, string playlistId)
    {
        if (songUri == null) return false;

        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest
        {
            ContextUri = $"spotify:playlist:{playlistId}",
            OffsetParam = new PlayerResumePlaybackRequest.Offset
            {
                Uri = songUri
            }
        }).Result);
    }

    public bool SetCurrentPlayingSongInAlbum(string songUri, string albumId)
    {
        if (songUri == null) return false;

        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest
        {
            ContextUri = $"spotify:album:{albumId}",
            OffsetParam = new PlayerResumePlaybackRequest.Offset
            {
                Uri = songUri
            }
        }).Result);
    }

    public bool SetCurrentPlayingToPlaylist(string playlistId)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest
        {
            ContextUri = $"spotify:playlist:{playlistId}"
        }).Result);
    }

    public bool SetCurrentPlayingToSongInLikedPlaylist(string songId)
    {
        var likedSongs = GetUserLikedSongs().Select(ls => ls.Track).ToList();
        var likedSongIds = likedSongs.Select(ls => ls.Id).ToList();
        var indexOfSelectedSong = likedSongIds.IndexOf(songId);
        var songCount = likedSongIds.Count - indexOfSelectedSong;
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest
        {
            Uris = likedSongs.Select(ls => ls.Uri).ToList().GetRange(indexOfSelectedSong, songCount)
        }).Result);
    }

    public bool TogglePlaybackStatus()
    {
        CurrentlyPlayingContext playContext = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.GetCurrentPlayback().Result);
        return playContext.IsPlaying
            ? HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.PausePlayback().Result)
            : HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.ResumePlayback().Result);
    }

    public bool ChangePlaybackRepeatType()
    {
        var currentlyPlayingContext = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.GetCurrentPlayback().Result);
        if (!Enum.TryParse(currentlyPlayingContext.RepeatState, true, out PlayerSetRepeatRequest.State currentRepeatState)) return false;

        var states = Enum.GetValues<PlayerSetRepeatRequest.State>().Reverse().ToArray();
        int indexOfNextItem = Array.IndexOf(states, currentRepeatState) + 1;
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.SetRepeat(new PlayerSetRepeatRequest(states[indexOfNextItem % states.Length])).Result);
    }

    public bool GetCurrentPlaybackShuffleState()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.GetCurrentPlayback().Result.ShuffleState);
    }

    public bool TogglePlaybackShuffle()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.SetShuffle(new PlayerShuffleRequest(!GetCurrentPlaybackShuffleState())).Result);
    }
    
    public bool SetPlaybackShuffle(bool value)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.SetShuffle(new PlayerShuffleRequest(value)).Result);
    }

    public bool SkipToNextSong()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.SkipNext().Result);
    }

    public bool SkipToPreviousSong()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.SkipPrevious().Result);
    }

    public bool SetVolume(int volume)
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Player.SetVolume(new PlayerVolumeRequest(volume)).Result);
    }

    #endregion

    #region Profile

    public string? GetUserName()
    {
        return HandleExceptions(() => Authentication.SpotifyClient.UserProfile.Current().Result.DisplayName);
    }

    public string? GetProfilePictureURL()
    {
        return HandleExceptions(() => Authentication.SpotifyClient.UserProfile.Current().Result.Images.ElementAtOrDefault(0)?.Url ?? string.Empty);
    }

    public Followers GetUserFollows()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.UserProfile.Current().Result.Followers);
    }
    
    public List<SavedTrack> GetUserLikedSongs()
    {
        var result = new List<SavedTrack>();
        int? totalLiked = null;
        int totalRecived = 0;
        while (totalLiked == null || totalRecived < totalLiked)
        {
            var req = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Library.GetTracks(new LibraryTracksRequest { Limit = 50, Offset = totalRecived }).Result);
            totalLiked ??= req.Total;
            if (req.Items != null)
            {
                result.AddRange(req.Items);
                totalRecived += 50;
            }
        }
        return result;
    }
    
    public int GetUserLikedSongsAmount()
    {
        return HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Library.GetTracks().Result.Total ?? 0);
    }

    public List<SavedShow> GetSavedShows()
    {
        var result = new List<SavedShow>();
        var req = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Library.GetShows().Result);

        if (req.Items != null)
        {
            result.AddRange(req.Items);
        }

        return result;
    }

    public List<SimpleEpisode> GetPodcastEpisodesByPodcastId(string showId)
    {
        var result = new List<SimpleEpisode>();
        var req = HandleExceptionsNonAbstract(() => Authentication.SpotifyClient.Shows.GetEpisodes(showId).Result);

        if (req.Items != null)
        {
            result.AddRange(req.Items);
        }
        return result;
    }

    #endregion
}