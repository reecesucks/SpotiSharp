using System.Linq;
using QinF25.Input;

namespace SpotiSharp.Keypad;

/// <summary>
/// A row of buttons navigable by Left/Right, for header-style option groups (e.g.
/// Generate/Settings) where <see cref="KeypadCollectionView"/> doesn't apply. Each
/// child should be a <see cref="Button"/> with its own <c>Command</c> already wired
/// for touch — Select invokes that same command, so "Select == tap" here too.
/// </summary>
/// <remarks>
/// Children show focus via a custom "KeypadStates" VisualStateGroup with
/// "Focused"/"Unfocused" states, kept separate from the built-in CommonStates group
/// so a Select tap's native Pressed animation can't clobber the highlight.
/// </remarks>
public class KeypadButtonRow : Grid, IKeypadSection
{
    /// <summary>
    /// When <c>true</c> (the default) the row listens for keypad input itself. A
    /// <see cref="KeypadFocusScope"/> sets this to <c>false</c> so it can route keys
    /// via <see cref="HandleKey"/> instead, avoiding double handling.
    /// </summary>
    public bool SelfManaged { get; set; } = true;

    private int _focusedIndex = -1;

    public KeypadButtonRow()
    {
        Loaded += (_, _) =>
        {
            foreach (var child in Children.OfType<VisualElement>())
                VisualStateManager.GoToState(child, "Unfocused");

            if (SelfManaged)
                KeypadManager.Instance.KeyPressed += OnKeyPressed;
        };
        Unloaded += (_, _) =>
        {
            if (SelfManaged)
                KeypadManager.Instance.KeyPressed -= OnKeyPressed;
        };
    }

    public bool HandleKey(KeypadKey key)
    {
        switch (key)
        {
            case KeypadKey.Left:
                MoveFocus(-1);
                return true;
            case KeypadKey.Right:
                MoveFocus(1);
                return true;
            case KeypadKey.Select:
                return Activate();
            default:
                return false;
        }
    }

    public void SetActive(bool active)
    {
        if (!active)
            SetFocusedIndex(-1);
    }

    private void OnKeyPressed(object? sender, KeypadKeyEventArgs e)
    {
        if (HandleKey(e.Key))
            e.Handled = true;
    }

    private void MoveFocus(int delta)
    {
        if (Children.Count == 0)
            return;

        var next = _focusedIndex < 0
            ? (delta > 0 ? 0 : Children.Count - 1)
            : (_focusedIndex + delta + Children.Count) % Children.Count;

        SetFocusedIndex(next);
    }

    private void SetFocusedIndex(int index)
    {
        if (_focusedIndex >= 0 && _focusedIndex < Children.Count)
            VisualStateManager.GoToState((VisualElement)Children[_focusedIndex], "Unfocused");

        _focusedIndex = index;

        if (_focusedIndex >= 0 && _focusedIndex < Children.Count)
            VisualStateManager.GoToState((VisualElement)Children[_focusedIndex], "Focused");
    }

    private bool Activate()
    {
        if (_focusedIndex < 0 || _focusedIndex >= Children.Count)
            return false;
        if (Children[_focusedIndex] is not Button button)
            return false;
        if (button.Command?.CanExecute(button.CommandParameter) != true)
            return false;

        button.Command.Execute(button.CommandParameter);
        return true;
    }
}
