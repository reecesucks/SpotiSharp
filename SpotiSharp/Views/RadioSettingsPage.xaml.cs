using QinF25.Input;
using SpotiSharp.Keypad;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioSettingsPage : BasePage
{
    private readonly Dictionary<object, KeypadCollectionView> _pageLists = new();

    public RadioSettingsPage()
    {
        InitializeComponent();
        BindingContext = new RadioSettingsPageViewModel();
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

    private void OnKeyPressed(object sender, KeypadKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KeypadKey.Left:
                e.Handled = MovePage(-1);
                break;
            case KeypadKey.Right:
                e.Handled = MovePage(1);
                break;
            case KeypadKey.Up:
            case KeypadKey.Down:
                e.Handled = CurrentPageList()?.HandleKey(e.Key) == true;
                break;
        }
    }

    private bool MovePage(int delta)
    {
        if (BindingContext is not RadioSettingsPageViewModel vm) return false;

        var next = PagesCarousel.Position + delta;
        if (next < 0 || next >= vm.Pages.Count) return false;

        PagesCarousel.Position = next;
        return true;
    }

    private KeypadCollectionView? CurrentPageList()
    {
        if (BindingContext is not RadioSettingsPageViewModel vm) return null;

        var page = vm.Pages.ElementAtOrDefault(PagesCarousel.Position);
        return page != null && _pageLists.TryGetValue(page, out var view) ? view : null;
    }

    private void OnPageListLoaded(object sender, EventArgs e)
    {
        if (sender is not KeypadCollectionView view || view.BindingContext is null) return;
        _pageLists[view.BindingContext] = view;
    }

    private void OnPageListUnloaded(object sender, EventArgs e)
    {
        if (sender is not KeypadCollectionView view || view.BindingContext is null) return;
        if (_pageLists.TryGetValue(view.BindingContext, out var existing) && ReferenceEquals(existing, view))
            _pageLists.Remove(view.BindingContext);
    }
}
