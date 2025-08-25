using SpotifyAPI.Web;
using SpotiSharp.Models;
using SpotiSharpBackend;
using static System.Net.Mime.MediaTypeNames;


namespace SpotiSharp.ViewModels
{
    public class PodcastSectionSectionCreatorViewModel : BaseViewModel
    {
        private int _numericValue;
        private List<FullShow> _savedShows;
        private FullShow _selectedSavedShow;
        private List<FullEpisode> _selectedFullEpisodes;

        private PlaylistSectionType _playlistSectionType;
        private List<PlaylistSectionType> _playlistSectionTypes;
       
        public PodcastSectionSectionCreatorViewModel()
        {
            _savedShows = PlaylistListModel.SavedShows.ToList();
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

        public List<FullEpisode> SelectedFullEpisodes
        {
            get { return _selectedFullEpisodes; }
            set { SetProperty(ref _selectedFullEpisodes, value); }
        }

        public async void OnSelectedPodcastChanged(SavedShow savedShow)
        {
            //var songListModel = new SongsListModel(savedShow.);
            //var apiCallerInstance = await APICaller.WaitForRateLimitWindowInstance;
           // SelectedPlaylistTracks = apiCallerInstance?.GetMultipleTracksByTrackId(songListModel.Songs.Select(s => s.SongId).ToList());
        }
    }
}
