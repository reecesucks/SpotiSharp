using System.Text.Json;
using SpotiSharpBackend;
using Device = SpotifyAPI.Web.Device;

namespace SpotiSharp.Models;

public class DeviceStore
{
    private static DeviceStore _instance;
    public static DeviceStore Instance => _instance ??= new DeviceStore();

    private volatile IReadOnlyList<CachedDevice> _devices = Array.Empty<CachedDevice>();

    public IReadOnlyList<CachedDevice> Devices => _devices;

    private DeviceStore()
    {
        Load();
    }

    private void Load()
    {
        var json = StorageHandler.CachedDevices;
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var cached = JsonSerializer.Deserialize<List<CachedDevice>>(json);
            if (cached != null) _devices = cached;
        }
        catch (JsonException)
        {
            // Corrupt/legacy payload: fall back to an empty cache and let the next
        }
    }

    public void Update(IEnumerable<Device> devices)
    {
        var mapped = devices
            .Where(device => !string.IsNullOrEmpty(device.Id))
            .Select(device => new CachedDevice(device.Id, device.Name, device.Type))
            .ToList();

        _devices = mapped;
        StorageHandler.CachedDevices = JsonSerializer.Serialize(mapped);
    }
}
