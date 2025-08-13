
using System.Linq;
using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels
{
    public class PlaylistSectionSectionCreatorViewModel : BaseViewModel
    {
        public PlaylistSectionSectionCreatorViewModel()
        {
            _playlists = PlaylistListModel.PlayLists.Select(p => p).ToList();
            _playlistSectionTypes = EnumHelper.GetEnumListAsDictionary<PlaylistSectionEnum>().Select(p => new PlaylistSectionType(p.Key, p.Value)).ToList();
        }

        private List<Playlist> _playlists;
        private Playlist _SelectedPlaylist;
        

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

        private List<PlaylistSectionType> _playlistSectionTypes;

        public List<PlaylistSectionType> PlaylistSectionTypes
        {
            get { return _playlistSectionTypes; }
        }
    }
}
