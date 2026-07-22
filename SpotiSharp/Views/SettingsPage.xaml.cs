using SpotiSharp.Themes;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class SettingsPage : BasePage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = new SettingsPageViewModel();

        ThemeSwitch.IsToggled = ThemeService.Current == AppThemeVariant.Spotify;
    }

    private void OnThemeSwitchToggled(object sender, ToggledEventArgs e)
    {
        ThemeService.Apply(e.Value ? AppThemeVariant.Spotify : AppThemeVariant.Ipod);
    }
}