using SpotiSharpBackend.Radio;

namespace SpotiSharpBackend.Tests;

/// <summary>
/// Stands in for Spotify as App Remote presents it. The one thing that matters here: play() starts a
/// uri immediately and leaves the queue exactly as it was, and the only way anything leaves the queue
/// is by being played.
/// </summary>
public sealed class FakeSpotify
{
    public const int SongMs = 180000;
    public const int EpisodeMs = 60 * 60 * 1000;

    public List<string> Queue { get; } = new List<string>();

    public string? NowPlaying { get; private set; }
    public int ProgressMs { get; private set; }
    public bool IsPlaying { get; private set; }

    /// <summary>Every uri play() was called with, in order.</summary>
    public List<string> Plays { get; } = new List<string>();

    /// <summary>Every uri that started from the top, in order -- what the listener actually hears,
    /// whether the radio played it or Spotify took it off its own queue.</summary>
    public List<string> Heard { get; } = new List<string>();

    public int DurationMs => NowPlaying == null ? 0 : NowPlaying.Contains(":episode:") ? EpisodeMs : SongMs;

    public void Play(string uri)
    {
        Plays.Add(uri);
        Heard.Add(uri);
        NowPlaying = uri;
        ProgressMs = 0;
        IsPlaying = true;
    }

    /// <summary>The most the queue has ever held, for catching a queue that ratchets up over a session.</summary>
    public int PeakQueueLength { get; private set; }

    public void Enqueue(string uri)
    {
        Queue.Add(uri);
        PeakQueueLength = Math.Max(PeakQueueLength, Queue.Count);
    }

    public void SeekTo(int ms) => ProgressMs = ms;

    public void PlayTo(int ms) => ProgressMs = ms;

    /// <summary>Spotify's own skip-forward: same thing as the track running out.</summary>
    public void SkipNext() => TrackEnded();

    /// <summary>The current track runs out, so Spotify takes the next thing off its own queue.</summary>
    public void TrackEnded()
    {
        if (Queue.Count == 0)
        {
            NowPlaying = null;
            IsPlaying = false;
            ProgressMs = 0;
            return;
        }

        NowPlaying = Queue[0];
        Heard.Add(Queue[0]);
        Queue.RemoveAt(0);
        ProgressMs = 0;
        IsPlaying = true;
    }

    public PlaybackSnapshot Snapshot => new PlaybackSnapshot(IsPlaying, "device-1", NowPlaying, ProgressMs, DurationMs);
}

/// <summary>
/// The radio driving a <see cref="FakeSpotify"/> end to end: <see cref="RadioTickState"/> deciding,
/// <see cref="SpotifyQueueMirror"/> planning what to play and queue, and Spotify's queue reacting the
/// way it really does. <see cref="RadioHarness"/> covers the tick decisions on their own; this is for
/// what the radio leaves behind in Spotify's queue.
/// </summary>
public sealed class RadioSession
{
    private readonly IReadOnlyList<IRadioQueueItem> _radio;
    private readonly int _startIndex;
    private RadioTickState _state = null!;

    public RadioSession(IReadOnlyList<IRadioQueueItem> radio, int startIndex = 0)
    {
        _radio = radio;
        _startIndex = startIndex;
    }

    public FakeSpotify Spotify { get; } = new FakeSpotify();
    public SpotifyQueueMirror Mirror { get; } = new SpotifyQueueMirror();

    public DateTime Now { get; private set; } = new DateTime(2026, 10, 9, 8, 54, 39, DateTimeKind.Utc);

    public bool Stopped { get; private set; }
    public List<string> Skipped { get; } = new List<string>();

    public string? ActiveUri => _state.ActiveItem?.PlayUri;

    public RadioSession Wait(int seconds)
    {
        Now += TimeSpan.FromSeconds(seconds);
        return this;
    }

    /// <summary>Generate/row tap: the item is issued first, then the conductor picks it up (alreadyIssued).</summary>
    public RadioSession Start()
    {
        _state = new RadioTickState(_radio, _startIndex, Now, alreadyIssued: true);
        IssuePlayback(_radio[_startIndex]);
        return this;
    }

    public RadioSession PressNext()
    {
        Apply(_state.AdvanceManually(Now));
        return this;
    }

    /// <summary>One playback sample reaching the radio, the way the App Remote connector delivers it.</summary>
    public RadioSession Sample()
    {
        Mirror.NoteNowPlaying(Spotify.NowPlaying);

        switch (Mirror.CheckRemovedSong(Spotify.NowPlaying, Now))
        {
            case RemovedSongCheck.SkipNow:
                Spotify.TrackEnded();
                return this;
            case RemovedSongCheck.SkipInFlight:
                return this;
        }

        Apply(_state.Tick(Spotify.Snapshot, Now));
        return this;
    }

    /// <summary>Lets whatever Spotify is on run to its end, sampling as it goes, then lets Spotify move on.</summary>
    public RadioSession PlayCurrentToEnd()
    {
        int durationMs = Spotify.DurationMs;
        if (durationMs <= 0) return this;

        Spotify.PlayTo(5000);
        Wait(5).Sample();

        Spotify.PlayTo(durationMs - 1000);
        Wait(Math.Max(1, (durationMs - 6000) / 1000)).Sample();

        Spotify.TrackEnded();
        Wait(2).Sample();
        return this;
    }

    private void Apply(RadioTickResult result)
    {
        while (true)
        {
            switch (result.Action)
            {
                case RadioTickAction.StartActive:
                    IssuePlayback(_state.ActiveItem!);
                    result = _state.ReportStartOutcome(PlaybackAttempt.Success, Now);
                    continue;

                case RadioTickAction.SkipUnexpected:
                    Skipped.Add(_state.SkippingUri!);
                    Spotify.TrackEnded();
                    return;

                case RadioTickAction.Stop:
                    Stopped = true;
                    return;

                default:
                    return;
            }
        }
    }

    // RadioConductor.IssuePlayback + AppRemotePlayback.TryPlayItemAsync, without the network.
    private void IssuePlayback(IRadioQueueItem item)
    {
        if (item.IsPodcastSegment)
        {
            Execute(Mirror.PlanPlay(item.PlayUri, null));

            var rewoundMs = Math.Max(0, item.PositionMs - RadioTuning.RESUME_REWIND_MS);
            if (rewoundMs > 0) Spotify.SeekTo(rewoundMs);
            return;
        }

        var run = SongRunFromActive();
        if (run.Count == 0) return;
        Execute(Mirror.PlanPlay(run[0], run.Skip(1)));
    }

    private void Execute(QueuePlan plan)
    {
        if (plan.Start == QueueStart.SkipTo) Spotify.SkipNext();
        else Spotify.Play(plan.PlayUri);

        Mirror.NotePlayAccepted(plan);

        foreach (var uri in plan.ToQueue)
        {
            Spotify.Enqueue(uri);
            Mirror.NoteQueued(uri);
        }
    }

    private List<string> SongRunFromActive()
    {
        var run = new List<string>();
        foreach (var queued in _state.RemainingItems)
        {
            if (queued.IsPodcastSegment) break;
            run.Add(queued.PlayUri);
        }
        return run;
    }
}
