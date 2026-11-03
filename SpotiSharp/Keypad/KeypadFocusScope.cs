using QinF25.Input;

namespace SpotiSharp.Keypad;

/// <summary>
/// Page-level owner of two orthogonal <see cref="IKeypadSection"/>s — one driven by
/// Left/Right, the other by Up/Down. Whichever axis was pressed most recently becomes
/// "active" (gains focus visuals via <see cref="IKeypadSection.SetActive"/>) and is
/// the target of Select. Back is never claimed here, so it still bubbles to Android.
/// </summary>
/// <remarks>
/// Set both sections' <c>SelfManaged</c> to <c>false</c> before attaching a scope —
/// otherwise the section and the scope would both handle the same key.
/// </remarks>
public class KeypadFocusScope
{
    private IKeypadSection _horizontal;
    private readonly IKeypadSection _vertical;
    private IKeypadSection? _active;

    public KeypadFocusScope(IKeypadSection horizontal, IKeypadSection vertical)
    {
        _horizontal = horizontal;
        _vertical = vertical;
    }

    /// <summary>
    /// Hands the Left/Right axis to a different section — for a page where a row appears and
    /// takes over that axis while it is on screen. Only two axes exist, so the section being
    /// replaced loses its focus visuals rather than keeping a highlight nothing can reach.
    /// </summary>
    public void SetHorizontal(IKeypadSection section)
    {
        if (ReferenceEquals(_horizontal, section)) return;

        if (ReferenceEquals(_active, _horizontal))
        {
            _horizontal.SetActive(false);
            _active = null;
        }
        _horizontal = section;
    }

    public void Attach() => KeypadManager.Instance.KeyPressed += OnKeyPressed;

    public void Detach() => KeypadManager.Instance.KeyPressed -= OnKeyPressed;

    private void OnKeyPressed(object? sender, KeypadKeyEventArgs e)
    {
        var target = e.Key switch
        {
            KeypadKey.Left or KeypadKey.Right => _horizontal,
            KeypadKey.Up or KeypadKey.Down => _vertical,
            KeypadKey.Select => _active,
            _ => null
        };
        if (target is null)
            return;

        if (!ReferenceEquals(target, _active))
        {
            _active?.SetActive(false);
            _active = target;
            _active.SetActive(true);
        }

        if (target.HandleKey(e.Key))
            e.Handled = true;
    }
}
