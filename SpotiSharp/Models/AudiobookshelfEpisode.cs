using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SpotiSharp.Models;

/// <summary>UI-facing episode row for the AudioBookshelf podcasts page. Deliberately not shared with
/// Spotify's <see cref="RecentEpisode"/> or <see cref="RadioItem"/> — this is the isolated AudioBookshelf path.
/// Implements <see cref="IPodcastEpisodeSource"/> so a future <see cref="IPodcastEpisodeProvider"/> for
/// AudioBookshelf shows can hand these straight to <see cref="RadioAudiobookshelfModel"/>.</summary>
public class AudiobookshelfEpisode : INotifyPropertyChanged, IPodcastEpisodeSource
{
    public string EpisodeId { get; private set; }
    public string LibraryItemId { get; private set; }

    /// <summary>The podcast-feed guid for this episode, stable across AudioBookshelf deleting and
    /// re-downloading it (unlike <see cref="EpisodeId"/>, which is tied to the specific downloaded-episode
    /// row and stops existing once that row is gone). Used to anchor a binge pin so it survives that.</summary>
    public string? Guid { get; private set; }

    /// <summary>The audio file's "ino" on the AudioBookshelf server, used to build its stream URL.</summary>
    public string AudioFileIno { get; private set; }

    public string EpisodeName { get; private set; }
    public string ShowName { get; private set; }
    public string ShowCoverUrl { get; private set; }
    public DateTime ReleaseDate { get; private set; }
    public int DurationMs { get; private set; }
    public int ResumePositionMs { get; private set; }

    /// <summary>Whether the AudioBookshelf server considers this episode finished, as of the last
    /// time the episode list was refreshed - lets <see cref="AudiobookshelfPodcastEpisodeProvider"/>
    /// exclude it from Radio's pool without a network round trip of its own.</summary>
    public bool IsFinished { get; private set; }

    public List<AudiobookshelfChapter> Chapters { get; private set; }

    // IPodcastEpisodeSource - lets this flow through RadioAudiobookshelfModel's segment-building
    // the same way a Spotify RecentEpisode does. Not a real playable Spotify URI; just a stable key
    // AudiobookshelfPodcastPlaybackAdapter re-resolves back to this episode at play time.
    [JsonIgnore]
    string IPodcastEpisodeSource.PlayUri => $"audiobookshelf:episode:{EpisodeId}";

    [JsonIgnore]
    string IPodcastEpisodeSource.ShowImageUrl => ShowCoverUrl;

    [JsonIgnore]
    PodcastSourceKind IPodcastEpisodeSource.SourceKind => PodcastSourceKind.Audiobookshelf;

    [JsonIgnore]
    public IReadOnlyList<int> SpliceStartMs =>
        Chapters.Where(chapter => chapter.IsSplice).Select(chapter => (int)(chapter.StartSeconds * 1000)).OrderBy(ms => ms).ToList();

    // Live download state, re-derived from AudiobookshelfDownloadsModel's own on-disk index each time
    // the episode list is built - never part of the cached episode-list JSON itself.
    private bool _isDownloaded;
    [JsonIgnore]
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set { if (_isDownloaded == value) return; _isDownloaded = value; OnPropertyChanged(); }
    }

    private bool _isDownloading;
    [JsonIgnore]
    public bool IsDownloading
    {
        get => _isDownloading;
        set { if (_isDownloading == value) return; _isDownloading = value; OnPropertyChanged(); }
    }

    // -1 until the server has told us the total size (or we haven't started reading the body yet),
    // meaning "show an indeterminate spinner" rather than a 0% ring - 0 would misleadingly suggest a
    // known, empty-so-far progress rather than an unknown one.
    private double _downloadProgress = -1;
    [JsonIgnore]
    public double DownloadProgress
    {
        get => _downloadProgress;
        set { if (_downloadProgress == value) return; _downloadProgress = value; OnPropertyChanged(); }
    }

    // Mirrors RadioAudiobookshelfItem's "Up next" marker - set from RadioAudiobookshelfConfigModel's
    // UpNextEpisodes whenever the Podcasts page's recency list is (re)built or a mark is toggled.
    // Never persisted itself; UpNextEpisodes is the source of truth.
    private string? _upNextLabel;
    [JsonIgnore]
    public string? UpNextLabel
    {
        get => _upNextLabel;
        set
        {
            if (_upNextLabel == value) return;
            _upNextLabel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUpNextLabel));
        }
    }

    [JsonIgnore]
    public bool HasUpNextLabel => !string.IsNullOrEmpty(UpNextLabel);

    public AudiobookshelfEpisode(string episodeId, string libraryItemId, string audioFileIno, string episodeName,
        string showName, string showCoverUrl, DateTime releaseDate, int durationMs, int resumePositionMs = 0,
        List<AudiobookshelfChapter>? chapters = null, bool isFinished = false, string? guid = null)
    {
        EpisodeId = episodeId;
        LibraryItemId = libraryItemId;
        Guid = guid;
        AudioFileIno = audioFileIno;
        EpisodeName = episodeName;
        ShowName = showName;
        ShowCoverUrl = showCoverUrl;
        ReleaseDate = releaseDate;
        DurationMs = durationMs;
        ResumePositionMs = resumePositionMs;
        Chapters = chapters ?? new List<AudiobookshelfChapter>();
        IsFinished = isFinished;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
