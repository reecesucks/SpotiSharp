using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RecentEpisodesListView : ContentView
{
    public RecentEpisodesListView()
    {
        InitializeComponent();
        BindingContext = new RecentEpisodesListViewModel();
    }
}
