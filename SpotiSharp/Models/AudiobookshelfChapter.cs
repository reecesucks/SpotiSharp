namespace SpotiSharp.Models;

/// <summary>One chapter/segment marker for an episode, as tagged on the AudioBookshelf server. A
/// chapter titled "Splice N" marks a point where an ad was cut out of the audio - the file is already
/// continuous there, so <see cref="RadioAudiobookshelfModel"/> is free to pause at that timestamp,
/// play some music, and resume the podcast at that same timestamp with nothing lost.</summary>
public class AudiobookshelfChapter
{
    private const string SpliceTagPrefix = "splice";

    public double StartSeconds { get; private set; }
    public double EndSeconds { get; private set; }
    public string Tag { get; private set; }

    // Titles seen from a real tagged episode are "Start", "Splice 1", "Splice 2", ... - a prefix
    // match, not an exact one, so the numbered splice chapters all count.
    public bool IsSplice => Tag != null && Tag.StartsWith(SpliceTagPrefix, StringComparison.OrdinalIgnoreCase);

    public AudiobookshelfChapter(double startSeconds, double endSeconds, string tag)
    {
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
        Tag = tag;
    }
}
