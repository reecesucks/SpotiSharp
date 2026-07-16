using SpotiSharp.ViewModels;

namespace SpotiSharp;

public partial class PlaylistListView : ContentView
{
    public PlaylistListView()
    {
        InitializeComponent();
        BindingContext = new PlaylistListViewModel();
        
        MainListView.SelectionChanged += (sender, args) =>
        {
            if (args.CurrentSelection.Count > 0 && BindingContext is PlaylistListViewModel playlistListViewModel)
                playlistListViewModel.GoToPlaylistDetail();
            MainListView.SelectedItem = null;
        };
    }
}