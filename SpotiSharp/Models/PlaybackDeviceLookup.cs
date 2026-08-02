using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public static class PlaybackDeviceLookup
{
    public static async Task<(string? deviceId, bool apiFailed)> ResolveAsync()
    {
        var selectedId = StorageHandler.SelectedDeviceId;

        return await Task.Run(() =>
        {
            var api = APICaller.Instance;
            var devices = api?.GetDevices();
            if (devices == null) return ((string?)null, true);
            if (devices.Count == 0) return ((string?)null, false);

            api!.TryGetCurrentPlaybackContext(out var context);
            var resolved = DeviceResolver.Resolve(devices, selectedId, context?.IsPlaying == true, context?.Device?.Id);
            return (resolved, false);
        });
    }
}
