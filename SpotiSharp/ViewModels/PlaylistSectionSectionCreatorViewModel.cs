
using System.Collections.ObjectModel;
using SpotifyAPI.Web;
using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels
{
    public class PlaylistSectionSectionCreatorViewModel : BaseViewModel
    {
        public PlaylistSectionSectionCreatorViewModel(bool isPodcast)
        {
            SetMode(isPodcast);
            _multiPickerSelections = new ObservableCollection<FullTrack>();
        }

        private bool _isPodcast;
        private int _numericValue;
        private List<Playlist> _playlists;
        private Playlist _selectedPlaylist;
        private PlaylistSectionType _playlistSectionType;
        private List<PlaylistSectionType> _playlistSectionTypes;
        private List<FullTrack> _selectedPlaylistTracks;
        private ObservableCollection<FullTrack> _multiPickerSelections;

        private List<FullShow> _savedShows;
        private FullShow _selectedSavedShow;
        private List<SimpleEpisode> _selectedSimpleEpisode;
        private List<PodcastSectionType> _podcastSectionTypes;
        private PodcastSectionType _selectedSectionType;

        public bool IsPodcast {  get { return _isPodcast; } }
        public bool IsMusic { get { return !_isPodcast; } }

        public Brush Background => IsPodcast ? new SolidColorBrush(Color.FromHex("#FFA500"))  :  new SolidColorBrush(Color.FromHex("#1DB954"));

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

        public ObservableCollection<FullTrack> MultiPickerSelections
        {
            get { return _multiPickerSelections; }
            set { SetProperty(ref _multiPickerSelections, value); }
        }

        #region "PodCast Properties"
        public List<SimpleEpisode> SelectedFullEpisodes
        {
            get { return _selectedSimpleEpisode; }
            set { SetProperty(ref _selectedSimpleEpisode, value); }
        }
        public List<FullShow> SavedShows
        {
            get { return _savedShows; }
            set { SetProperty(ref _savedShows, value); }
        }
        public FullShow SelectedSavedShow
        {
            get { return _selectedSavedShow; }
            set { SetProperty(ref _selectedSavedShow, value); }
        }
        public List<PodcastSectionType> PodcastSectionTypes
        {
            get { return _podcastSectionTypes; }
        }
        public PodcastSectionType SelectedSectionTypePod
        {
            get { return _selectedSectionType; }
            set { SetProperty(ref _selectedSectionType, value); }
        }
        #endregion

        private void SetMode(bool isPodcast)
        {
            _isPodcast = isPodcast;

            if (isPodcast)
            {
                _savedShows = PlaylistListModel.SavedShows.ToList();
                _podcastSectionTypes = EnumHelper.GetEnumListAsDictionary<PodcastSectionEnum>().Select(p => new PodcastSectionType((PodcastSectionEnum)p.Key, p.Value)).ToList();
            }
            else
            {
                _playlists = PlaylistListModel.PlayLists.Select(p => p).ToList();
                _playlistSectionTypes = EnumHelper.GetEnumListAsDictionary<PlaylistSectionEnum>().Select(p => new PlaylistSectionType((PlaylistSectionEnum)p.Key, p.Value)).ToList();
            }
        }

        public async void OnSelectedPlaylistChanged(Playlist playlist)
        {
            var songListModel = new SongsListModel(playlist.PlayListId);
            var apiCallerInstance = await APICaller.WaitForRateLimitWindowInstance;
            SelectedPlaylistTracks =  apiCallerInstance?.GetMultipleTracksByTrackId(songListModel.Songs.Select(s => s.SongId).ToList());
        }
        public async void OnSelectedPodcastChanged(FullShow savedShow)
        {
            var songListModel = new PodcastShowModel(savedShow);
            SelectedFullEpisodes = songListModel.Episodes;
        }
    }
}
