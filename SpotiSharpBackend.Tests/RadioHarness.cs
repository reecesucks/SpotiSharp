using SpotiSharpBackend;
using SpotiSharpBackend.Radio;

namespace SpotiSharpBackend.Tests;

public sealed record QueueItem(string PlayUri, bool IsPodcastSegment, int PositionMs) : IRadioQueueItem;

public sealed class RadioHarness
{
    private readonly RadioTickState _state;

    public RadioHarness(IReadOnlyList<IRadioQueueItem> queue, int startIndex = 0, bool alreadyIssued = true)
    {
        _state = new RadioTickState(queue, startIndex, Now, alreadyIssued);
    }

    public DateTime Now { get; private set; } = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    public RadioTickState State => _state;
    public IRadioQueueItem? Active => _state.ActiveItem;
    public string? ActiveUri => _state.ActiveItem?.PlayUri;

    public List<string> Started { get; } = new List<string>();
    public bool Stopped { get; private set; }

    public List<PlaybackAttempt> Outcomes { get; } = new List<PlaybackAttempt>();
    public PlaybackAttempt DefaultOutcome { get; set; } = PlaybackAttempt.Success;

    private int _outcomeIndex;

    public RadioHarness Wait(TimeSpan by)
    {
        Now += by;
        return this;
    }

    public RadioHarness Wait(int seconds) => Wait(TimeSpan.FromSeconds(seconds));

    public RadioHarness Tick(PlaybackSnapshot snapshot)
    {
        Apply(_state.Tick(snapshot, Now));
        return this;
    }

    public RadioHarness Skip()
    {
        Apply(_state.AdvanceManually(Now));
        return this;
    }

    private void Apply(RadioTickResult result)
    {
        while (true)
        {
            switch (result.Action)
            {
                case RadioTickAction.StartActive:
                    Started.Add(_state.ActiveItem!.PlayUri);
                    var outcome = _outcomeIndex < Outcomes.Count ? Outcomes[_outcomeIndex++] : DefaultOutcome;
                    result = _state.ReportStartOutcome(outcome, Now);
                    continue;

                case RadioTickAction.Stop:
                    Stopped = true;
                    return;

                default:
                    return;
            }
        }
    }
}

public static class Radio
{
    public static QueueItem Song(string id) => new QueueItem($"spotify:track:{id}", false, 0);

    public static QueueItem Segment(string id, int positionMs = 0) => new QueueItem($"spotify:episode:{id}", true, positionMs);

    public static PlaybackSnapshot Playing(IRadioQueueItem item, int progressMs, int durationMs) =>
        new PlaybackSnapshot(true, "device-1", item.PlayUri, progressMs, durationMs);

    public static PlaybackSnapshot Paused(IRadioQueueItem item, int progressMs, int durationMs) =>
        new PlaybackSnapshot(false, "device-1", item.PlayUri, progressMs, durationMs);

    public static PlaybackSnapshot Foreign(string uri) => new PlaybackSnapshot(true, "device-1", uri, 1000, 200000);

    public static PlaybackSnapshot Silent => PlaybackSnapshot.Empty;
}
