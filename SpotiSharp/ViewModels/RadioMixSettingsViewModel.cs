using SpotiSharp.Models;

namespace SpotiSharp.ViewModels;

public class RadioMixSettingsViewModel : BaseViewModel
{
    public string Title => "Mix";

    private int _songsPerSection;

    public int SongsPerSection
    {
        get { return _songsPerSection; }
        set
        {
            if (SetProperty(ref _songsPerSection, value)) RadioConfigModel.SetSongsPerSection(value);
        }
    }

    private int _podcastSegmentMinutes;

    public int PodcastSegmentMinutes
    {
        get { return _podcastSegmentMinutes; }
        set
        {
            if (SetProperty(ref _podcastSegmentMinutes, value)) RadioConfigModel.SetPodcastSegmentMinutes(value);
        }
    }

    private bool _fullPodcastEpisodes;

    public bool FullPodcastEpisodes
    {
        get { return _fullPodcastEpisodes; }
        set
        {
            if (!SetProperty(ref _fullPodcastEpisodes, value)) return;
            RadioConfigModel.SetFullPodcastEpisodes(value);
            OnPropertyChanged(nameof(SegmentLengthApplies));
        }
    }

    public bool SegmentLengthApplies => !FullPodcastEpisodes;

    public RadioMixSettingsViewModel()
    {
        _songsPerSection = RadioConfigModel.GetSongsPerSection();
        _podcastSegmentMinutes = RadioConfigModel.GetPodcastSegmentMinutes();
        _fullPodcastEpisodes = RadioConfigModel.GetFullPodcastEpisodes();
    }
}
