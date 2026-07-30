using SpotiSharp.Keypad;

namespace SpotiSharp.Views;

public partial class RadioHeaderButtons : ContentView
{
    public RadioHeaderButtons()
    {
        InitializeComponent();
    }

    public KeypadButtonRow Row => HeaderRow;
}
