using SpotiSharp.Keypad;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioAudiobookshelfHeaderButtons : ContentView
{
    public RadioAudiobookshelfHeaderButtons()
    {
        InitializeComponent();
    }

    public KeypadButtonRow Row => HeaderRow;

#if ANDROID
    // Android delivers a long press instead of a click, so Generate keeps working as a tap while
    // a hold reveals the listen mode strip. Done on the platform view for the same reason the
    // radio rows are: a maui gesture recognizer here would swallow the button's own touch.
    private void OnGenerateHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element) return;
        if (element.Handler?.PlatformView is not global::Android.Views.View platformView) return;

        platformView.LongClickable = true;
        platformView.LongClick -= OnGenerateLongClick;
        platformView.LongClick += OnGenerateLongClick;
    }

    private void OnGenerateLongClick(object sender, global::Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;
        (BindingContext as RadioAudiobookshelfPageViewModel)?.ToggleModeStrip();
    }
#else
    private void OnGenerateHandlerChanged(object sender, EventArgs e)
    {
    }
#endif
}
