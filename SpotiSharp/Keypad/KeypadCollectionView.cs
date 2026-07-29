using System.Collections;
using System.Windows.Input;
using QinF25.Input;

namespace SpotiSharp.Keypad;

/// <summary>
/// A <see cref="CollectionView"/> that is navigable by the physical keypad. Up/Down
/// move the selection (which doubles as the on-screen focus highlight) and scroll it
/// into view; Select — and a touch tap — both run <see cref="ActivateCommand"/> with
/// the item as its parameter, so "Select == tap" by construction.
/// </summary>
/// <remarks>
/// Drop it into XAML in place of a <c>CollectionView</c> and bind
/// <see cref="ActivateCommand"/>; no code-behind needed. While loaded it subscribes
/// to <see cref="KeypadManager"/> itself, which is all a single-section page needs.
/// It also implements <see cref="IKeypadSection"/> so a future page-level focus scope
/// can drive it instead (see <see cref="SelfManaged"/>).
/// </remarks>
public class KeypadCollectionView : CollectionView, IKeypadSection
{
    public static readonly BindableProperty ActivateCommandProperty =
        BindableProperty.Create(nameof(ActivateCommand), typeof(ICommand), typeof(KeypadCollectionView));

    /// <summary>Run when an item is activated (Select key or touch tap). The item is the parameter.</summary>
    public ICommand? ActivateCommand
    {
        get => (ICommand?)GetValue(ActivateCommandProperty);
        set => SetValue(ActivateCommandProperty, value);
    }

    /// <summary>
    /// When <c>true</c> (the default) the control listens for keypad input itself.
    /// A focus scope sets this to <c>false</c> so it can route keys via
    /// <see cref="HandleKey"/> instead, avoiding double handling.
    /// </summary>
    public bool SelfManaged { get; set; } = true;

    private bool _movingFocus;

    public KeypadCollectionView()
    {
        SelectionMode = SelectionMode.Single;
        SelectionChanged += OnSelectionChanged;
        Loaded += (_, _) =>
        {
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
            case KeypadKey.Up:
                MoveFocus(-1);
                return true;
            case KeypadKey.Down:
                MoveFocus(1);
                return true;
            case KeypadKey.Select:
                if (SelectedItem is { } selected)
                {
                    Activate(selected);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    public void SetActive(bool active)
    {
        if (!active)
            SetSelectionSilently(null);
    }

    private void OnKeyPressed(object? sender, KeypadKeyEventArgs e)
    {
        if (HandleKey(e.Key))
            e.Handled = true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Keypad focus move — highlight only, don't activate.
        if (_movingFocus)
            return;

        // Touch tap: activate, same as pressing Select.
        if (e.CurrentSelection.FirstOrDefault() is { } item)
            Activate(item);
    }

    private void Activate(object item)
    {
        if (ActivateCommand?.CanExecute(item) == true)
            ActivateCommand.Execute(item);
    }

    private void MoveFocus(int delta)
    {
        if (ItemsSource is not IList items || items.Count == 0)
            return;

        var current = SelectedItem is null ? -1 : items.IndexOf(SelectedItem);
        var next = current < 0
            // First press with nothing selected: enter at the top (Down) or bottom (Up).
            ? (delta > 0 ? 0 : items.Count - 1)
            // Thereafter wrap around.
            : (current + delta + items.Count) % items.Count;

        SetSelectionSilently(items[next]);
        ScrollTo(next, position: ScrollToPosition.MakeVisible, animate: false);
    }

    private void SetSelectionSilently(object? item)
    {
        _movingFocus = true;
        SelectedItem = item;
        _movingFocus = false;
    }
}
