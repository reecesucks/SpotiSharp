namespace QinF25.Input;

/// <summary>
/// Central hub for physical keypad input. The platform layer feeds raw key
/// presses in through <see cref="Dispatch"/>; pages and view models subscribe
/// to <see cref="KeyPressed"/> to react to them.
/// </summary>
/// <remarks>
/// This type is intentionally platform-agnostic so it lives in the shared part
/// of the library and can be referenced from anywhere without pulling in an
/// Android/iOS dependency.
/// </remarks>
public sealed class KeypadManager
{
    private static readonly Lazy<KeypadManager> _instance = new(() => new KeypadManager());

    private KeypadManager()
    {
    }

    /// <summary>The process-wide keypad manager.</summary>
    public static KeypadManager Instance => _instance.Value;

    /// <summary>
    /// Raised on the thread that called <see cref="Dispatch"/>. On Android that
    /// is the UI thread, so handlers may touch the UI directly.
    /// </summary>
    public event EventHandler<KeypadKeyEventArgs>? KeyPressed;

    /// <summary>
    /// Raises <see cref="KeyPressed"/> for <paramref name="key"/> and reports
    /// whether a subscriber consumed it.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a subscriber set <see cref="KeypadKeyEventArgs.Handled"/>;
    /// otherwise <c>false</c> (including when there are no subscribers, or the
    /// key is <see cref="KeypadKey.Unknown"/>).
    /// </returns>
    public bool Dispatch(KeypadKey key)
    {
        if (key == KeypadKey.Unknown)
            return false;

        var handler = KeyPressed;
        if (handler is null)
            return false;

        var args = new KeypadKeyEventArgs(key);
        handler(this, args);
        return args.Handled;
    }
}
