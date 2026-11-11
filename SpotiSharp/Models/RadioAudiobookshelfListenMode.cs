namespace SpotiSharp.Models;

/// <summary>
/// Which of the generated radio the listener wants to hear right now. A view over the
/// radio, not a way of generating one: the full mix is always what was generated, and
/// switching modes only changes what the list shows and what the conductor is handed.
/// </summary>
public enum RadioAudiobookshelfListenMode
{
    // songs and podcast segments as they were generated
    Mix = 0,

    // songs only, podcast segments hidden
    Music = 1,

    // podcasts only, each episode as a single item rather than its segments
    Podcasts = 2
}
