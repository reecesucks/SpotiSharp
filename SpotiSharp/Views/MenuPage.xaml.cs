using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class MenuPage : BasePage
{
    public MenuPage()
    {
        InitializeComponent();
        BindingContext = new MenuViewModel();
    }
}
