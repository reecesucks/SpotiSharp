using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels;

public class RadioPageViewModel : BaseViewModel
{
    public ICommand GenerateRadio { get; }
    public ICommand OpenSettings { get; }

    public RadioPageViewModel()
    {
        GenerateRadio = new Command(async () => await GenerateAsync());
        OpenSettings = new Command(async () => await Shell.Current.GoToAsync("RadioSettingsPage"));
    }

    private bool _isGenerating;

    public bool IsGenerating
    {
        get { return _isGenerating; }
        private set { SetProperty(ref _isGenerating, value); }
    }

    private List<RadioItem> _items = new List<RadioItem>();

    public List<RadioItem> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    private async Task GenerateAsync()
    {
        if (IsGenerating) return;
        IsGenerating = true;

        var items = await Task.Run(RadioModel.Generate);
        if (items != null) Items = items;

        IsGenerating = false;
    }

    public async void ClickItem(object sourceItem)
    {
        if (sourceItem is not RadioItem radioItem) return;

        if (PlaybackStateStore.Instance.HasActiveDevice)
        {
            bool started = await Task.Run(() => APICaller.Instance?.PlayUriAtPosition(radioItem.PlayUri, radioItem.PositionMs) ?? false);
            if (started) return;
        }

        if (!await LaunchInSpotify(radioItem.PlayUri))
        {
            await Shell.Current.DisplayAlert("Playback failed", "Couldn't start playback. Make sure Spotify is installed and you're signed in.", "OK");
        }
    }

    private static async Task<bool> LaunchInSpotify(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;
        try
        {
            return await Launcher.Default.TryOpenAsync(uri);
        }
        catch
        {
            return false;
        }
    }
}
