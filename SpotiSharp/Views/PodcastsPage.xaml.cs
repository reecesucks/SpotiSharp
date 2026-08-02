using QinF25.Input;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PodcastsPage : BasePage
{
    private readonly PodcastsPageViewModel _viewModel;

    public PodcastsPage()
    {
        InitializeComponent();
        _viewModel = new PodcastsPageViewModel();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        KeypadManager.Instance.KeyPressed += OnKeyPressed;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        KeypadManager.Instance.KeyPressed -= OnKeyPressed;
    }

    private void OnKeyPressed(object? sender, KeypadKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KeypadKey.Left:
                if (_viewModel.CurrentPageIndex <= 0) return;
                _viewModel.CurrentPageIndex--;
                e.Handled = true;
                break;
            case KeypadKey.Right:
                if (_viewModel.CurrentPageIndex >= _viewModel.Pages.Count - 1) return;
                _viewModel.CurrentPageIndex++;
                e.Handled = true;
                break;
            case KeypadKey.Up:
            case KeypadKey.Down:
            case KeypadKey.Select:
                var section = _viewModel.CurrentPageIndex == 0
                    ? _viewModel.GroupedViewModel.Section
                    : _viewModel.FlatViewModel.Section;
                if (section?.HandleKey(e.Key) == true) e.Handled = true;
                break;
        }
    }
}
