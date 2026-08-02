using SpotifyAPI.Web;

namespace SpotiSharpBackend.Radio;

public static class DeviceResolver
{
    // Deliberately doesn't try to defer to "whatever's already playing elsewhere" — Spotify's own
    // IsPlaying/active-device signal has proven unreliable with some third-party Connect receivers
    // (they can report a stale "playing" session long after they've gone idle), so honoring it here
    // could silently redirect playback to a device that isn't actually doing anything. The phone is
    // always the target unless the user explicitly pins something else in Settings.
    public static string? Resolve(IReadOnlyList<Device> devices, string? selectedId)
    {
        if (devices.Count == 0) return null;

        if (!string.IsNullOrEmpty(selectedId) && devices.Any(device => device.Id == selectedId)) return selectedId;

        var phone = devices.FirstOrDefault(device => device.Type == "Smartphone");
        return phone?.Id ?? devices[0].Id;
    }
}
