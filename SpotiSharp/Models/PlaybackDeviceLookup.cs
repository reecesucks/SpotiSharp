using System.Linq;
using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public static class PlaybackDeviceLookup
{
    public static string? LastKnownPhoneDeviceId { get; private set; }

    public static async Task<(string? deviceId, bool apiFailed)> ResolveAsync()
    {
        var selectedId = StorageHandler.SelectedDeviceId;

        return await Task.Run(() =>
        {
            var api = APICaller.Instance;
            var devices = api?.GetDevices();
            if (devices == null) return ((string?)null, true);
            if (devices.Count == 0) return ((string?)null, false);

            var phoneId = devices.FirstOrDefault(device => device.Type == "Smartphone")?.Id;
            if (!string.IsNullOrEmpty(phoneId)) LastKnownPhoneDeviceId = phoneId;

            var resolved = DeviceResolver.Resolve(devices, selectedId);

            var resolvedDevice = devices.FirstOrDefault(device => device.Id == resolved);
            DiagnosticLog.Write($"[Device] resolved {resolved} ({resolvedDevice?.Name ?? "unknown"} / {resolvedDevice?.Type ?? "unknown"})");

            return (resolved, false);
        });
    }
}
