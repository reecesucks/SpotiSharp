namespace QinF25.Input;

/// <summary>
/// A device-independent key on a physical keypad. Platform-specific key codes
/// (e.g. Android <c>Keycode</c>) are mapped onto these values so that consuming
/// pages never have to reference a platform namespace.
/// </summary>
public enum KeypadKey
{
    /// <summary>A key that has no mapping in <see cref="KeypadKey"/>.</summary>
    Unknown = 0,

    // Navigation cluster (d-pad).
    Up,
    Down,
    Left,
    Right,

    /// <summary>The centre / OK button of the navigation cluster.</summary>
    Select,

    /// <summary>The hardware back button.</summary>
    Back,

    // Soft keys sitting under the screen on most keypad phones.
    SoftLeft,
    SoftRight,

    // Call cluster.
    Call,
    EndCall,

    // Dial pad.
    Star,
    Pound,
    D0,
    D1,
    D2,
    D3,
    D4,
    D5,
    D6,
    D7,
    D8,
    D9,

    // Volume rocker.
    VolumeUp,
    VolumeDown,
}
