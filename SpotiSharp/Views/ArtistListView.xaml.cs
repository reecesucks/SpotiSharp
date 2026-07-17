using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class ArtistListView : ContentView
{
    public ArtistListView()
    {
        InitializeComponent();
        BindingContext = new ArtistListViewModel();

        MainListView.SelectionChanged += (sender, args) =>
        {
            if (args.CurrentSelection.Count > 0 && BindingContext is ArtistListViewModel artistListViewModel)
                artistListViewModel.GoToArtistDetail();
            MainListView.SelectedItem = null;
        };
    }
}
