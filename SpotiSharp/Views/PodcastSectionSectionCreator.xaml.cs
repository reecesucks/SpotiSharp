using SpotifyAPI.Web;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PodcastSectionSectionCreator : ContentView
{
	public PodcastSectionSectionCreator()
	{
		InitializeComponent();
	}

    public void Podcast_SelectedIndexChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        var selectedItem = (SavedShow)picker.SelectedItem;

        if (BindingContext is PodcastSectionSectionCreatorViewModel vm)
        {
            vm.OnSelectedPodcastChanged(selectedItem);
        }
    }

    private void SectionTypeChangedHandler(object sender, EventArgs e)
    {
        //var picker = (Picker)sender;
        //var sectionType = (PlaylistSectionType)picker.SelectedItem;

        //lblPlaceholder.IsVisible = false;
        //entNumericValue.IsVisible = false;
        //msSongs.IsVisible = false;
        //switch (sectionType.SectionType)
        //{

        //    case PlaylistSectionEnum.EntirePlaylist:
        //        break;
        //    case PlaylistSectionEnum.PercentageOfNewPlaylistRandom:
        //        entNumericValue.IsVisible = true;
        //        break;
        //    case PlaylistSectionEnum.FixedAmountSelected:
        //        msSongs.IsVisible = true;
        //        break;
        //    case PlaylistSectionEnum.FixedAmountRandom:
        //        entNumericValue.IsVisible = true;
        //        break;
        //}
    }
}