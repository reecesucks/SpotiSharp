using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class DetailArtistPage : BasePage, IQueryAttributable
{
    public DetailArtistPage()
    {
        InitializeComponent();
        BindingContext = new DetailArtistPageViewModel();

        MainListView.SelectionChanged += (sender, args) =>
        {
            if (args.CurrentSelection.Count > 0 && BindingContext is DetailArtistPageViewModel detailArtistPageViewModel)
                detailArtistPageViewModel.GoToAlbumDetail();
            MainListView.SelectedItem = null;
        };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not DetailArtistPageViewModel bindingContext) return;
        bindingContext.ArtistName = query["ArtistName"] as string;
        bindingContext.ArtistId = query["ArtistId"] as string;
    }
}
