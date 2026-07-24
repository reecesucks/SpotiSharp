using System.Collections.ObjectModel;
using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharpBackend;
using Device = SpotifyAPI.Web.Device;

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

        _ = LoadDevicesAsync();
    }

    private async Task LoadDevicesAsync()
    {
        var pinnedId = StorageHandler.SelectedDeviceId;

        PopulateDevices(
            DeviceStore.Instance.Devices.Select(device => new DeviceOption { Id = device.Id, DisplayName = FormatCached(device) }),
            pinnedId);

        var devices = await Task.Run(() => APICaller.Instance?.GetDevices());
        if (devices == null) return;

        DeviceStore.Instance.Update(devices);
        PopulateDevices(
            devices.Select(device => new DeviceOption { Id = device.Id, DisplayName = FormatDevice(device) }),
            pinnedId);
    }

    private void PopulateDevices(IEnumerable<DeviceOption> options, string pinnedId)
    {
        _isLoadingDevices = true;

        Devices.Clear();
        Devices.Add(new DeviceOption { Id = string.Empty, DisplayName = "Automatic (active device)" });
        foreach (var option in options) Devices.Add(option);

        if (!string.IsNullOrEmpty(pinnedId) && Devices.All(option => option.Id != pinnedId))
            Devices.Add(new DeviceOption { Id = pinnedId, DisplayName = "Selected device (offline)" });

        SelectedDevice = Devices.FirstOrDefault(option => option.Id == pinnedId) ?? Devices[0];

        _isLoadingDevices = false;
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
                DisplayName = cached != null ? FormatCached(cached) : "Current device"
            };
            Devices.Add(option);
        }

        // The setter persists it to StorageHandler.SelectedDeviceId.
        SelectedDevice = option;
    }

    private static string FormatDevice(Device device)
    {
        var active = device.IsActive ? " • active" : string.Empty;
        return $"{device.Name} ({device.Type}){active}";
    }

    private static string FormatCached(CachedDevice device) => $"{device.Name} ({device.Type})";

    public ICommand ApplySettings { private set; get; }

    public ICommand UseCurrentDevice { private set; get; }
}