using SpotifyAPI.Web;
using SpotiSharpBackend.Radio;

namespace SpotiSharpBackend.Tests;

public class DeviceResolverTests
{
    private static Device MakeDevice(string id, string type, bool isActive = false) =>
        new Device { Id = id, Type = type, IsActive = isActive };

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
    public void No_phone_in_the_list_and_nothing_pinned_resolves_to_nothing()
    {
        // Unpinned must never fall back to "whatever else is in the list" — that's how playback
        // used to land on a media streamer or a computer instead of the phone. Resolve returning
        // null here is the signal for the caller to wake Spotify and retry, not a device to use.
        var speakerA = MakeDevice("speaker-a", "Speaker");
        var speakerB = MakeDevice("speaker-b", "Speaker");
        var devices = new[] { speakerA, speakerB };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null);

        Assert.Null(resolved);
    }

    [Fact]
    public void Multiple_phones_prefers_the_one_reporting_active()
    {
        // Regression: accounts with more than one real phone registered (family members, an old
        // handset still logged in) used to resolve to whichever phone Spotify happened to list
        // first — nondeterministic from this app's perspective. The phone this app just woke via
        // App Remote is the one that should report active, so that's the one that should win.
        var otherPhone = MakeDevice("other-phone", "Smartphone", isActive: false);
        var thisPhone = MakeDevice("this-phone", "Smartphone", isActive: true);
        var devices = new[] { otherPhone, thisPhone };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null);

        Assert.Equal("this-phone", resolved);
    }

    [Fact]
    public void Multiple_phones_none_active_falls_back_to_the_first_phone()
    {
        var phoneA = MakeDevice("phone-a", "Smartphone", isActive: false);
        var phoneB = MakeDevice("phone-b", "Smartphone", isActive: false);
        var devices = new[] { phoneA, phoneB };

        var resolved = DeviceResolver.Resolve(devices, selectedId: null);

        Assert.Equal("phone-a", resolved);
    }
}
