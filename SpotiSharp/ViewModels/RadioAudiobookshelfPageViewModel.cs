using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfPageViewModel : BaseViewModel
{
    public ICommand GenerateRadio { get; }
    public ICommand GenerateMoreRadio { get; }
    public ICommand OpenSettings { get; }
    public ICommand OpenDebugMenu { get; }

    public ICommand RemoveSingle { get; }
    public ICommand RemoveAllSections { get; }
    public ICommand NotInterested { get; }
    public ICommand HideMoreEpisode { get; }
    public ICommand SelectItem { get; }

    public ICommand ShowMix { get; }
    public ICommand ShowMusic { get; }
    public ICommand ShowPodcasts { get; }
    public ICommand HideModeStrip { get; }

    public RadioAudiobookshelfPageViewModel()
    {
        GenerateRadio = new Command(async () => await GenerateAsync());
        GenerateMoreRadio = new Command(async () => await GenerateMoreAsync());
        OpenSettings = new Command(async () => await Shell.Current.GoToAsync("RadioAudiobookshelfSettingsPage"));
        OpenDebugMenu = new Command(async () => await OpenDebugMenuAsync());
        RemoveSingle = new Command<RadioAudiobookshelfItem>(RemoveSingleItem);
        RemoveAllSections = new Command<RadioAudiobookshelfItem>(RemoveEpisode);
        NotInterested = new Command<RadioAudiobookshelfItem>(ExcludeEpisode);
        HideMoreEpisode = new Command<RadioAudiobookshelfItem>(HideMoreEpisodeRow);
        SelectItem = new Command<RadioAudiobookshelfItem>(item =>
        {
            if (item == null) return;
            if (item.IsConfirmingRemove) ClearRemoveOptions();
            else ClickItem(item);
        });

        ShowMix = new Command(() => ApplyMode(RadioAudiobookshelfListenMode.Mix));
        ShowMusic = new Command(() => ApplyMode(RadioAudiobookshelfListenMode.Music));
        ShowPodcasts = new Command(() => ApplyMode(RadioAudiobookshelfListenMode.Podcasts));
        HideModeStrip = new Command(() => IsModeStripVisible = false);

        _mode = RadioAudiobookshelfConfigModel.GetListenMode();
        // Never arrive on a filtered list with nothing on screen saying so.
        _isModeStripVisible = _mode != RadioAudiobookshelfListenMode.Mix;

        _ = LoadCachedRadioAsync();
    }

    private RadioAudiobookshelfListenMode _mode;

    public RadioAudiobookshelfListenMode Mode
    {
        get { return _mode; }
        private set
        {
            if (!SetProperty(ref _mode, value)) return;
            OnPropertyChanged(nameof(IsMixMode));
            OnPropertyChanged(nameof(IsMusicMode));
            OnPropertyChanged(nameof(IsPodcastsMode));
        }
    }

    public bool IsMixMode => Mode == RadioAudiobookshelfListenMode.Mix;
    public bool IsMusicMode => Mode == RadioAudiobookshelfListenMode.Music;
    public bool IsPodcastsMode => Mode == RadioAudiobookshelfListenMode.Podcasts;

    private bool _isModeStripVisible;

    public bool IsModeStripVisible
    {
        get { return _isModeStripVisible; }
        set { SetProperty(ref _isModeStripVisible, value); }
    }

    public void ToggleModeStrip() => IsModeStripVisible = !IsModeStripVisible;

    private void ApplyMode(RadioAudiobookshelfListenMode mode)
    {
        if (Mode == mode) return;

        Mode = mode;
        RadioAudiobookshelfConfigModel.SetListenMode(mode);
        Project();

        // Back in the mix, whichever kind was hidden has bunched up wherever the listener got
        // to - spread it out again so consecutive podcast segments don't play back to back.
        if (mode == RadioAudiobookshelfListenMode.Mix)
        {
            Reinterleave();
            Project();
        }

        DiagnosticLog.Write($"[Radio] listen mode {mode}: showing {Items.Count} of {_allItems.Count}");
        ResyncConductor($"listen mode {mode}");
        SaveRadio();
    }

    private RadioAudiobookshelfItem _selectedItem;

    public RadioAudiobookshelfItem SelectedItem
    {
        get { return _selectedItem; }
        set { SetProperty(ref _selectedItem, value); }
    }

    private RadioAudiobookshelfItem _currentItem;

    private bool _showSongSources;

    public bool ShowSongSources
    {
        get { return _showSongSources; }
        private set { SetProperty(ref _showSongSources, value); }
    }

    internal override void OnAppearing()
    {
        base.OnAppearing();
        // re-read on every visit, since the toggle lives on the settings page
        ShowSongSources = DebugSettings.ShowSongSource;
        RadioAudiobookshelfConductor.Instance.ActiveItemChanged += SetCurrentItem;
        UiLoop.Instance.OnRefreshUi += UpdateDebugSegmentTimer;
        SyncWithConductor();
        UpdateDebugSegmentTimer();
    }

    internal override void OnDisappearing()
    {
        base.OnDisappearing();
        RadioAudiobookshelfConductor.Instance.ActiveItemChanged -= SetCurrentItem;
        UiLoop.Instance.OnRefreshUi -= UpdateDebugSegmentTimer;
    }

    private void UpdateDebugSegmentTimer()
    {
        var item = _currentItem;
        if (item == null) return;

        string text = null;
        if (DebugSettings.ShowSegmentTimer && item.IsPodcastSegment)
        {
            var remainingMs = RadioAudiobookshelfConductor.Instance.RemainingSegmentMs();
            if (remainingMs.HasValue) text = FormatSegmentRemaining(remainingMs.Value);
        }

        MainThread.BeginInvokeOnMainThread(() => item.DebugSegmentRemaining = text);
    }

    private static string FormatSegmentRemaining(int remainingMs)
    {
        var span = TimeSpan.FromMilliseconds(Math.Abs(remainingMs));
        var formatted = span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
        return remainingMs >= 0 ? $"{formatted} left" : $"{formatted} over";
    }

    private void SyncWithConductor()
    {
        var remaining = RadioAudiobookshelfConductor.Instance.RemainingItems;
        if (remaining == null || remaining.Count == 0) return;

        // The conductor knows how far the radio got while the page was away. Its items are this
        // view's rows, so take its active item and reproject around it rather than adopting its
        // list - which in a filtered mode would be missing everything this mode hides.
        if (_currentItem != null && !ReferenceEquals(_currentItem, remaining[0])) _currentItem.IsCurrent = false;
        _currentItem = remaining[0];

        Project();
        TrimPlayed(_currentItem);
    }

    private void SetCurrentItem(RadioAudiobookshelfItem item)
    {
        if (ReferenceEquals(_currentItem, item)) return;

        if (_currentItem != null) _currentItem.IsCurrent = false;
        _currentItem = item;
        if (_currentItem == null) return;

        _currentItem.IsCurrent = true;
        TrimPlayed(_currentItem);
    }

    /// <summary>
    /// Lays <see cref="RadioAudiobookshelfModel.SongsPerSection"/> songs in front of every podcast segment,
    /// which is what makes the mix a mix. Nothing is added or dropped, only re-ordered.
    /// </summary>
    private static List<RadioAudiobookshelfItem> Interleave(List<RadioAudiobookshelfItem> items)
    {
        var segments = items.Where(radioItem => radioItem.IsPodcastSegment).ToList();
        if (segments.Count == 0) return items;

        var songs = items.Where(radioItem => !radioItem.IsPodcastSegment).ToList();

        var rebuilt = new List<RadioAudiobookshelfItem>();
        int songIndex = 0;
        foreach (var segment in segments)
        {
            for (int i = 0; i < RadioAudiobookshelfModel.SongsPerSection && songIndex < songs.Count; i++)
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
        return rebuilt;
    }

    // Re-lays the radio behind whatever is playing. What's playing, and anything still sitting
    // in front of it, is left where it is.
    private void Reinterleave()
    {
        int keep = _currentItem == null ? 0 : _allItems.IndexOf(_currentItem) + 1;
        _allItems = _allItems.Take(keep).Concat(Interleave(_allItems.Skip(keep).ToList())).ToList();
    }

    private void RebalancePodcasts()
    {
        Reinterleave();
        Project();
    }

    private void ResyncConductor(string why)
    {
        if (_currentItem == null || !RadioAudiobookshelfConductor.Instance.IsActive) return;

        var rows = RadioRows;
        int index = rows.IndexOf(_currentItem);
        if (index >= 0) RadioAudiobookshelfConductor.Instance.Resync(rows, index, why);
    }

    private void TrimPlayed(RadioAudiobookshelfItem current)
    {
        int index = Items.IndexOf(current);
        if (index <= 0) return;

        // Rows the listener has gone past in this view are done with. Entries of the kind this
        // mode hides are not - they were never heard, so they move ahead of the frontier rather
        // than being dropped, which also stops them stranding above the current row in the mix.
        var played = new HashSet<RadioAudiobookshelfItem>();
        for (int i = 0; i < index; i++)
        {
            foreach (var source in SourcesOf(Items[i])) played.Add(source);
        }

        int frontier = FirstSourceIndex(current);
        if (frontier < 0) frontier = _allItems.Count;

        var skipped = _allItems.Take(frontier).Where(item => !played.Contains(item)).ToList();
        _allItems = _allItems.Skip(frontier).ToList();
        _allItems.InsertRange(Math.Min(1, _allItems.Count), skipped);

        for (int i = 0; i < index; i++) Items.RemoveAt(0);

        SaveRadio();
    }

    // Everything that was generated, always fully segmented and in mix order. Items is a view
    // over this, so switching mode hides things rather than losing them.
    private List<RadioAudiobookshelfItem> _allItems = new List<RadioAudiobookshelfItem>();

    // Each row in Items mapped back to the _allItems entries it stands for: itself for a song or
    // a single segment, every segment of the episode for a collapsed podcasts-only row.
    private Dictionary<RadioAudiobookshelfItem, List<RadioAudiobookshelfItem>> _backing = new Dictionary<RadioAudiobookshelfItem, List<RadioAudiobookshelfItem>>();

    private List<RadioAudiobookshelfItem> SourcesOf(RadioAudiobookshelfItem row)
    {
        return _backing.TryGetValue(row, out var sources) ? sources : new List<RadioAudiobookshelfItem> { row };
    }

    private int FirstSourceIndex(RadioAudiobookshelfItem row)
    {
        int best = -1;
        foreach (var source in SourcesOf(row))
        {
            int at = _allItems.IndexOf(source);
            if (at >= 0 && (best < 0 || at < best)) best = at;
        }
        return best;
    }

    private void DropFromAll(RadioAudiobookshelfItem row)
    {
        foreach (var source in SourcesOf(row)) _allItems.Remove(source);
        _backing.Remove(row);
    }

    /// <summary>Rebuilds <see cref="Items"/> from the full radio according to <see cref="Mode"/>.</summary>
    private void Project()
    {
        var projected = new List<RadioAudiobookshelfItem>();
        var backing = new Dictionary<RadioAudiobookshelfItem, List<RadioAudiobookshelfItem>>();

        switch (Mode)
        {
            case RadioAudiobookshelfListenMode.Music:
                foreach (var item in _allItems.Where(item => !item.IsPodcastSegment))
                {
                    projected.Add(item);
                    backing[item] = new List<RadioAudiobookshelfItem> { item };
                }
                break;

            case RadioAudiobookshelfListenMode.Podcasts:
                // One row per episode, standing for all of its segments: with no songs between
                // them the segment boundaries would only make the conductor re-issue the same
                // episode at a new offset every few minutes.
                foreach (var episode in _allItems.Where(item => item.IsPodcastSegment).GroupBy(item => item.PlayUri))
                {
                    var segments = episode.ToList();
                    var earliest = segments.OrderBy(segment => segment.PositionMs).First();

                    var collapsed = RadioAudiobookshelfItem.ForWholeEpisode(earliest);
                    projected.Add(collapsed);
                    backing[collapsed] = segments;
                }
                break;

            default:
                foreach (var item in _allItems)
                {
                    projected.Add(item);
                    backing[item] = new List<RadioAudiobookshelfItem> { item };
                }
                break;
        }

        _backing = backing;

        // A reprojection mints new objects for collapsed rows, so the "currently playing" row has
        // to be re-pinned rather than compared by identity alone.
        var row = ResolveCurrentRow(projected);
        if (row != null)
        {
            if (!ReferenceEquals(row, _currentItem)) _currentItem.IsCurrent = false;
            _currentItem = row;
            _currentItem.IsCurrent = true;
        }
        else if (_currentItem != null)
        {
            // This mode hides what's playing. It stays at the head anyway: the mode decides what
            // comes next, it doesn't interrupt.
            projected.Insert(0, _currentItem);
            _backing[_currentItem] = new List<RadioAudiobookshelfItem> { _currentItem };
            _currentItem.IsCurrent = true;
        }

        if (Mode == RadioAudiobookshelfListenMode.Podcasts) projected.AddRange(MoreEpisodeRows());

        Items = new ObservableCollection<RadioAudiobookshelfItem>(projected);
        RefreshUpNextLabels();
    }

    // What the last generation left out, shown under the radio in the podcasts view.
    private List<RecentEpisode> _moreEpisodes = new List<RecentEpisode>();

    private List<RadioAudiobookshelfItem> MoreEpisodeRows()
    {
        var inRadio = _allItems.Where(item => item.IsPodcastSegment).Select(item => item.PlayUri).ToHashSet();
        var excluded = RadioAudiobookshelfConfigModel.Config.ExcludedEpisodeIds;

        var rows = _moreEpisodes
            .Where(episode => !inRadio.Contains($"spotify:episode:{episode.EpisodeId}") && !excluded.Contains(episode.EpisodeId))
            .Select(RadioAudiobookshelfItem.ForMoreEpisode)
            .ToList();

        if (rows.Count > 0) rows.Insert(0, RadioAudiobookshelfItem.ForMoreHeader());
        return rows;
    }

    private void RefreshUpNextLabels()
    {
        var upNext = RadioAudiobookshelfConfigModel.GetUpNext();
        foreach (var row in Items.Where(item => item.MoreEpisode != null))
        {
            int at = -1;
            for (int i = 0; i < upNext.Count; i++)
            {
                if (upNext[i].EpisodeId == row.MoreEpisode.EpisodeId) at = i;
            }

            row.UpNextLabel = at < 0 ? null : upNext.Count == 1 ? "Up next" : $"Up next {at + 1}";
        }
    }

    private void ToggleUpNext(RadioAudiobookshelfItem row)
    {
        if (row?.MoreEpisode == null) return;

        RadioAudiobookshelfConfigModel.ToggleUpNext(row.MoreEpisode);
        DiagnosticLog.Write($"[Radio] up next toggled for {row.PlayUri} \"{row.Title}\": {RadioAudiobookshelfConfigModel.GetUpNext().Count} picked");
        RefreshUpNextLabels();
    }

    // The rows that are the radio itself, without the "More episodes" section under it. This is
    // all the conductor may ever be handed.
    private List<RadioAudiobookshelfItem> RadioRows => Items.Where(item => !item.IsMoreRow).ToList();

    private RadioAudiobookshelfItem ResolveCurrentRow(List<RadioAudiobookshelfItem> projected)
    {
        if (_currentItem == null) return null;

        foreach (var row in projected)
        {
            if (_backing.TryGetValue(row, out var sources) && sources.Any(source => ReferenceEquals(source, _currentItem))) return row;
        }

        // Coming back to the page the radio was rebuilt from the cache, so the conductor's item
        // is a different object: match the segment itself before settling for its episode, or
        // part three of an episode would be mistaken for part one.
        return projected.FirstOrDefault(row => row.PlayUri == _currentItem.PlayUri && row.PositionMs == _currentItem.PositionMs)
            ?? projected.FirstOrDefault(row => row.PlayUri == _currentItem.PlayUri);
    }

    private void SaveRadio()
    {
        var snapshot = _allItems.ToList();
        Task.Run(() => RadioAudiobookshelfModel.SaveRadio(snapshot));
    }

    public void ShowRemoveOptions(RadioAudiobookshelfItem item)
    {
        // the header has nothing to act on; an episode under it can only be hidden
        if (item?.IsMoreHeader == true) item = null;
        foreach (var current in Items) current.IsConfirmingRemove = ReferenceEquals(current, item);
    }

    public void ClearRemoveOptions()
    {
        foreach (var current in Items) current.IsConfirmingRemove = false;
    }

    public void RemoveSingleItem(RadioAudiobookshelfItem item)
    {
        if (item == null || !Items.Contains(item)) return;

        DropFromAll(item);
        Items.Remove(item);
        FinishRemoval($"removing {item.PlayUri} \"{item.Title}\"");
    }

    public void RemoveEpisode(RadioAudiobookshelfItem item)
    {
        if (item == null || !item.IsPodcastSegment) return;

        var rows = Items.Where(current => current.IsPodcastSegment && current.PlayUri == item.PlayUri).ToList();
        if (rows.Count == 0) return;

        foreach (var row in rows)
        {
            DropFromAll(row);
            Items.Remove(row);
        }

        // A collapsed row already stood for every segment; in the mix there may be segments of
        // this episode that no visible row covered.
        int dropped = _allItems.RemoveAll(entry => entry.IsPodcastSegment && entry.PlayUri == item.PlayUri);

        RebalancePodcasts();
        FinishRemoval($"removing every segment of {item.PlayUri} \"{item.Title}\" ({rows.Count} rows, {dropped} stragglers) and rebalancing");
    }

    public void ExcludeEpisode(RadioAudiobookshelfItem item)
    {
        if (item == null || !item.IsPodcastSegment) return;

        var episodeId = EpisodeIdFromUri(item.PlayUri);
        if (!string.IsNullOrEmpty(episodeId)) RadioAudiobookshelfConfigModel.ExcludeEpisode(episodeId);

        RemoveEpisode(item);
    }

    /// <summary>
    /// Keeps a "More episodes" episode out of every future radio - the same list "Not interested"
    /// adds to - and drops it from the section, and from up next if it was picked.
    /// </summary>
    public void HideMoreEpisodeRow(RadioAudiobookshelfItem row)
    {
        if (row?.MoreEpisode == null) return;

        var episode = row.MoreEpisode;
        RadioAudiobookshelfConfigModel.ExcludeEpisode(episode.EpisodeId);
        if (RadioAudiobookshelfConfigModel.GetUpNext().Any(picked => picked.EpisodeId == episode.EpisodeId)) RadioAudiobookshelfConfigModel.ToggleUpNext(episode);

        Items.Remove(row);
        if (!Items.Any(item => item.IsMoreEpisode))
        {
            foreach (var header in Items.Where(item => item.IsMoreHeader).ToList()) Items.Remove(header);
        }

        DiagnosticLog.Write($"[Radio] hid {row.PlayUri} \"{row.Title}\" from future radios");
        ClearRemoveOptions();
        RefreshUpNextLabels();
    }

    private static string EpisodeIdFromUri(string playUri)
    {
        const string prefix = "spotify:episode:";
        return !string.IsNullOrEmpty(playUri) && playUri.StartsWith(prefix) ? playUri.Substring(prefix.Length) : null;
    }

    private void FinishRemoval(string why)
    {
        DiagnosticLog.Write($"[Radio] {why}");
        ClearRemoveOptions();
        ResyncConductor(why);
        SaveRadio();
    }

    private async Task LoadCachedRadioAsync()
    {
        IsLoadingCache = true;
        var (cached, more) = await Task.Run(() => (RadioAudiobookshelfModel.CachedRadio, RadioAudiobookshelfModel.CachedMoreEpisodes));
        IsLoadingCache = false;

        if (IsGenerating || Items.Count > 0) return;
        if (cached == null || cached.Count == 0) return;

        _moreEpisodes = more ?? new List<RecentEpisode>();
        _allItems = cached;
        Project();
    }

    private bool _isGenerating;

    public bool IsGenerating
    {
        get { return _isGenerating; }
        private set
        {
            if (SetProperty(ref _isGenerating, value)) OnPropertyChanged(nameof(IsBusy));
        }
    }

    private bool _isLoadingCache;

    public bool IsLoadingCache
    {
        get { return _isLoadingCache; }
        private set
        {
            if (SetProperty(ref _isLoadingCache, value)) OnPropertyChanged(nameof(IsBusy));
        }
    }

    public bool IsBusy => IsGenerating || IsLoadingCache;

    private ObservableCollection<RadioAudiobookshelfItem> _items = new ObservableCollection<RadioAudiobookshelfItem>();

    public ObservableCollection<RadioAudiobookshelfItem> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    private async Task GenerateAsync()
    {
        if (IsGenerating) return;
        IsGenerating = true;
        DiagnosticLog.Write("[Radio] Generate pressed");
        RadioAudiobookshelfConductor.Instance.Stop("Generate pressed");
        AppRemotePlayback.LogQueueMirror("Generate pressed");

        var (items, more) = await Task.Run(() => (RadioAudiobookshelfModel.Generate(), RadioAudiobookshelfModel.CachedMoreEpisodes));
        DiagnosticLog.Write($"[Radio] generated {items?.Count.ToString() ?? "nothing (failed)"} items");
        if (items != null)
        {
            _moreEpisodes = more ?? new List<RecentEpisode>();
            _allItems = items;
            Project();
        }

        IsGenerating = false;
    }

    private async Task GenerateMoreAsync()
    {
        if (IsGenerating) return;
        IsGenerating = true;

        var (items, more) = await Task.Run(() => (RadioAudiobookshelfModel.Generate(), RadioAudiobookshelfModel.CachedMoreEpisodes));
        DiagnosticLog.Write($"[Radio] Generate more added {items?.Count.ToString() ?? "nothing (failed)"} items");
        if (items != null)
        {
            _moreEpisodes = more ?? new List<RecentEpisode>();
            _allItems.AddRange(items);
            Project();

            ResyncConductor("Generate more");
            SaveRadio();
        }

        IsGenerating = false;
    }

    private const string DebugSaveLog = "Save log";
    private const string DebugCheckQueue = "Check queue";

    private bool _debugMenuOpen;

    private async Task OpenDebugMenuAsync()
    {
        if (_debugMenuOpen) return;
        _debugMenuOpen = true;
        try
        {
            var choice = await Shell.Current.DisplayActionSheet("Debug", "Cancel", null, DebugSaveLog, DebugCheckQueue);
            if (choice == DebugSaveLog) await SaveBugReportAsync();
            else if (choice == DebugCheckQueue) await CheckQueueAsync();
        }
        finally
        {
            _debugMenuOpen = false;
        }
    }

    private static async Task SaveBugReportAsync()
    {
        // capture straight away, before the note prompt, so the state matches the moment of the tap
        var tappedAt = DateTime.Now;
        DiagnosticLog.Write("[Mark] Save log tapped");
        var stateAtTap = await Task.Run(RadioAudiobookshelfDiagnostics.DescribeLocalState);

        var note = await Shell.Current.DisplayPromptAsync("Save log", "What just happened? (optional)", "Save", "Cancel");
        if (note == null)
        {
            DiagnosticLog.Write("[Mark] Save log cancelled");
            return;
        }

        var savedTo = await RadioAudiobookshelfDiagnostics.SaveBugReportAsync(tappedAt, stateAtTap, note);
        await Shell.Current.DisplayAlert(
            savedTo != null ? "Log saved" : "Save failed",
            savedTo ?? "Couldn't write the file. The reason is in the diagnostics log.",
            "OK");
    }

    private static async Task CheckQueueAsync()
    {
        var report = await Task.Run(RadioAudiobookshelfDiagnostics.DescribeQueues);
        DiagnosticLog.Write($"[Queue] queue check:{Environment.NewLine}{report}");
        await Shell.Current.DisplayAlert("Queue check", report, "OK");
    }

    private RadioAudiobookshelfItem _lastClickedItem;
    private DateTime _lastClickedAt;

    public async void ClickItem(object sourceItem)
    {
        if (sourceItem is not RadioAudiobookshelfItem radioItem) return;

        if (radioItem.IsMoreRow)
        {
            ClearRemoveOptions();
            ToggleUpNext(radioItem);
            return;
        }

        var now = DateTime.UtcNow;
        if (ReferenceEquals(_lastClickedItem, radioItem) && now - _lastClickedAt < TimeSpan.FromMilliseconds(400)) return;
        _lastClickedItem = radioItem;
        _lastClickedAt = now;

        ClearRemoveOptions();

        SetCurrentItem(radioItem);

        if (radioItem.IsPodcastSegment && radioItem.SourceKind == PodcastSourceKind.Audiobookshelf)
        {
            DiagnosticLog.Write($"[Radio] tapped {radioItem.PlayUri} \"{radioItem.Title}\" (AudioBookshelf)");
            PlayerBarViewModel.Instance.NotifyPlaybackStarting(radioItem.Title, radioItem.ImageUrl, radioItem.PlayUri);

            var attempt = PodcastPlaybackAdapters.For(radioItem.SourceKind).Play(radioItem);
            if (attempt == PlaybackAttempt.Success)
            {
                DiagnosticLog.Write("[Radio] played via AudioBookshelf");
                StartConductorAt(radioItem);
            }
            else
            {
                DiagnosticLog.Write($"[Radio] AudioBookshelf playback attempt failed: {attempt}");
                SetCurrentItem(null);
                await Shell.Current.DisplayAlert("Playback failed", "Couldn't start that episode on AudioBookshelf.", "OK");
            }
            return;
        }

        var songRun = radioItem.IsPodcastSegment
            ? null
            : RadioRows
                .SkipWhile(item => item != radioItem)
                .TakeWhile(item => !item.IsPodcastSegment)
                .Take(RadioAudiobookshelfConductor.MAX_SONG_RUN)
                .Select(item => item.PlayUri)
                .ToList();


        DiagnosticLog.Write($"[Radio] tapped {radioItem.PlayUri} \"{radioItem.Title}\" (run of {songRun?.Count.ToString() ?? "podcast"})");

        PlayerBarViewModel.Instance.NotifyPlaybackStarting(radioItem.Title, radioItem.ImageUrl, radioItem.PlayUri);

        if (await TryPlayViaAppRemoteAsync(radioItem, songRun))
        {
            DiagnosticLog.Write("[Radio] played via App Remote (local, no Connect device needed)");
            StartConductorAt(radioItem);
            return;
        }

        var (played, apiFailed) = await TryPlayOnActiveDeviceAsync(radioItem, songRun);
        if (played)
        {
            DiagnosticLog.Write("[Radio] direct play on active device succeeded");
            StartConductorAt(radioItem);
            return;
        }

        if (apiFailed)
        {
            DiagnosticLog.Write("[Radio] device lookup failed (Spotify API unreachable), not launching Spotify");
            SetCurrentItem(null);
            await Shell.Current.DisplayAlert("Playback failed", "Couldn't reach Spotify right now. Check your connection and try again.", "OK");
            return;
        }

        DiagnosticLog.Write("[Radio] no active device took the command, launching Spotify");
        await LaunchAndRestoreContextAsync(radioItem, songRun);
    }

    private void StartConductorAt(RadioAudiobookshelfItem radioItem)
    {
        var rows = RadioRows;
        RadioAudiobookshelfConductor.Instance.Start(rows, rows.IndexOf(radioItem));
    }

    private static Task<bool> TryPlayViaAppRemoteAsync(RadioAudiobookshelfItem radioItem, List<string> songRun) =>
        Models.AppRemotePlayback.TryPlayItemAsync(radioItem, songRun);

    private static async Task<(bool played, bool apiFailed)> TryPlayOnActiveDeviceAsync(RadioAudiobookshelfItem radioItem, List<string> songRun)
    {
        var (deviceId, apiFailed) = await ResolvePlayableDeviceAsync();
        if (apiFailed) return (false, true);
        if (string.IsNullOrEmpty(deviceId)) return (false, false);

        var played = await Task.Run(() =>
        {
            var api = APICaller.Instance;
            if (api == null) return false;

            if (PlaybackStateStore.Instance.ShuffleOn) api.SetPlaybackShuffle(false);

            return radioItem.IsPodcastSegment
                ? api.PlayUrisOnDevice(new List<string> { radioItem.PlayUri }, deviceId, radioItem.PositionMs)
                : api.PlayUrisOnDevice(songRun, deviceId);
        });

        if (played && radioItem.IsPodcastSegment) Models.PlaybackCommands.SeekTo?.Invoke(radioItem.PositionMs);

        return (played, false);
    }

    private static Task<(string? deviceId, bool apiFailed)> ResolvePlayableDeviceAsync() =>
        Models.PlaybackDeviceLookup.ResolveAsync();

    private async Task LaunchAndRestoreContextAsync(RadioAudiobookshelfItem radioItem, List<string> songRun)
    {
        RadioAudiobookshelfBackgroundService.Start();

        bool woke = Models.PlaybackCommands.WakeSpotify != null
            ? await Models.PlaybackCommands.WakeSpotify()
            : await LaunchInSpotify(radioItem.PlayUri);

        if (!woke)
        {
            DiagnosticLog.Write("[Radio] could not wake Spotify, aborting");
            SetCurrentItem(null);
            RadioAudiobookshelfBackgroundService.Stop();
            await Shell.Current.DisplayAlert("Playback failed", "Couldn't start playback. Make sure Spotify is installed and you're signed in.", "OK");
            return;
        }

        var deviceId = await WaitForAvailableDeviceAsync();
        if (deviceId == null)
        {
            DiagnosticLog.Write("[Radio] no device appeared after launch, aborting");
            SetCurrentItem(null);
            RadioAudiobookshelfBackgroundService.Stop();
            await Shell.Current.DisplayAlert("Playback failed", "Couldn't start playback. Make sure Spotify is installed and you're signed in.", "OK");
            return;
        }

        DiagnosticLog.Write($"[Radio] device {deviceId} appeared, handing over the run");
        await Task.Run(() => APICaller.Instance?.SetPlaybackShuffle(false));

        bool handedOver = await StartRunOnDeviceAsync(radioItem, songRun, deviceId);
        DiagnosticLog.Write(handedOver
            ? "[Radio] handover succeeded"
            : "[Radio] launch handover failed; conductor will re-issue at the item boundary");

        StartConductorAt(radioItem);
    }

    private static async Task<bool> StartRunOnDeviceAsync(RadioAudiobookshelfItem radioItem, List<string> songRun, string deviceId)
    {
        // A freshly woken device is listed a moment before it accepts commands, and the wake-up
        // burst can trip a rate-limit cooldown: keep asking for a while instead of giving up on
        // the first refusal — this handover is the whole point of the launch.
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (true)
        {
            deadline += await Ratelimiter.WaitOutCooldownAsync(TimeSpan.FromSeconds(30));

            bool started = await Task.Run(() =>
            {
                var api = APICaller.Instance;
                if (api == null) return false;

                return radioItem.IsPodcastSegment
                    ? api.PlayUrisOnDevice(new List<string> { radioItem.PlayUri }, deviceId, radioItem.PositionMs)
                    : api.PlayUrisOnDevice(songRun, deviceId);
            });

            if (started) return true;
            DiagnosticLog.Write($"[Radio] handover to {deviceId} refused, {(deadline - DateTime.UtcNow).TotalSeconds:0.#}s left in window");
            if (DateTime.UtcNow >= deadline) return false;

            await Task.Delay(1000);
        }
    }

    private static async Task<string?> WaitForAvailableDeviceAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        var phoneGrace = DateTime.UtcNow.AddSeconds(6);
        string? fallback = null;

        var pinnedId = StorageHandler.SelectedDeviceId;

        while (DateTime.UtcNow < deadline)
        {
            // A rate-limit cooldown pauses the clock rather than counting against the deadline.
            // Unlike the shared loop thread, this user-initiated task can afford to wait it out.
            var waited = await Ratelimiter.WaitOutCooldownAsync(TimeSpan.FromSeconds(30));
            deadline += waited;
            phoneGrace += waited;

            var devices = await Task.Run(() => APICaller.Instance?.GetDevices());
            if (devices != null && devices.Count > 0)
            {
                if (!string.IsNullOrEmpty(pinnedId) && devices.Any(d => d.Id == pinnedId)) return pinnedId;

                var phones = devices.Where(d => d.Type == "Smartphone").ToList();
                var activePhone = phones.FirstOrDefault(d => d.IsActive)?.Id;
                if (!string.IsNullOrEmpty(activePhone)) return activePhone;

                var anyPhone = phones.FirstOrDefault()?.Id;
                if (!string.IsNullOrEmpty(anyPhone))
                {
                    fallback = anyPhone;
                    if (DateTime.UtcNow > phoneGrace) return fallback;
                }
            }

            await Task.Delay(1000);
        }
        return fallback;
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
