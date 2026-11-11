using System.Collections.ObjectModel;
using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.ViewModels;

public class SettingsPageViewModel : BaseViewModel
{
    private bool _isUsingCollaborationHost;

    public bool IsUsingCollaborationHost
    {
        get { return _isUsingCollaborationHost; }
        set { SetProperty(ref _isUsingCollaborationHost, value); }
    }
    
    private string _collaborationHostAddress;

    public string CollaborationHostAddress
    {
        get { return _collaborationHostAddress; }
        set { SetProperty(ref _collaborationHostAddress, value); }
    }
    
    private string _collaborationSession;

    public string CollaborationSession
    {
        get { return _collaborationSession; }
        set { SetProperty(ref _collaborationSession, value); }
    }

    private bool _showSegmentDebugTimer;

    public bool ShowSegmentDebugTimer
    {
        get { return _showSegmentDebugTimer; }
        set
        {
            if (!SetProperty(ref _showSegmentDebugTimer, value)) return;
            DebugSettings.ShowSegmentTimer = value;
        }
    }

    private bool _showSongSource;

    public bool ShowSongSource
    {
        get { return _showSongSource; }
        set
        {
            if (!SetProperty(ref _showSongSource, value)) return;
            DebugSettings.ShowSongSource = value;
        }
    }

    private string _audiobookshelfServerUrl;

    public string AudiobookshelfServerUrl
    {
        get { return _audiobookshelfServerUrl; }
        set { SetProperty(ref _audiobookshelfServerUrl, value); }
    }

    private string _audiobookshelfApiKey;

    public string AudiobookshelfApiKey
    {
        get { return _audiobookshelfApiKey; }
        set { SetProperty(ref _audiobookshelfApiKey, value); }
    }

    private string _audiobookshelfConnectionStatus = string.Empty;

    public string AudiobookshelfConnectionStatus
    {
        get { return _audiobookshelfConnectionStatus; }
        set { SetProperty(ref _audiobookshelfConnectionStatus, value); }
    }

    private bool _useAudiobookshelfPodcasts;

    // Either/or, not a separate toggle per source - the Radio page's podcasts come from Spotify
    // or AudioBookshelf, never both, so this takes effect immediately rather than needing Apply.
    public bool UseAudiobookshelfPodcasts
    {
        get { return _useAudiobookshelfPodcasts; }
        set
        {
            if (!SetProperty(ref _useAudiobookshelfPodcasts, value)) return;
            StorageHandler.UseAudiobookshelfPodcasts = value;
        }
    }

    public ObservableCollection<DeviceOption> Devices { get; } = new();

    private DeviceOption _selectedDevice;

    private bool _isLoadingDevices;

    public DeviceOption SelectedDevice
    {
        get { return _selectedDevice; }
        set
        {
            if (!SetProperty(ref _selectedDevice, value)) return;
            if (_isLoadingDevices) return;
            StorageHandler.SelectedDeviceId = value?.Id ?? string.Empty;
        }
    }

    public SettingsPageViewModel()
    {
        _showSegmentDebugTimer = DebugSettings.ShowSegmentTimer;
        _showSongSource = DebugSettings.ShowSongSource;
        _audiobookshelfServerUrl = StorageHandler.AudiobookshelfServerUrl;
        _audiobookshelfApiKey = StorageHandler.AudiobookshelfApiKey;
        _useAudiobookshelfPodcasts = StorageHandler.UseAudiobookshelfPodcasts;

        ApplySettings = new Command(() =>
        {
            StorageHandler.IsUsingCollaborationHost = IsUsingCollaborationHost;
            StorageHandler.CollaborationHostAddress = CollaborationHostAddress;
            StorageHandler.CollaborationSession = CollaborationSession;
            if (IsUsingCollaborationHost) CollaborationAPI.Instance?.CreateSession();
            // TODO: else clear current state of playlist creation.
            });

        ApplyAudiobookshelfSettings = new Command(() =>
        {
            SaveAudiobookshelfSettings();
            AudiobookshelfConnectionStatus = string.Empty;
        });

        TestAudiobookshelfConnection = new Command(async () => await TestAudiobookshelfConnectionAsync());

        UseCurrentDevice = new Command(PinCurrentDevice);
        RefreshDevices = new Command(async () => await RefreshDevicesAsync());

        ShowDevices();
        _ = RefreshDevicesAsync();
    }
    private void ShowDevices()
    {
        var pinnedId = StorageHandler.SelectedDeviceId;
        var activeId = PlaybackStateStore.Instance.ActiveDeviceId;

        _isLoadingDevices = true;

        Devices.Clear();
        Devices.Add(new DeviceOption { Id = string.Empty, DisplayName = "This phone" });
        foreach (var device in DeviceStore.Instance.Devices)
            Devices.Add(new DeviceOption { Id = device.Id, DisplayName = FormatCached(device, activeId) });

        if (!string.IsNullOrEmpty(pinnedId) && Devices.All(option => option.Id != pinnedId))
            Devices.Add(new DeviceOption { Id = pinnedId, DisplayName = "Selected device" });

        SelectedDevice = Devices.FirstOrDefault(option => option.Id == pinnedId) ?? Devices[0];

        _isLoadingDevices = false;
    }

    private async Task RefreshDevicesAsync()
    {
        var live = await Task.Run(() => APICaller.Instance?.GetDevices());
        if (live == null) return;

        DeviceStore.Instance.Merge(live);
        ShowDevices();
    }

    private async void PinCurrentDevice()
    {
        var activeId = PlaybackStateStore.Instance.ActiveDeviceId;
        if (string.IsNullOrEmpty(activeId))
        {
            await Shell.Current.DisplayAlert(
                "No active device",
                "Start playing something on the device you want to use, then try again.",
                "OK");
            return;
        }

        var option = Devices.FirstOrDefault(existing => existing.Id == activeId);
        if (option == null)
        {
            var cached = DeviceStore.Instance.Devices.FirstOrDefault(device => device.Id == activeId);
            option = new DeviceOption
            {
                Id = activeId,
                DisplayName = cached != null ? FormatCached(cached, activeId) : "Current device"
            };
            Devices.Add(option);
        }

        // The setter persists it to StorageHandler.SelectedDeviceId.
        SelectedDevice = option;
    }

    private static string FormatCached(CachedDevice device, string activeId)
    {
        var active = device.Id == activeId ? " • active" : string.Empty;
        return $"{device.Name} ({device.Type}){active}";
    }

    private void SaveAudiobookshelfSettings()
    {
        StorageHandler.AudiobookshelfServerUrl = AudiobookshelfServerUrl?.Trim() ?? string.Empty;
        StorageHandler.AudiobookshelfApiKey = AudiobookshelfApiKey?.Trim() ?? string.Empty;
    }

    private async Task TestAudiobookshelfConnectionAsync()
    {
        // Save first so Test checks whatever is currently typed, not the last-applied value.
        SaveAudiobookshelfSettings();
        AudiobookshelfConnectionStatus = "Testing...";
        var error = await AudiobookshelfApiClient.TestConnectionAsync();
        AudiobookshelfConnectionStatus = error == null ? "Connected" : $"Failed: {error}";
    }

    public ICommand ApplySettings { private set; get; }

    public ICommand ApplyAudiobookshelfSettings { private set; get; }

    public ICommand TestAudiobookshelfConnection { private set; get; }

    public ICommand UseCurrentDevice { private set; get; }

    public ICommand RefreshDevices { private set; get; }
}