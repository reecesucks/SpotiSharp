namespace SpotiSharpBackend.Radio;

public interface IRadioQueueItem
{
    string PlayUri { get; }
    bool IsPodcastSegment { get; }

    int PositionMs { get; }
}
