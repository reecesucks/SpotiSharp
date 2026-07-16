using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class DetailPlaylistPageViewModel : BaseViewModel
{
    private string _playlistId;

    public string PlaylistId
    {
        get { return _playlistId; }
        set
        {
            SetProperty(ref _playlistId, value);
            RefreshPlaylistInfo();
        }
    }
    
    private string _playlistName;

    public string PlaylistName
    {
        get { return _playlistName; }
        set { SetProperty(ref _playlistName, value); }
    }

    private DetailPlaylistModel _detailPlaylistModel;
    private string _latestRequestedPlaylistId;

    public DetailPlaylistPageViewModel()
    {
        _detailPlaylistModel = new DetailPlaylistModel();
    }

    private async void RefreshPlaylistInfo()
    {
        if (PlaylistId == null) return;

        var playlistId = PlaylistId;
        _latestRequestedPlaylistId = playlistId;

        var name = await Task.Run(() => _detailPlaylistModel.GetPlaylistName(playlistId));
        if (playlistId != _latestRequestedPlaylistId) return;

        PlaylistName = name;
    }
}