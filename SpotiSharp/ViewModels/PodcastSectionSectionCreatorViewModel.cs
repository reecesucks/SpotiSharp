using SpotifyAPI.Web;
using SpotiSharp.Enums;
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
        private List<SimpleEpisode> _selectedSimpleEpisode;
        private List<PodcastSectionType> _podcastSectionTypes;
        
        private PodcastSectionType _selectedSectionType;

        public PodcastSectionSectionCreatorViewModel()
        {
            _savedShows = PlaylistListModel.SavedShows.ToList();
            _podcastSectionTypes = EnumHelper.GetEnumListAsDictionary<PodcastSectionEnum>().Select(p => new PodcastSectionType((PodcastSectionEnum)p.Key, p.Value)).ToList();
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

        public PodcastSectionType SelectedSectionType
        {
            get { return _selectedSectionType; }
            set { SetProperty(ref _selectedSectionType, value); }
        }

        public List<PodcastSectionType> PlaylistSectionTypes
        {
            get { return _podcastSectionTypes; }
        }

        public int NumericValue
        {
            get { return _numericValue; }
            set { SetProperty(ref _numericValue, value); }
        }

        public List<SimpleEpisode> SelectedFullEpisodes
        {
            get { return _selectedSimpleEpisode; }
            set { SetProperty(ref _selectedSimpleEpisode, value); }
        }

        public async void OnSelectedPodcastChanged(FullShow savedShow)
        {
            var songListModel = new PodcastShowModel(savedShow);
            SelectedFullEpisodes = songListModel.Episodes;
        }
    }
}
