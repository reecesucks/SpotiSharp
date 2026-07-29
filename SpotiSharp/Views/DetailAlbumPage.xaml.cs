using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class DetailAlbumPage : BasePage, IQueryAttributable
{
    public DetailAlbumPage()
    {
        InitializeComponent();
        BindingContext = new DetailAlbumPageViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not DetailAlbumPageViewModel bindingContext) return;
        bindingContext.AlbumName = query["AlbumName"] as string;
        bindingContext.AlbumImageUrl = query["AlbumImageUrl"] as string;
        bindingContext.AlbumId = query["AlbumId"] as string;
    }
}
