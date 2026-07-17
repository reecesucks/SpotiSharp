using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioSettingsPage : BasePage
{
    public RadioSettingsPage()
    {
        InitializeComponent();
        BindingContext = new RadioSettingsPageViewModel();
    }
}
