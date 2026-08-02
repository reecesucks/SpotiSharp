using SpotifyAPI.Web;
using SpotiSharpBackend.Radio;

namespace SpotiSharpBackend.Tests;

public class DeviceResolverTests
{
    private static Device MakeDevice(string id, string type) => new Device { Id = id, Type = type };

    [Fact]
    public void Empty_device_list_resolves_to_nothing()
    {
        var resolved = DeviceResolver.Resolve(Array.Empty<Device>(), selectedId: null);

        Assert.Null(resolved);
    }

    [Fact]
    public void Selected_device_wins_over_the_phone()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var speaker = MakeDevice("speaker-1", "Speaker");
        var devices = new[] { phone, speaker };

        var resolved = DeviceResolver.Resolve(devices, selectedId: "speaker-1");

        Assert.Equal("speaker-1", resolved);
    }

    [Fact]
    public void A_selected_device_no_longer_in_the_list_is_ignored()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var devices = new[] { phone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: "unplugged-device");

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void Nothing_selected_defaults_to_the_phone()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var speaker = MakeDevice("speaker-1", "Speaker");
        var devices = new[] { speaker, phone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null);

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void An_always_on_device_reported_as_active_does_not_steal_the_default()
    {
        // The regression this guards: Spotify's own "currently playing" state for a third-party
        // Connect receiver can lie (stale/zombie sessions that never got a proper pause/stop).
        // Resolve deliberately has no way to be told "something's playing elsewhere" at all, so a
        // misbehaving always-on device sitting in the list can never redirect the default away
        // from the phone — only an explicit Settings pin can.
        var phone = MakeDevice("phone-1", "Smartphone");
        var alwaysOnSpeaker = MakeDevice("wiim-1", "AVR");
        var devices = new[] { phone, alwaysOnSpeaker };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null);

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void No_phone_in_the_list_falls_back_to_the_first_device()
    {
        var speakerA = MakeDevice("speaker-a", "Speaker");
        var speakerB = MakeDevice("speaker-b", "Speaker");
        var devices = new[] { speakerA, speakerB };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null);

        Assert.Equal("speaker-a", resolved);
    }
}
