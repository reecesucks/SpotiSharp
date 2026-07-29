using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class ArtistListView : ContentView
{
    public ArtistListView()
    {
        InitializeComponent();
        BindingContext = new ArtistListViewModel();
    }
}
