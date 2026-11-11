using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfMixSettingsViewModel : BaseViewModel
{
    public string Title => "Mix";

    private int _songsPerSection;

    public int SongsPerSection
    {
        get { return _songsPerSection; }
        set
        {
            if (SetProperty(ref _songsPerSection, value)) RadioAudiobookshelfConfigModel.SetSongsPerSection(value);
        }
    }

    private int _podcastSegmentMinutes;

    public int PodcastSegmentMinutes
    {
        get { return _podcastSegmentMinutes; }
        set
        {
            if (SetProperty(ref _podcastSegmentMinutes, value)) RadioAudiobookshelfConfigModel.SetPodcastSegmentMinutes(value);
        }
    }

    private bool _fullPodcastEpisodes;

    public bool FullPodcastEpisodes
    {
        get { return _fullPodcastEpisodes; }
        set
        {
            if (!SetProperty(ref _fullPodcastEpisodes, value)) return;
            RadioAudiobookshelfConfigModel.SetFullPodcastEpisodes(value);
            OnPropertyChanged(nameof(SegmentLengthApplies));
        }
    }

    public bool SegmentLengthApplies => !FullPodcastEpisodes;

    public RadioAudiobookshelfMixSettingsViewModel()
    {
        _songsPerSection = RadioAudiobookshelfConfigModel.GetSongsPerSection();
        _podcastSegmentMinutes = RadioAudiobookshelfConfigModel.GetPodcastSegmentMinutes();
        _fullPodcastEpisodes = RadioAudiobookshelfConfigModel.GetFullPodcastEpisodes();
    }
}
