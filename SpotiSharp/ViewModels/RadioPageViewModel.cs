using System.Collections.ObjectModel;
using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.ViewModels;

public class RadioPageViewModel : BaseViewModel
{
    public ICommand GenerateRadio { get; }
    public ICommand OpenSettings { get; }

    // bound by the inline remove buttons a long press reveals on a row
    public ICommand RemoveSingle { get; }
    public ICommand RemoveAllSections { get; }

    public RadioPageViewModel()
    {
        GenerateRadio = new Command(async () => await GenerateAsync());
        OpenSettings = new Command(async () => await Shell.Current.GoToAsync("RadioSettingsPage"));
        RemoveSingle = new Command<RadioItem>(RemoveSingleItem);
        RemoveAllSections = new Command<RadioItem>(RemoveEpisode);
        RadioConductor.Instance.ActiveItemChanged += SetCurrentItem;
        _ = LoadCachedRadioAsync();
    }

    private RadioItem _currentItem;

    // highlights whichever row the radio is playing
    private void SetCurrentItem(RadioItem item)
    {
        if (ReferenceEquals(_currentItem, item)) return;

        if (_currentItem != null) _currentItem.IsCurrent = false;
        _currentItem = item;
        if (_currentItem == null) return;

        _currentItem.IsCurrent = true;
        TrimPlayed(_currentItem);
    }

    // dropping a whole podcast leaves a long run of music, so spread the remaining
    // episodes back through the songs and keep roughly the original music/podcast mix
    private void RebalancePodcasts()
    {
        // the playing row stays put, only what is still upcoming gets reshuffled
        int keep = _currentItem != null && Items.Count > 0 && ReferenceEquals(Items[0], _currentItem) ? 1 : 0;

        var upcoming = Items.Skip(keep).ToList();
        var segments = upcoming.Where(radioItem => radioItem.IsPodcastSegment).ToList();
        if (segments.Count == 0) return;

        var songs = upcoming.Where(radioItem => !radioItem.IsPodcastSegment).ToList();

        var rebuilt = new List<RadioItem>();
        int songIndex = 0;
        foreach (var segment in segments)
        {
            for (int i = 0; i < RadioModel.SONGS_BETWEEN_SEGMENTS && songIndex < songs.Count; i++)
            {
                rebuilt.Add(songs[songIndex]);
                songIndex++;
            }
            rebuilt.Add(segment);
        }
        while (songIndex < songs.Count)
        {
            rebuilt.Add(songs[songIndex]);
            songIndex++;
        }

        Items = new ObservableCollection<RadioItem>(Items.Take(keep).Concat(rebuilt));
    }

    // the conductor works from a snapshot taken when playback started, so hand it the
    // edited list (this only re-points its tracking, it does not restart playback)
    private void ResyncConductor()
    {
        if (_currentItem == null || !RadioConductor.Instance.IsActive) return;

        int index = Items.IndexOf(_currentItem);
        if (index >= 0) RadioConductor.Instance.Start(Items.ToList(), index);
    }

    // the radio is consumed as it plays: whatever sits above the playing row has
    // been heard or skipped, so drop it
    private void TrimPlayed(RadioItem current)
    {
        int index = Items.IndexOf(current);
        if (index <= 0) return;

        for (int i = 0; i < index; i++) Items.RemoveAt(0);

        var snapshot = Items.ToList();
        Task.Run(() => RadioModel.SaveRadio(snapshot));
    }

    // long press turns a row into remove buttons; only one row at a time
    public void ShowRemoveOptions(RadioItem item)
    {
        foreach (var current in Items) current.IsConfirmingRemove = ReferenceEquals(current, item);
    }

    public void ClearRemoveOptions()
    {
        foreach (var current in Items) current.IsConfirmingRemove = false;
    }

    // just this row, whether it is a song or a single podcast section
    public void RemoveSingleItem(RadioItem item)
    {
        if (item == null || !Items.Remove(item)) return;
        FinishRemoval();
    }

    // every section of the episode, then spread the remaining podcasts back out
    public void RemoveEpisode(RadioItem item)
    {
        if (item == null || !item.IsPodcastSegment) return;

        var segments = Items.Where(current => current.IsPodcastSegment && current.PlayUri == item.PlayUri).ToList();
        if (segments.Count == 0) return;

        foreach (var segment in segments) Items.Remove(segment);
        RebalancePodcasts();
        FinishRemoval();
    }

    private void FinishRemoval()
    {
        ClearRemoveOptions();
        ResyncConductor();

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

        ClearRemoveOptions();

        // highlight straight away so the tap feels responsive, the conductor
        // confirms (or corrects) it once playback actually starts
        SetCurrentItem(radioItem);

        // songs play as a run up to the next podcast segment
        var songRun = radioItem.IsPodcastSegment
            ? null
            : Items
                .SkipWhile(item => item != radioItem)
                .TakeWhile(item => !item.IsPodcastSegment)
                .Select(item => item.PlayUri)
                .ToList();

        if (PlaybackStateStore.Instance.HasActiveDevice)
        {
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

        await LaunchAndRestoreContextAsync(radioItem, songRun);
    }

    // a spotify uri can only carry a single item, so launching the app loses the queue.
    // once spotify comes up as a device the rest of the run (or the segment offset) is
    // applied on top of what it already started playing
    private async Task LaunchAndRestoreContextAsync(RadioItem radioItem, List<string> songRun)
    {
        // spotify takes the foreground, keep our process alive to finish setting up
        RadioBackgroundService.Start();

        if (!await LaunchInSpotify(radioItem.PlayUri))
        {
            // nothing is playing after all, drop the optimistic highlight
            SetCurrentItem(null);
            RadioBackgroundService.Stop();
            await Shell.Current.DisplayAlert("Playback failed", "Couldn't start playback. Make sure Spotify is installed and you're signed in.", "OK");
            return;
        }

        if (!await WaitForActiveDeviceAsync())
        {
            RadioBackgroundService.Stop();
            return;
        }

        await Task.Run(() =>
        {
            var api = APICaller.Instance;
            if (api == null) return;

            api.SetPlaybackShuffle(false);

            if (radioItem.IsPodcastSegment)
            {
                // the launch starts the episode at 0, jump to this segment
                if (radioItem.PositionMs > 0) api.SeekTo(radioItem.PositionMs);
                return;
            }

            // the first track is already playing, queue the rest behind it so nothing restarts
            foreach (var uri in songRun.Skip(1)) api.AddToQueue(uri);
        });

        RadioConductor.Instance.Start(Items.ToList(), Items.IndexOf(radioItem));
    }

    private static async Task<bool> WaitForActiveDeviceAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var context = await Task.Run(() => APICaller.Instance?.GetCurrentPlaybackContext());
            if (!string.IsNullOrEmpty(context?.Device?.Id)) return true;

            await Task.Delay(1000);
        }
        return false;
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
