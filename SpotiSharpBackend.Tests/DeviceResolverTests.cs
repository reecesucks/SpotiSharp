using SpotifyAPI.Web;
using SpotiSharpBackend.Radio;

namespace SpotiSharpBackend.Tests;

public class DeviceResolverTests
{
    private static Device MakeDevice(string id, string type) => new Device { Id = id, Type = type };

    [Fact]
    public void Empty_device_list_resolves_to_nothing()
    {
        var resolved = DeviceResolver.Resolve(Array.Empty<Device>(), selectedId: null, false, null);

        Assert.Null(resolved);
    }

    [Fact]
    public void Selected_device_wins_even_over_something_playing_elsewhere()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var speaker = MakeDevice("speaker-1", "Speaker");
        var devices = new[] { phone, speaker };

        var resolved = DeviceResolver.Resolve(devices, selectedId: "speaker-1", somethingIsPlayingElsewhere: true, activeElsewhereDeviceId: "phone-1");

        Assert.Equal("speaker-1", resolved);
    }

    [Fact]
    public void A_selected_device_no_longer_in_the_list_is_ignored()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var devices = new[] { phone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: "unplugged-device", false, null);

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void Nothing_selected_and_nothing_playing_elsewhere_defaults_to_the_phone()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var speaker = MakeDevice("speaker-1", "Speaker");
        var devices = new[] { speaker, phone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null, somethingIsPlayingElsewhere: false, activeElsewhereDeviceId: null);

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void Something_genuinely_playing_on_another_device_is_not_stolen()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var speaker = MakeDevice("speaker-1", "Speaker");
        var devices = new[] { phone, speaker };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null, somethingIsPlayingElsewhere: true, activeElsewhereDeviceId: "speaker-1");

        Assert.Equal("speaker-1", resolved);
    }

    [Fact]
    public void An_always_on_device_merely_marked_active_but_not_actually_playing_does_not_win()
    {
        // The regression this guards: some always-on Connect devices (smart speakers/receivers)
        // can sit flagged active in the device list even while idle. Resolve only takes a device
        // list plus an explicit "is something playing elsewhere" signal — it never looks at a
        // device's own IsActive flag — so a caller correctly passing somethingIsPlayingElsewhere:
        // false (because nothing is actually playing there) must land on the phone regardless of
        // what the always-on device's own listing looks like.
        var phone = MakeDevice("phone-1", "Smartphone");
        var alwaysOnSpeaker = MakeDevice("wiim-1", "AVR");
        var devices = new[] { phone, alwaysOnSpeaker };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null, somethingIsPlayingElsewhere: false, activeElsewhereDeviceId: null);

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void Playing_elsewhere_flag_without_a_device_id_is_ignored()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var devices = new[] { phone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null, somethingIsPlayingElsewhere: true, activeElsewhereDeviceId: null);

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void Playing_elsewhere_reported_as_the_phone_itself_still_resolves_to_the_phone()
    {
        var phone = MakeDevice("phone-1", "Smartphone");
        var devices = new[] { phone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null, somethingIsPlayingElsewhere: true, activeElsewhereDeviceId: "phone-1");

        Assert.Equal("phone-1", resolved);
    }

    [Fact]
    public void No_phone_in_the_list_falls_back_to_the_first_device()
    {
        var speakerA = MakeDevice("speaker-a", "Speaker");
        var speakerB = MakeDevice("speaker-b", "Speaker");
        var devices = new[] { speakerA, speakerB };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null, false, null);

        Assert.Equal("speaker-a", resolved);
    }
}
