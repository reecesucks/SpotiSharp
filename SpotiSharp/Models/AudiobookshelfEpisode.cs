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

    /// <summary>The audio file's "ino" on the AudioBookshelf server, used to build its stream URL.</summary>
    public string AudioFileIno { get; private set; }

    public string EpisodeName { get; private set; }
    public string ShowName { get; private set; }
    public string ShowCoverUrl { get; private set; }
    public DateTime ReleaseDate { get; private set; }
    public int DurationMs { get; private set; }
    public int ResumePositionMs { get; private set; }
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

    public AudiobookshelfEpisode(string episodeId, string libraryItemId, string audioFileIno, string episodeName,
        string showName, string showCoverUrl, DateTime releaseDate, int durationMs, int resumePositionMs = 0,
        List<AudiobookshelfChapter>? chapters = null)
    {
        EpisodeId = episodeId;
        LibraryItemId = libraryItemId;
        AudioFileIno = audioFileIno;
        EpisodeName = episodeName;
        ShowName = showName;
        ShowCoverUrl = showCoverUrl;
        ReleaseDate = releaseDate;
        DurationMs = durationMs;
        ResumePositionMs = resumePositionMs;
        Chapters = chapters ?? new List<AudiobookshelfChapter>();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
