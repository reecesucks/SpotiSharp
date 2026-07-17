using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class DetailAlbumPage : BasePage, IQueryAttributable
{
    public DetailAlbumPage()
    {
        InitializeComponent();
        BindingContext = new DetailAlbumPageViewModel();

        MainListView.SelectionChanged += (sender, args) =>
        {
            var selectedSong = args.CurrentSelection.FirstOrDefault();
            if (selectedSong != null && BindingContext is DetailAlbumPageViewModel detailAlbumPageViewModel)
                detailAlbumPageViewModel.ClickSong(selectedSong);
            MainListView.SelectedItem = null;
        };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not DetailAlbumPageViewModel bindingContext) return;
        bindingContext.AlbumName = query["AlbumName"] as string;
        bindingContext.AlbumImageUrl = query["AlbumImageUrl"] as string;
        bindingContext.AlbumId = query["AlbumId"] as string;
    }
}
