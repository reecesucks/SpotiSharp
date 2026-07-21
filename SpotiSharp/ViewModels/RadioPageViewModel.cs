using System.Collections.ObjectModel;
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
        _ = LoadCachedRadioAsync();
    }

    public void RemoveRadioItem(RadioItem item)
    {
        if (item == null) return;

        if (item.IsPodcastSegment)
        {
            var segments = Items.Where(current => current.IsPodcastSegment && current.PlayUri == item.PlayUri).ToList();
            foreach (var segment in segments) Items.Remove(segment);
        }
        else if (!Items.Remove(item))
        {
            return;
        }

        var snapshot = Items.ToList();
        Task.Run(() => RadioModel.SaveRadio(snapshot));
    }

    private async Task LoadCachedRadioAsync()
    {
        var cached = await Task.Run(() => RadioModel.CachedRadio);
        if (IsGenerating || Items.Count > 0) return;
        if (cached != null && cached.Count > 0) Items = new ObservableCollection<RadioItem>(cached);
    }

    private bool _isGenerating;

    public bool IsGenerating
    {
        get { return _isGenerating; }
        private set { SetProperty(ref _isGenerating, value); }
    }

    private ObservableCollection<RadioItem> _items = new ObservableCollection<RadioItem>();

    public ObservableCollection<RadioItem> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    private async Task GenerateAsync()
    {
        if (IsGenerating) return;
        IsGenerating = true;
        RadioConductor.Instance.Stop();

        var items = await Task.Run(RadioModel.Generate);
        if (items != null) Items = new ObservableCollection<RadioItem>(items);

        IsGenerating = false;
    }

    public async void ClickItem(object sourceItem)
    {
        if (sourceItem is not RadioItem radioItem) return;

        if (PlaybackStateStore.Instance.HasActiveDevice)
        {
            var songRun = radioItem.IsPodcastSegment
                ? null
                : Items
                    .SkipWhile(item => item != radioItem)
                    .TakeWhile(item => !item.IsPodcastSegment)
                    .Select(item => item.PlayUri)
                    .ToList();

            bool started = await Task.Run(() =>
            {
                var api = APICaller.Instance;
                if (api == null) return false;

                api.SetPlaybackShuffle(false);

                return radioItem.IsPodcastSegment
                    ? api.PlayUriAtPosition(radioItem.PlayUri, radioItem.PositionMs)
                    : api.PlayUris(songRun);
            });
            if (started)
            {
                RadioConductor.Instance.Start(Items.ToList(), Items.IndexOf(radioItem));
                return;
            }
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
