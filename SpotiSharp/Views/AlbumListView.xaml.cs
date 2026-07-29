using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class AlbumListView : ContentView
{
    public AlbumListView()
    {
        InitializeComponent();
        BindingContext = new AlbumListViewModel();
    }
}
