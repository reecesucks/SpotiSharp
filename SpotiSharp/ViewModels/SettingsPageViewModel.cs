using System.Collections.ObjectModel;
using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;

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
        ApplySettings = new Command(() =>
        {
            StorageHandler.IsUsingCollaborationHost = IsUsingCollaborationHost;
            StorageHandler.CollaborationHostAddress = CollaborationHostAddress;
            StorageHandler.CollaborationSession = CollaborationSession;
            if (IsUsingCollaborationHost) CollaborationAPI.Instance?.CreateSession();
            // TODO: else clear current state of playlist creation.
            });

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
        Devices.Add(new DeviceOption { Id = string.Empty, DisplayName = "Automatic (active device)" });
        foreach (var device in DeviceStore.Instance.Devices)
            Devices.Add(new DeviceOption { Id = device.Id, DisplayName = FormatCached(device, activeId) });

        if (!string.IsNullOrEmpty(pinnedId) && Devices.All(option => option.Id != pinnedId))
            Devices.Add(new DeviceOption { Id = pinnedId, DisplayName = "Selected device" });

        SelectedDevice = Devices.FirstOrDefault(option => option.Id == pinnedId) ?? Devices[0];

        _isLoadingDevices = false;
    }

    // Pulls the live device list and merges it into the cache (adds new, keeps existing), then
    // refreshes the picker. This is what the "Refresh devices" button runs.
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

    public ICommand ApplySettings { private set; get; }

    public ICommand UseCurrentDevice { private set; get; }

    public ICommand RefreshDevices { private set; get; }
}