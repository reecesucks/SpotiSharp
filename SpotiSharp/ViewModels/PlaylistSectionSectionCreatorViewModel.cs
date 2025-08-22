
using SpotifyAPI.Web;
using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels
{
    public class PlaylistSectionSectionCreatorViewModel : BaseViewModel
    {
        public PlaylistSectionSectionCreatorViewModel()
        {
            _playlists = PlaylistListModel.PlayLists.Select(p => p).ToList();
            _playlistSectionTypes = EnumHelper.GetEnumListAsDictionary<PlaylistSectionEnum>().Select(p => new PlaylistSectionType((PlaylistSectionEnum)p.Key, p.Value)).ToList();
        }

        private int _numericValue;
        private List<Playlist> _playlists;
        private Playlist _selectedPlaylist;
        private PlaylistSectionType _playlistSectionType;
        private List<PlaylistSectionType> _playlistSectionTypes;
        private List<FullTrack> _selectedPlaylistTracks;

        public List<Playlist> Playlists
        {
            get { return _playlists; }
            set { SetProperty(ref _playlists, value); }
        }
        public Playlist SelectedPlaylist
        {
            get { return _selectedPlaylist; }
            set { SetProperty(ref _selectedPlaylist, value); }
        }

        public PlaylistSectionType SelectedSectionType
        {
            get { return _playlistSectionType; }
            set { SetProperty(ref _playlistSectionType, value); }
        }

        public List<PlaylistSectionType> PlaylistSectionTypes
        {
            get { return _playlistSectionTypes; }
        }

        public int NumericValue
        {
            get { return _numericValue; }
            set { SetProperty(ref _numericValue, value); }
        }

        public List<FullTrack> SelectedPlaylistTracks
        {
            get { return _selectedPlaylistTracks; }
            set { SetProperty(ref _selectedPlaylistTracks, value); }
        }

        public async void OnSelectedPlaylistChanged(Playlist playlist)
        {
            var songListModel = new SongsListModel(playlist.PlayListId);
            var apiCallerInstance = await APICaller.WaitForRateLimitWindowInstance;
            SelectedPlaylistTracks =  apiCallerInstance?.GetMultipleTracksByTrackId(songListModel.Songs.Select(s => s.SongId).ToList());
        }
    }
}
