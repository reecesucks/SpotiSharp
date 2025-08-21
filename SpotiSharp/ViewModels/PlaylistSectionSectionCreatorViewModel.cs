
using SpotiSharp.Enums;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels
{
    public class PlaylistSectionSectionCreatorViewModel : BaseViewModel
    {
        public PlaylistSectionSectionCreatorViewModel()
        {
            _playlists = PlaylistListModel.PlayLists.Select(p => p).ToList();
            _playlistSectionTypes = EnumHelper.GetEnumListAsDictionary<PlaylistSectionEnum>().Select(p => new PlaylistSectionType(p.Key, p.Value)).ToList();
        }


        private int _numericValue;
        private List<Playlist> _playlists;
        private Playlist _SelectedPlaylist;
        private PlaylistSectionType _playlistSectionType;
        private List<PlaylistSectionType> _playlistSectionTypes;

        public List<Playlist> Playlists
        {
            get { return _playlists; }
            set { SetProperty(ref _playlists, value); }
        }
        public Playlist SelectedPlaylist
        {
            get { return _SelectedPlaylist; }
            set { SetProperty(ref _SelectedPlaylist, value); }
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
    }
}
