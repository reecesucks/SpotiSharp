using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioPage : BasePage
{
    public RadioPage()
    {
        InitializeComponent();
        BindingContext = new RadioPageViewModel();
    }

#if ANDROID
    private readonly Dictionary<global::Android.Views.View, View> _rowElements = new();

    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element) return;
        if (element.Handler?.PlatformView is not global::Android.Views.View platformView) return;

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
        if (ResolveItem(sender) is RadioItem item && BindingContext is RadioPageViewModel vm)
            vm.ClickItem(item);
    }

    private void OnRowLongClick(object sender, global::Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;

        if (ResolveItem(sender) is RadioItem item) OnItemLongPressed(item);
    }

    private RadioItem ResolveItem(object platformSender)
    {
        if (platformSender is not global::Android.Views.View platformView) return null;
        return _rowElements.TryGetValue(platformView, out var element) ? element.BindingContext as RadioItem : null;
    }
#else
    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element || element.GestureRecognizers.Count > 0) return;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, _) =>
        {
            if ((s as VisualElement)?.BindingContext is RadioItem item && BindingContext is RadioPageViewModel vm)
                vm.ClickItem(item);
        };
        element.GestureRecognizers.Add(tap);
    }
#endif

    private async void OnItemLongPressed(RadioItem item)
    {
        if (item == null || BindingContext is not RadioPageViewModel vm) return;

        string what = item.IsPodcastSegment ? "podcast" : "song";
        bool remove = await DisplayAlert("Remove", $"Remove this {what} from the radio?", "Remove", "Cancel");
        if (remove) vm.RemoveRadioItem(item);
    }
}
