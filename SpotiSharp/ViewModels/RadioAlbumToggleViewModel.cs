using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioAlbumToggleViewModel : BaseViewModel
{
    public string Id { get; }
    public string Name { get; }
    public string ArtistNames { get; }
    public string ImageUrl { get; }

    public ICommand CycleMode { get; }

    private RadioAlbumMode _mode;

    public string ModeLabel => _mode switch
    {
        RadioAlbumMode.Scattered => "Scattered",
        RadioAlbumMode.Consecutive => "Consecutive",
        _ => "Off"
    };

    public bool IsIncluded => _mode != RadioAlbumMode.Off;

    public RadioAlbumToggleViewModel(string id, string name, string artistNames, string imageUrl, RadioAlbumMode mode)
    {
        Id = id;
        Name = name;
        ArtistNames = artistNames;
        ImageUrl = imageUrl;
        _mode = mode;
        CycleMode = new Command(CycleModeFunc);
    }

    private void CycleModeFunc()
    {
        _mode = _mode switch
        {
            RadioAlbumMode.Off => RadioAlbumMode.Scattered,
            RadioAlbumMode.Scattered => RadioAlbumMode.Consecutive,
            _ => RadioAlbumMode.Off
        };
        RadioConfigModel.SetAlbumMode(Id, _mode);
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(IsIncluded));
    }
}
