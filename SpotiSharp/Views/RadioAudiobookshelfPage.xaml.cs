using System.ComponentModel;
using QinF25.Input;
using SpotiSharp.Keypad;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioAudiobookshelfPage : BasePage
{
    protected override double TitleTranslationX => 35;

    private RadioAudiobookshelfHeaderButtons _headerButtons;
    private readonly KeypadFocusScope _focusScope;

    private RadioAudiobookshelfPageViewModel ViewModel => (RadioAudiobookshelfPageViewModel)BindingContext;

    public RadioAudiobookshelfPage()
    {
        InitializeComponent();
        BindingContext = new RadioAudiobookshelfPageViewModel();

        _headerButtons.BindingContext = BindingContext;

        _focusScope = new KeypadFocusScope(horizontal: _headerButtons.Row, vertical: MainListView);

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncHorizontalSection();
    }

    protected override View CreateTitleAccessory()
    {
        _headerButtons = new RadioAudiobookshelfHeaderButtons();
        return _headerButtons;
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RadioAudiobookshelfPageViewModel.IsModeStripVisible)) SyncHorizontalSection();
    }

    // The scope only has the two axes, so while the mode strip is on screen it takes Left/Right
    // over from the header buttons - it is the thing the listener just asked for.
    private void SyncHorizontalSection()
    {
        _focusScope.SetHorizontal(ViewModel.IsModeStripVisible ? ModeStrip : _headerButtons.Row);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _focusScope.Attach();
        KeypadManager.Instance.KeyPressed += OnKeyPressed;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _focusScope.Detach();
        KeypadManager.Instance.KeyPressed -= OnKeyPressed;
    }

    // The keypad has no long press, so * stands in for the hold on Generate. The focus scope
    // ignores the dial pad, so claiming this key here can't collide with it.
    private void OnKeyPressed(object sender, KeypadKeyEventArgs e)
    {
        if (e.Key != KeypadKey.Star) return;

        ViewModel.ToggleModeStrip();
        e.Handled = true;
    }

#if ANDROID
    private readonly Dictionary<global::Android.Views.View, View> _rowElements = new();

    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element) return;

        if (element.Handler?.PlatformView is not global::Android.Views.View platformView)
        {
            foreach (var stale in _rowElements.Where(entry => ReferenceEquals(entry.Value, element)).Select(entry => entry.Key).ToList())
            {
                _rowElements.Remove(stale);
            }
            return;
        }

        _rowElements[platformView] = element;

        platformView.Clickable = true;
        platformView.LongClickable = true;

        platformView.Click -= OnRowClick;
        platformView.Click += OnRowClick;
        platformView.LongClick -= OnRowLongClick;
        platformView.LongClick += OnRowLongClick;
    }

    private void OnRowClick(object sender, EventArgs e)
    {
        if (ResolveItem(sender) is not RadioAudiobookshelfItem item || BindingContext is not RadioAudiobookshelfPageViewModel vm) return;

        if (item.IsConfirmingRemove) vm.ClearRemoveOptions();
        else vm.ClickItem(item);
    }

    private void OnRowLongClick(object sender, global::Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;

        if (ResolveItem(sender) is not RadioAudiobookshelfItem item || BindingContext is not RadioAudiobookshelfPageViewModel vm) return;

        if (item.IsConfirmingRemove) vm.ClearRemoveOptions();
        else vm.ShowRemoveOptions(item);
    }

    private RadioAudiobookshelfItem ResolveItem(object platformSender)
    {
        if (platformSender is not global::Android.Views.View platformView) return null;
        return _rowElements.TryGetValue(platformView, out var element) ? element.BindingContext as RadioAudiobookshelfItem : null;
    }
#else
    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element || element.GestureRecognizers.Count > 0) return;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, _) =>
        {
            if ((s as VisualElement)?.BindingContext is RadioAudiobookshelfItem item && BindingContext is RadioAudiobookshelfPageViewModel vm)
                vm.ClickItem(item);
        };
        element.GestureRecognizers.Add(tap);
    }
#endif

}
