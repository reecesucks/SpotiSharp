using QinF25.Input;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;
using SpotiSharp.Views;

namespace SpotiSharp;

public partial class MainPage : BasePage
{
	// Sample rows for the keypad navigation test. Replace with real content once
	// physical-key input is proven end to end.
	private static readonly string[] TestItems =
	{
		"First item",
		"Second item",
		"Third item",
		"Fourth item",
		"Fifth item",
	};

	private readonly List<Label> _rowLabels = new();
	private int _selectedIndex;

	public MainPage()
	{
		// calling constructor of CollaborationSessionConnection to add it's actions to the ui loop
		_ = CollaborationSessionConnection.Instance;
		InitializeComponent();
        BindingContext = new MainPageViewModel();

		BuildTestRows();
		HighlightSelectedRow();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		KeypadManager.Instance.KeyPressed += OnKeypadKeyPressed;
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		KeypadManager.Instance.KeyPressed -= OnKeypadKeyPressed;
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
		// Wrap around so holding a direction cycles through the list.
		_selectedIndex = (_selectedIndex + delta % count + count) % count;
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

	private Color GetThemeColor(string key, Color fallback) =>
		Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
			? color
			: fallback;
}
