using SpotifyAPI.Web;

namespace SpotiSharpBackend.Radio;

public static class DeviceResolver
{
    public static string? Resolve(
        IReadOnlyList<Device> devices,
        string? selectedId,
        bool somethingIsPlayingElsewhere,
        string? activeElsewhereDeviceId)
    {
        if (devices.Count == 0) return null;

        if (!string.IsNullOrEmpty(selectedId) && devices.Any(device => device.Id == selectedId)) return selectedId;

        var phone = devices.FirstOrDefault(device => device.Type == "Smartphone");

        if (somethingIsPlayingElsewhere && !string.IsNullOrEmpty(activeElsewhereDeviceId)
            && activeElsewhereDeviceId != phone?.Id)
        {
            return activeElsewhereDeviceId;
        }

        return phone?.Id ?? devices[0].Id;
    }
}
