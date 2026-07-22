namespace QinF25.Input;

/// <summary>
/// Carries a single physical key press to subscribers of
/// <see cref="KeypadManager.KeyPressed"/>. A subscriber that consumes the key
/// should set <see cref="Handled"/> to <c>true</c> so the platform layer can
/// suppress the system's default behaviour for that key.
/// </summary>
public sealed class KeypadKeyEventArgs : EventArgs
{
    public KeypadKeyEventArgs(KeypadKey key)
    {
        Key = key;
    }

    /// <summary>The device-independent key that was pressed.</summary>
    public KeypadKey Key { get; }

    /// <summary>
    /// Set to <c>true</c> by a subscriber that has acted on the key. When any
    /// subscriber sets this, <see cref="KeypadManager.Dispatch"/> reports the
    /// key as handled and the platform layer stops it bubbling to the OS.
    /// </summary>
    public bool Handled { get; set; }
}
