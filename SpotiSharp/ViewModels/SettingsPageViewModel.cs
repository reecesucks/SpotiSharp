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

        _ = LoadDevicesAsync();
    }

    private async Task LoadDevicesAsync()
    {
        var devices = await Task.Run(() => APICaller.Instance?.GetDevices()) ?? new List<Device>();
        var pinnedId = StorageHandler.SelectedDeviceId;

        _isLoadingDevices = true;

        Devices.Clear();
        Devices.Add(new DeviceOption { Id = string.Empty, DisplayName = "Automatic (active device)" });
        foreach (var device in devices)
            Devices.Add(new DeviceOption { Id = device.Id, DisplayName = FormatDevice(device) });

        if (!string.IsNullOrEmpty(pinnedId) && Devices.All(option => option.Id != pinnedId))
            Devices.Add(new DeviceOption { Id = pinnedId, DisplayName = "Selected device (offline)" });

        SelectedDevice = Devices.FirstOrDefault(option => option.Id == pinnedId) ?? Devices[0];

        _isLoadingDevices = false;
    }

    private static string FormatDevice(Device device)
    {
        var active = device.IsActive ? " • active" : string.Empty;
        return $"{device.Name} ({device.Type}){active}";
    }

    public ICommand ApplySettings { private set; get; }
}