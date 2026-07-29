namespace QinF25.Input;

/// <summary>
/// Diagnostic payload for <see cref="KeypadManager.RawKeyReceived"/>. Reports
/// every physical key the platform sees — including ones that do not map to a
/// <see cref="KeypadKey"/> — so the raw device codes can be inspected while
/// bringing keypad support up on a new handset.
/// </summary>
public sealed class KeypadRawKeyEventArgs : EventArgs
{
    public KeypadRawKeyEventArgs(int rawCode, string rawName, KeypadKey mappedKey)
    {
        RawCode = rawCode;
        RawName = rawName;
        MappedKey = mappedKey;
    }

    /// <summary>The platform's numeric key code (Android <c>Keycode</c> value).</summary>
    public int RawCode { get; }

    /// <summary>The platform's name for the key code, e.g. <c>"DpadUp"</c>.</summary>
    public string RawName { get; }

    /// <summary>
    /// The <see cref="KeypadKey"/> the raw code mapped to, or
    /// <see cref="KeypadKey.Unknown"/> if there is no mapping yet.
    /// </summary>
    public KeypadKey MappedKey { get; }
}
