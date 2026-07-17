using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class AlbumListView : ContentView
{
    public AlbumListView()
    {
        InitializeComponent();
        BindingContext = new AlbumListViewModel();

        MainListView.SelectionChanged += (sender, args) =>
        {
            if (args.CurrentSelection.Count > 0 && BindingContext is AlbumListViewModel albumListViewModel)
                albumListViewModel.GoToAlbumDetail();
            MainListView.SelectedItem = null;
        };
    }
}
