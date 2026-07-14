using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PodcastsPage : BasePage
{
    public PodcastsPage()
    {
        InitializeComponent();
        BindingContext = new PodcastsPageViewModel();
    }
}
