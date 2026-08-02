using SpotiSharp.ViewModels;
using SpotiSharp.Views;

namespace SpotiSharp;

public partial class AuthenticationPage : BasePage
{
    protected override bool ShowPlayerBar => false;

    public AuthenticationPage()
    {
        InitializeComponent();
        BindingContext = new AuthenticationPageViewModel();
    }
}