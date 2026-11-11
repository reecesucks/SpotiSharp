using System.Windows.Input;
using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfAlbumToggleViewModel : BaseViewModel
{
    public string Id { get; }
    public string Name { get; }
    public string ArtistNames { get; }
    public string ImageUrl { get; }

    public ICommand CycleMode { get; }

    private RadioAudiobookshelfAlbumMode _mode;

    public string ModeLabel => _mode switch
    {
        RadioAudiobookshelfAlbumMode.Scattered => "Scattered",
        RadioAudiobookshelfAlbumMode.Consecutive => "Consecutive",
        _ => "Off"
    };

    public bool IsIncluded => _mode != RadioAudiobookshelfAlbumMode.Off;

    public RadioAudiobookshelfAlbumToggleViewModel(string id, string name, string artistNames, string imageUrl, RadioAudiobookshelfAlbumMode mode)
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
            RadioAudiobookshelfAlbumMode.Off => RadioAudiobookshelfAlbumMode.Scattered,
            RadioAudiobookshelfAlbumMode.Scattered => RadioAudiobookshelfAlbumMode.Consecutive,
            _ => RadioAudiobookshelfAlbumMode.Off
        };
        RadioAudiobookshelfConfigModel.SetAlbumMode(Id, _mode);
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(IsIncluded));
    }
}
