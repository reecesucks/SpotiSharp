using System.Linq;
using SpotiSharpBackend;
using SpotiSharpBackend.Radio;
using Device = SpotifyAPI.Web.Device;

namespace SpotiSharp.Models;

public static class PlaybackDeviceLookup
{
    public static string? LastKnownPhoneDeviceId { get; private set; }

    public static async Task<(string? deviceId, bool apiFailed)> ResolveAsync()
    {
        var selectedId = StorageHandler.SelectedDeviceId;

        var (devices, apiFailed) = await FetchDevicesAsync();
        if (apiFailed) return (null, true);

        if (NeedsWake(devices, selectedId) && PlaybackCommands.WakeSpotify != null)
        {
            DiagnosticLog.Write("[Device] no phone in the device list, waking Spotify before resolving");
            var woke = await PlaybackCommands.WakeSpotify();
            DiagnosticLog.Write(woke ? "[Device] wake succeeded" : "[Device] wake failed or timed out");

            var (retried, retryFailed) = await FetchDevicesAsync();
            if (!retryFailed && retried.Count > 0) devices = retried;
        }

        if (devices.Count == 0) return (null, false);

        var phones = devices.Where(device => device.Type == "Smartphone").ToList();
        var phoneId = (phones.FirstOrDefault(device => device.IsActive) ?? phones.FirstOrDefault())?.Id;
        if (!string.IsNullOrEmpty(phoneId)) LastKnownPhoneDeviceId = phoneId;

        var resolved = DeviceResolver.Resolve(devices, selectedId);

        var resolvedDevice = devices.FirstOrDefault(device => device.Id == resolved);
        DiagnosticLog.Write($"[Device] resolved {resolved} ({resolvedDevice?.Name ?? "unknown"} / {resolvedDevice?.Type ?? "unknown"})");

        return (resolved, false);
    }

    private static bool NeedsWake(IReadOnlyList<Device> devices, string? selectedId)
    {
        if (!string.IsNullOrEmpty(selectedId) && devices.Any(device => device.Id == selectedId)) return false;
        return !devices.Any(device => device.Type == "Smartphone");
    }

    private static Task<(IReadOnlyList<Device> devices, bool apiFailed)> FetchDevicesAsync() =>
        Task.Run(() =>
        {
            var devices = APICaller.Instance?.GetDevices();
            return devices == null
                ? ((IReadOnlyList<Device>)Array.Empty<Device>(), true)
                : ((IReadOnlyList<Device>)devices, false);
        });
}
