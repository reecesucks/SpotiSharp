using System.Windows.Input;
using SpotiSharpBackend;
using SpotiSharp.Models;
using SpotiSharpBackend.Enums;
using System.Collections.ObjectModel;
using SpotifyAPI.Web;
using SpotiSharp.Classes;

namespace SpotiSharp.ViewModels;

public delegate void AddingFilter(TrackFilter trackFilter, Guid guid, List<object> parameters);

public class PlaylistCreatorPageViewModel : BaseViewModel
{
    public static event AddingFilter OnAddingFilter;

    #region "Locals"
    private List<FullTrack> _playlist = new List<FullTrack>();
    private SectionCreatorClass _sectionCreatorClass;
    private bool _isAuthenticated = false;
    private string _playlistName;
    private string _selectedPlaylistNameId;
    private List<string> _playlistNamesIds;
    private List<string> _savedShowsNamesIds;
    private TrackFilter _selectedFilter;
    private List<TrackFilter> _filters = Enum.GetValues<TrackFilter>().ToList();
    private bool _isFilteringPlaylist = false;

    #endregion

    #region "Properties"
    public List<FullTrack> Playlist
    {
        get { return _playlist; }
        set { SetProperty(ref _playlist, value);}
    }
    public bool IsAuthenticated
    {
        get { return _isAuthenticated; }
        set { SetProperty(ref _isAuthenticated, value); }
    }
    public string PlaylistName
    {
        get { return _playlistName; }
        set
        {PlaylistCreatorPageModel.PlaylistName = value; SetProperty(ref _playlistName, value);}
    }
    public string SelectedPlaylistNameId
    {
        get { return _selectedPlaylistNameId; }
        set { SetProperty(ref _selectedPlaylistNameId, value); }
    }
    public List<string> PlaylistNamesIds
    {
        get { return _playlistNamesIds; }
        set { SetProperty(ref _playlistNamesIds, value); }
    }
    public List<string> SavedShowsNamesId
    {
        get { return _savedShowsNamesIds; }
        set { SetProperty(ref _savedShowsNamesIds, value); }
    }
    public string SelectedFilter
    {
        get { return _selectedFilter.ToString(); }
        set { SetProperty(ref _selectedFilter, Enum.Parse<TrackFilter>(value)); }
    }
    public List<string> Filters
    {
        get { return _filters.Select(f => f.ToString()).ToList(); }
        set { SetProperty(ref _filters, value.Select(Enum.Parse<TrackFilter>).ToList() ); }
    }
    public bool IsFilteringPlaylist
    {
        get { return _isFilteringPlaylist; }
        set { SetProperty(ref _isFilteringPlaylist, value ); }
    }
    #endregion

    public PlaylistCreatorPageViewModel()
    {
        AddSongsFromPlaylist = new Command(AddSongsFromPlaylistHandler);
        AddFilter = new Command(AddFilterHandler);
        ApplyFilters = new Command(ApplyFiltersHandler);
        AddPlayListSection = new Command(AddPlayListSectionHandler);
        AddPodcastSection = new Command(AddPodcastListSectionHandler);
        
        //CreatePlaylist = new Command(PlaylistCreatorPageModel.CreatePlaylist);
        CreatePlaylist = new Command(CreatePlaylistBySections);

        PlaylistCreationSonglistViewModel.OnPlalistIsFiltered += () => IsFilteringPlaylist = false;

        _sectionCreatorClass = new SectionCreatorClass(_playlist);
    }

    internal override void OnAppearing()
    {
        PlaylistNamesIds = PlaylistListModel.PlayLists.Select(p => $"{p.PlayListTitle}\n{p.PlayListId}").ToList();
        SavedShowsNamesId = PlaylistListModel.SavedShows.Select(s => $"{s.Name}\n{s.Id}").ToList();
        IsAuthenticated = Authentication.SpotifyClient != null;
    }

    private void AddSongsFromPlaylistHandler()
    {
        if (SelectedPlaylistNameId == null) return;
        IsFilteringPlaylist = true;
        PlaylistCreatorPageModel.AddSongsFromPlaylist(SelectedPlaylistNameId.Split("\n")[1]);
    }

    public static void InvokeAddFilter(TrackFilter trackFilter, Guid guid, List<object> parameters)
    {
        OnAddingFilter?.Invoke(trackFilter, guid, parameters);
    }

    private void AddFilterHandler()
    {
        InvokeAddFilter(_selectedFilter, Guid.Empty, null);
    }

    private void ApplyFiltersHandler()
    {
        IsFilteringPlaylist = true;
        if (StorageHandler.IsUsingCollaborationHost)
        {
            CollaborationAPI.Instance.TriggerFiltering();
            IsFilteringPlaylist = false;
            return;
        }
        PlaylistCreatorPageModel.ApplyFilters();
    }

    public ICommand AddSongsFromPlaylist { private set; get; }
    public ICommand AddFilter { private set; get; }
    public ICommand ApplyFilters { private set; get; }
    public ICommand CreatePlaylist { private set; get; }
    public ICommand AddPlayListSection { private set; get; }
    public ICommand AddPodcastSection { private set; get; }

    public ObservableCollection<PlaylistSectionSectionCreatorViewModel> PlaylistSectionCreationList { get; } = new ObservableCollection<PlaylistSectionSectionCreatorViewModel>();

    private void AddPlayListSectionHandler()
    {
        PlaylistSectionCreationList.Add(new PlaylistSectionSectionCreatorViewModel(false));
    }

    private void AddPodcastListSectionHandler()
    {
        PlaylistSectionCreationList.Add(new PlaylistSectionSectionCreatorViewModel(true));
    }

    private void CreatePlaylistBySections()
    {
        try
        {
            foreach (PlaylistSectionSectionCreatorViewModel sect in PlaylistSectionCreationList)
            {
                _sectionCreatorClass.CreateSection(sect);
            }
        }
        catch (Exception ex)
        {
            //logging doesn't exist yet
        }
    }
}