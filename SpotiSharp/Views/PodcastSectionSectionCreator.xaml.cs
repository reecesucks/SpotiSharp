using SpotifyAPI.Web;
using SpotiSharp.Enums;
using SpotiSharp.Models;
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
        var selectedItem = (FullShow)picker.SelectedItem;

        if (BindingContext is PodcastSectionSectionCreatorViewModel vm)
        {
            vm.OnSelectedPodcastChanged(selectedItem);
        }
    }

    private void SectionTypeChangedHandler(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        var sectionType = (PodcastSectionType)picker.SelectedItem;

        lblPlaceholder.IsVisible = false;
        entNumericValue.IsVisible = false;
        msSongs.IsVisible = false;
        switch (sectionType.SectionType)
        {

            case PodcastSectionEnum.NewestUnplayed:
                break;
            case PodcastSectionEnum.RandomUnplayed:
                break;
            case PodcastSectionEnum.SelectEpisodes:
                msSongs.IsVisible = true;
                break;
        }
    }
}