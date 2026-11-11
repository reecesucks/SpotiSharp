namespace SpotiSharp.Models;

/// <summary>One chapter/segment marker for an episode, as tagged on the AudioBookshelf server. A
/// "splice" tag marks a point where an ad was cut out of the audio - the file is already continuous
/// there, so <see cref="RadioAudiobookshelfModel"/> is free to pause at that timestamp, play some
/// music, and resume the podcast at that same timestamp with nothing lost.</summary>
public class AudiobookshelfChapter
{
    private const string SpliceTag = "splice";

    public double StartSeconds { get; private set; }
    public double EndSeconds { get; private set; }
    public string Tag { get; private set; }

    public bool IsSplice => string.Equals(Tag, SpliceTag, StringComparison.OrdinalIgnoreCase);

    public AudiobookshelfChapter(double startSeconds, double endSeconds, string tag)
    {
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
        Tag = tag;
    }
}
