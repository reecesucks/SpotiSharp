using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PlaylistSectionSectionCreator : ContentView
{
	public PlaylistSectionSectionCreator()
	{
		InitializeComponent();
        BindingContext = new PlaylistSectionSectionCreatorViewModel();
    }

    private void SectionTypeChangedHandler(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        var sectionType = (PlaylistSectionType)picker.SelectedItem;

        lblPlaceholder.IsVisible = false;
        entNumericValue.IsVisible = false;
        msSongs.IsVisible = false;
        switch (sectionType.SectionType)
        {

            case PlaylistSectionEnum.EntirePlaylist:
                break;
            case PlaylistSectionEnum.PercentageOfNewPlaylistRandom:
                entNumericValue.IsVisible = true;
                break;
            case PlaylistSectionEnum.FixedAmountSelected:
                msSongs.IsVisible = true;
                break;
            case PlaylistSectionEnum.FixedAmountRandom:
                entNumericValue.IsVisible = true;
                break;
        }
    }

    public void Playlist_SelectedIndexChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        var selectedItem = (Playlist)picker.SelectedItem;

        if (BindingContext is PlaylistSectionSectionCreatorViewModel vm)
        {
            vm.OnSelectedPlaylistChanged(selectedItem);
        }
    }
}