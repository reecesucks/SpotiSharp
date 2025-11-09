using System.Windows.Input;
using SpotiSharpBackend;
using SpotiSharp.Models;
using SpotiSharpBackend.Enums;
using System.Collections.ObjectModel;
using SpotifyAPI.Web;
using SpotiSharp.Classes;
using System.Text.Json;
using SpotiSharp.Helpers;

namespace SpotiSharp.ViewModels;

public delegate void AddingFilter(TrackFilter trackFilter, Guid guid, List<object> parameters);

public class PlaylistCreatorPageViewModel : BaseViewModel
{
    public static event AddingFilter OnAddingFilter;

    #region "Locals"
    private List<string> _playlistUris = new List<string>();
    private SectionCreatorClass _sectionCreatorClass;
    private bool _isAuthenticated = false;
    private string _playlistName;
    private string _selectedPlaylistNameId;
    private List<string> _playlistNamesIds;
    private List<string> _savedShowsNamesIds;
    private TrackFilter _selectedFilter;
    private List<TrackFilter> _filters = Enum.GetValues<TrackFilter>().ToList();
    private bool _isFilteringPlaylist = false;
    private TemplateStorageHelper _templateStorageHelper;

    #endregion

    #region "Properties"
    public List<String> Playlist
    {
        get { return _playlistUris; }
        set { SetProperty(ref _playlistUris, value);}
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
        OpenTemplate = new Command(OpenTemplateHandler);
        SaveTemplate = new Command(SaveTemplateHandler);
        ClearTemplate = new Command(ClearTemplateHandler);

        //CreatePlaylist = new Command(PlaylistCreatorPageModel.CreatePlaylist);
        CreatePlaylist = new Command(CreatePlaylistBySections);

        PlaylistCreationSonglistViewModel.OnPlalistIsFiltered += () => IsFilteringPlaylist = false;

        _sectionCreatorClass = new SectionCreatorClass(_playlistUris);
        _templateStorageHelper = new TemplateStorageHelper();
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
    public ICommand OpenTemplate { private set; get; }
    public ICommand SaveTemplate { private set; get; }
    public ICommand ClearTemplate { private set; get; }

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
                if (sect.IsMusic)
                {
                    _sectionCreatorClass.CreateMusicSection(sect);
                }
                else
                {
                    _sectionCreatorClass.CreatePodcastSection(sect);
                }
            }

         PlaylistCreatorPageModel.CreatePlaylistWithUriList("test name", _playlistUris);
        }
        catch (Exception ex)
        {
            //logging doesn't exist yet
        }
    }
    private async void OpenTemplateHandler()
    {
        try
        {
            var jsonString = await _templateStorageHelper.GetUserTemplateSelection();
            ObservableCollection<PlaylistSectionSectionCreatorViewModel> playlistTemplate = JsonSerializer.Deserialize<ObservableCollection<PlaylistSectionSectionCreatorViewModel>>(jsonString);
            
            PlaylistSectionCreationList.Clear();
            
            foreach (var item in playlistTemplate)
            {
                PlaylistSectionCreationList.Add(item);

                if (item.IsPodcast)
                {
                    item.SelectedSectionTypePod = item.PodcastSectionTypes.First(t => t.SectionType == item.SelectedSectionTypePod.SectionType);
                    item.SelectedSavedShow = item.SavedShows.First(e => e.Uri == item.SelectedSavedShow.Uri);
                }
                else
                {
                    item.SelectedSectionType = item.PlaylistSectionTypes.First(t => t.SectionType == item.SelectedSectionType.SectionType);
                    var uris = item.MultiPickerSelections.Select(mpt => mpt.Uri).ToList();
                    item.MultiPickerSelections.Clear();
                    
                    foreach (FullTrack i in item.SelectedPlaylistTracks)
                    {
                        if (uris.Contains(i.Uri))
                        {
                            item.MultiPickerSelections.Add(i);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {

        }
    }

    private void SaveTemplateHandler()
    {
        _templateStorageHelper.SaveTemplate(PlaylistName, PlaylistSectionCreationList);       
    }

    private void ClearTemplateHandler()
    {
        PlaylistSectionCreationList.Clear();
    }
}