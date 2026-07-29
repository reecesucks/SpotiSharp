using QinF25.Input;

namespace SpotiSharp.Views;

// Temporary page for exercising QinF25.Input on-device. Delete once keypad input
// is proven and real navigation is wired.
public partial class KeypadTestPage : BasePage
{
	private static readonly string[] TestItems =
	{
		"First item",
		"Second item",
		"Third item",
		"Fourth item",
		"Fifth item",
	};

	private const int RawLogMaxEntries = 8;

	private readonly List<Label> _rowLabels = new();
	private readonly LinkedList<string> _rawLog = new();
	// -1 = nothing selected yet, so the very first Up/Down lands on a row rather than
	// jumping past the already-highlighted first row.
	private int _selectedIndex = -1;

	public KeypadTestPage()
	{
		InitializeComponent();

		BuildTestRows();
		HighlightSelectedRow();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		KeypadManager.Instance.KeyPressed += OnKeypadKeyPressed;
		KeypadManager.Instance.RawKeyReceived += OnRawKeyReceived;
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		KeypadManager.Instance.KeyPressed -= OnKeypadKeyPressed;
		KeypadManager.Instance.RawKeyReceived -= OnRawKeyReceived;
	}

	// Diagnostics: record every raw key the device emits, mapped or not, so we can
	// see exactly which codes the handset sends.
	private void OnRawKeyReceived(object? sender, KeypadRawKeyEventArgs e)
	{
		_rawLog.AddFirst($"{e.RawName} ({e.RawCode}) → {e.MappedKey}");
		while (_rawLog.Count > RawLogMaxEntries)
			_rawLog.RemoveLast();

		KeypadRawLog.Text = string.Join(Environment.NewLine, _rawLog);
	}

	private void BuildTestRows()
	{
		for (var i = 0; i < TestItems.Length; i++)
		{
			var label = new Label
			{
				Text = TestItems[i],
				FontSize = 18,
				Padding = new Thickness(12, 10),
			};

			// Tapping a row selects it and activates it — the same path the Select key uses.
			var index = i;
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) =>
			{
				_selectedIndex = index;
				HighlightSelectedRow();
				ActivateSelectedItem();
			};
			label.GestureRecognizers.Add(tap);

			_rowLabels.Add(label);
			KeypadTestList.Add(label);
		}
	}

	private void OnKeypadKeyPressed(object? sender, KeypadKeyEventArgs e)
	{
		KeypadStatusLabel.Text = $"Last key: {e.Key}";

		switch (e.Key)
		{
			case KeypadKey.Up:
				MoveSelection(-1);
				e.Handled = true;
				break;
			case KeypadKey.Down:
				MoveSelection(1);
				e.Handled = true;
				break;
			case KeypadKey.Select:
				ActivateSelectedItem();
				e.Handled = true;
				break;
			case KeypadKey.Back:
				// Consume on this test page so Back registers visibly ("Last key: Back")
				// instead of immediately navigating away. Test-only — real pages should
				// leave Back unhandled so it bubbles to normal back navigation.
				e.Handled = true;
				break;
		}
	}

	// Runs the "open" action for the highlighted row. Both the Select key and a row
	// tap route through here so they behave identically.
	private void ActivateSelectedItem()
	{
		if (_selectedIndex < 0 || _selectedIndex >= _rowLabels.Count)
			return;

		KeypadStatusLabel.Text = $"Activated: {TestItems[_selectedIndex]}";
	}

	private void MoveSelection(int delta)
	{
		if (_rowLabels.Count == 0)
			return;

		var count = _rowLabels.Count;
		_selectedIndex = _selectedIndex < 0
			// First press with nothing selected: enter the list at the top (Down) or bottom (Up).
			? (delta > 0 ? 0 : count - 1)
			// Thereafter wrap around so holding a direction cycles through the list.
			: (_selectedIndex + delta + count) % count;
		HighlightSelectedRow();
	}

	private void HighlightSelectedRow()
	{
		for (var i = 0; i < _rowLabels.Count; i++)
		{
			var selected = i == _selectedIndex;
			_rowLabels[i].BackgroundColor = selected
				? GetThemeColor("SelectedRowBackground", Colors.SlateGray)
				: Colors.Transparent;
			_rowLabels[i].TextColor = selected
				? GetThemeColor("SelectedRowText", Colors.White)
				: GetThemeColor("TextPrimary", Colors.Black);
		}
	}

	private static Color GetThemeColor(string key, Color fallback) =>
		Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
			? color
			: fallback;
}
