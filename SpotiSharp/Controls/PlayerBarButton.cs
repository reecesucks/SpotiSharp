using System.Windows.Input;

namespace SpotiSharp.Controls;

public class PlayerBarButton : ContentView
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PlayerBarButton));

    public ICommand Command
    {
        get { return (ICommand)GetValue(CommandProperty); }
        set { SetValue(CommandProperty, value); }
    }

    public PlayerBarButton()
    {
        BackgroundColor = Colors.Transparent;
        var tapGestureRecognizer = new TapGestureRecognizer();
        tapGestureRecognizer.Tapped += OnTapped;
        GestureRecognizers.Add(tapGestureRecognizer);
    }

    private async void OnTapped(object sender, TappedEventArgs e)
    {
        Command?.Execute(null);

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // haptics unsupported on this platform
        }

        var target = Content ?? (View)this;
        await target.ScaleTo(1.35, 90, Easing.CubicOut);
        await target.ScaleTo(1.0, 120, Easing.CubicIn);
    }
}
