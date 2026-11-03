using System.ComponentModel;
using System.Text.Json.Serialization;
using SpotiSharpBackend.Radio;

namespace SpotiSharp.Models;

public class RadioItem : INotifyPropertyChanged, IRadioQueueItem
{
    public bool IsPodcastSegment { get; }
    public bool IsFinalPodcastSegment { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string ImageUrl { get; }
    public string PlayUri { get; }

    public int PositionMs { get; }

    // A whole episode standing in for all of its segments, built by the podcasts-only view. It
    // plays from PositionMs to the end, so it carries no part-of-n subtitle and no pips.
    public bool IsWholeEpisode { get; }

    // The episode's own name and full length, kept so the podcasts-only view can collapse its
    // segments without going back to the episode cache. Both are absent for songs, and for
    // radios cached before they existed.
    public string ShowName { get; }

    public int DurationMs { get; }

    public List<bool> SegmentPips { get; }

    // Short label for where a song came from ("R3", "Liked", a shortened playlist or album name).
    // Null for podcasts, and for radios cached before this existed.
    public string Source { get; }

    private bool _isCurrent;

    [JsonIgnore]
    public bool IsCurrent
    {
        get { return _isCurrent; }
        set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    private string _debugSegmentRemaining;

    [JsonIgnore]
    public string DebugSegmentRemaining
    {
        get { return _debugSegmentRemaining; }
        set
        {
            if (_debugSegmentRemaining == value) return;
            _debugSegmentRemaining = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugSegmentRemaining)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDebugSegmentRemaining)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRemainingLabel)));
        }
    }

    [JsonIgnore]
    public bool HasDebugSegmentRemaining => !string.IsNullOrEmpty(DebugSegmentRemaining);

    /// <summary>How much of the episode is left, shown on a collapsed podcasts-only row.</summary>
    [JsonIgnore]
    public string RemainingLabel
    {
        get
        {
            if (!IsWholeEpisode || DurationMs <= PositionMs) return null;

            var left = TimeSpan.FromMilliseconds(DurationMs - PositionMs);
            return left.TotalMinutes >= 60
                ? $"{(int)left.TotalHours}h {left.Minutes}m left"
                : $"{Math.Max(1, (int)left.TotalMinutes)} min left";
        }
    }

    // Shares the row's right-hand cell with the debug segment timer, which wins while it's on.
    [JsonIgnore]
    public bool HasRemainingLabel => !HasDebugSegmentRemaining && RemainingLabel != null;

    // A segment of an episode that has others: only then is removing one section different from
    // removing the lot. A collapsed row is the lot, so it gets a single remove instead.
    [JsonIgnore]
    public bool HasSegmentActions => IsPodcastSegment && !IsWholeEpisode;

    private bool _isConfirmingRemove;

    [JsonIgnore]
    public bool IsConfirmingRemove
    {
        get { return _isConfirmingRemove; }
        set
        {
            if (_isConfirmingRemove == value) return;
            _isConfirmingRemove = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConfirmingRemove)));
        }
    }

    // The episode behind a "More episodes" row in the podcasts view. Those rows sit below the
    // radio and are never handed to the conductor: tapping one marks it up next instead.
    [JsonIgnore]
    public RecentEpisode MoreEpisode { get; private init; }

    // The caption row that starts the "More episodes" section.
    [JsonIgnore]
    public bool IsMoreHeader { get; private init; }

    [JsonIgnore]
    public bool IsMoreRow => MoreEpisode != null || IsMoreHeader;

    [JsonIgnore]
    public bool IsMoreEpisode => MoreEpisode != null;

    // A song in the radio: the only row whose long press offers a plain "Remove".
    [JsonIgnore]
    public bool IsRadioSong => !IsPodcastSegment && !IsMoreRow;

    private string _upNextLabel;

    // "Up next", numbered when there are several, on a picked "More episodes" row.
    [JsonIgnore]
    public string UpNextLabel
    {
        get { return _upNextLabel; }
        set
        {
            if (_upNextLabel == value) return;
            _upNextLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpNextLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUpNextLabel)));
        }
    }

    [JsonIgnore]
    public bool HasUpNextLabel => !string.IsNullOrEmpty(UpNextLabel);

    public event PropertyChangedEventHandler PropertyChanged;

    public RadioItem(bool isPodcastSegment, string title, string subtitle, string imageUrl, string playUri, int positionMs, List<bool> segmentPips, bool isFinalPodcastSegment = false, string source = null, string showName = null, int durationMs = 0, bool isWholeEpisode = false)
    {
        IsPodcastSegment = isPodcastSegment;
        IsFinalPodcastSegment = isFinalPodcastSegment;
        Title = title;
        Subtitle = subtitle;
        ImageUrl = imageUrl;
        PlayUri = playUri;
        PositionMs = positionMs;
        SegmentPips = segmentPips;
        Source = source;
        ShowName = showName;
        DurationMs = durationMs;
        IsWholeEpisode = isWholeEpisode;
    }

    internal static RadioItem ForSong(string title, string artists, string imageUrl, string trackUri, string source)
    {
        return new RadioItem(false, title, artists, imageUrl, trackUri, 0, new List<bool>(), source: source);
    }

    internal static RadioItem ForMoreEpisode(RecentEpisode episode)
    {
        var subtitle = episode.ReleaseDate > DateTime.MinValue
            ? $"{episode.ShowName} · {episode.ReleaseDate:d MMM yyyy}"
            : episode.ShowName;

        return new RadioItem(false, episode.EpisodeName, subtitle, episode.ShowImageUrl, $"spotify:episode:{episode.EpisodeId}", 0,
            new List<bool>(), showName: episode.ShowName, durationMs: episode.DurationMs)
        {
            MoreEpisode = episode
        };
    }

    internal static RadioItem ForMoreHeader()
    {
        return new RadioItem(false, "More episodes", "Tap one to play it first next time you generate", null, null, 0, new List<bool>())
        {
            IsMoreHeader = true
        };
    }

    internal static RadioItem ForPodcastSegment(
        RecentEpisode episode, int segmentIndex, int segmentLengthMs, int startOffsetMs,
        int displaySegmentNumber, int displayTotalSegments, bool isFinalSegment)
    {
        var pips = Enumerable.Range(0, displayTotalSegments).Select(index => index == displaySegmentNumber).ToList();
        var subtitle = displayTotalSegments > 1
            ? $"{episode.ShowName} · Part {displaySegmentNumber + 1} of {displayTotalSegments}"
            : episode.ShowName;

        return new RadioItem(
            true,
            episode.EpisodeName,
            subtitle,
            episode.ShowImageUrl,
            $"spotify:episode:{episode.EpisodeId}",
            startOffsetMs + segmentIndex * segmentLengthMs,
            pips,
            isFinalSegment,
            showName: episode.ShowName,
            durationMs: episode.DurationMs);
    }

    /// <summary>
    /// The whole of <paramref name="firstSegment"/>'s episode as one item, for the podcasts-only
    /// view. It keeps the earliest surviving segment's start, so a part-played episode still
    /// resumes where the mix had got to, and is marked final - which is what tells the conductor
    /// to let it run to the end of the episode instead of stopping it at a segment boundary.
    /// </summary>
    internal static RadioItem ForWholeEpisode(RadioItem firstSegment)
    {
        return new RadioItem(
            true,
            firstSegment.Title,
            firstSegment.ShowName ?? WithoutPartSuffix(firstSegment.Subtitle),
            firstSegment.ImageUrl,
            firstSegment.PlayUri,
            firstSegment.PositionMs,
            new List<bool>(),
            isFinalPodcastSegment: true,
            showName: firstSegment.ShowName,
            durationMs: firstSegment.DurationMs,
            isWholeEpisode: true);
    }

    // Radios cached before ShowName existed only carry the composed "Show . Part 2 of 5" subtitle.
    private static string WithoutPartSuffix(string subtitle)
    {
        int marker = subtitle?.IndexOf(" · Part ", StringComparison.Ordinal) ?? -1;
        return marker < 0 ? subtitle : subtitle.Substring(0, marker);
    }
}
