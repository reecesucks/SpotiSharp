using SpotifyAPI.Web;
using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PlaylistSectionSectionCreator : ContentView
{
	public PlaylistSectionSectionCreator()
	{
		InitializeComponent();
    }

    private void SectionTypeMusicChangedHandler(object sender, EventArgs e)
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

    private void SectionTypePodChangedHandler(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        var sectionType = (PodcastSectionType)picker.SelectedItem;

        lblPlaceholderPod.IsVisible = false;
        entNumericValuePod.IsVisible = false;
        msSongsPod.IsVisible = false;
        switch (sectionType.SectionType)
        {

            case PodcastSectionEnum.NewestUnplayed:
                break;
            case PodcastSectionEnum.RandomUnplayed:
                break;
            case PodcastSectionEnum.SelectEpisodes:
                msSongsPod.IsVisible = true;
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

    public void Podcast_SelectedIndexChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        var selectedItem = (FullShow)picker.SelectedItem;

        if (BindingContext is PlaylistSectionSectionCreatorViewModel vm)
        {
            vm.OnSelectedPodcastChanged(selectedItem);
        }
    }
}