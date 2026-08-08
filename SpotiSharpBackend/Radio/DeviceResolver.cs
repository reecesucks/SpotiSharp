using SpotifyAPI.Web;

namespace SpotiSharpBackend.Radio;

public static class DeviceResolver
{
    public static string? Resolve(IReadOnlyList<Device> devices, string? selectedId)
    {
        if (devices.Count == 0) return null;

        if (!string.IsNullOrEmpty(selectedId) && devices.Any(device => device.Id == selectedId)) return selectedId;

        var phones = devices.Where(device => device.Type == "Smartphone").ToList();
        return phones.FirstOrDefault(device => device.IsActive)?.Id ?? phones.FirstOrDefault()?.Id;
    }
}
