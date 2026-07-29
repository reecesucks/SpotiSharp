using Android.Views;

namespace QinF25.Input;

/// <summary>
/// Translates Android <see cref="Keycode"/> values into device-independent
/// <see cref="KeypadKey"/> values.
/// </summary>
internal static class AndroidKeyMapper
{
    public static KeypadKey Map(Keycode keyCode) => keyCode switch
    {
        Keycode.DpadUp => KeypadKey.Up,
        Keycode.DpadDown => KeypadKey.Down,
        Keycode.DpadLeft => KeypadKey.Left,
        Keycode.DpadRight => KeypadKey.Right,
        Keycode.DpadCenter or Keycode.Enter => KeypadKey.Select,
        Keycode.Back => KeypadKey.Back,

        Keycode.SoftLeft => KeypadKey.SoftLeft,
        Keycode.SoftRight => KeypadKey.SoftRight,

        Keycode.Call => KeypadKey.Call,
        Keycode.Endcall => KeypadKey.EndCall,

        Keycode.Star => KeypadKey.Star,
        Keycode.Pound => KeypadKey.Pound,
        Keycode.Num0 => KeypadKey.D0,
        Keycode.Num1 => KeypadKey.D1,
        Keycode.Num2 => KeypadKey.D2,
        Keycode.Num3 => KeypadKey.D3,
        Keycode.Num4 => KeypadKey.D4,
        Keycode.Num5 => KeypadKey.D5,
        Keycode.Num6 => KeypadKey.D6,
        Keycode.Num7 => KeypadKey.D7,
        Keycode.Num8 => KeypadKey.D8,
        Keycode.Num9 => KeypadKey.D9,

        Keycode.VolumeUp => KeypadKey.VolumeUp,
        Keycode.VolumeDown => KeypadKey.VolumeDown,

        _ => KeypadKey.Unknown,
    };
}
