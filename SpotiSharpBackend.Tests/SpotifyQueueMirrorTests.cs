using SpotiSharpBackend.Radio;
using static SpotiSharpBackend.Tests.Radio;

namespace SpotiSharpBackend.Tests;

/// <summary>
/// What the radio leaves behind in Spotify's queue. Captured from radio-bug-20261009-135650.txt
/// (08:54-09:05), the one report that catches the queue going bad from a clean start.
/// </summary>
public class SpotifyQueueMirrorTests
{
    [Fact]
    public void A_clean_run_queues_only_the_songs_behind_the_one_it_plays()
    {
        // 10-09 08:54:39 [Queue] playing <a> then queueing 3; Spotify should still hold 0 from before: []
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Song("d"), Segment("ep1") });

        session.Start();

        Assert.Equal(new[] { "spotify:track:b", "spotify:track:c", "spotify:track:d" }, session.Spotify.Queue);
    }

    [Fact]
    public void A_run_that_plays_straight_through_drains_Spotifys_queue()
    {
        // The healthy session (10-09 06:35-06:50): Play + queue 3, three "Spotify moved ahead in the
        // run" advances, queue back to []. Nothing is re-queued because the radio never re-plays.
        var podcast = Segment("ep1");
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Song("d"), podcast });

        session.Start();
        session.PlayCurrentToEnd().PlayCurrentToEnd().PlayCurrentToEnd().PlayCurrentToEnd();

        Assert.Equal(podcast.PlayUri, session.ActiveUri);
        Assert.Empty(session.Spotify.Queue);
    }

    [Fact]
    public void The_song_the_radio_plays_is_not_left_sitting_in_Spotifys_queue()
    {
        // 10-09 08:56:05 [Queue] playing <b> then queueing 2; Spotify should still hold 3 from before: [<b>, <c>, <d>]
        //
        // Next was pressed during <a>, so the radio played <b> -- but <b> was already queued behind
        // <a> and play() doesn't clear the queue, so a copy of <b> is left at the head of it. When
        // <b> ends Spotify plays that copy, and the song restarts ("played song, then restarted
        // song, then played a different song", radio-bug-20261016).
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Song("d"), Segment("ep1") });

        session.Start();
        session.PressNext();

        Assert.DoesNotContain(session.Spotify.NowPlaying, session.Spotify.Queue);
    }

    [Fact]
    public void Pressing_next_mid_run_does_not_duplicate_the_rest_of_the_run()
    {
        // Same moment: the queue should still just be the songs after <b>, not [<b>,<c>,<d>,<c>,<d>].
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Song("d"), Segment("ep1") });

        session.Start();
        session.PressNext();

        Assert.Equal(new[] { "spotify:track:c", "spotify:track:d" }, session.Spotify.Queue);
    }

    [Fact]
    public void A_run_interrupted_by_next_still_leaves_nothing_queued_when_the_podcast_takes_over()
    {
        // The whole captured cascade. By 09:04:51 the podcast was up and Spotify still held 6 songs
        // from the finished run; they were never played and never cleared, and every later run piled
        // more on top -- 28 by 10-13 07:07, 46 by 10-13 11:04.
        var podcast = Segment("ep1");
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Song("d"), podcast });

        session.Start();
        session.PressNext();
        session.PlayCurrentToEnd().PlayCurrentToEnd().PlayCurrentToEnd();

        Assert.Equal(podcast.PlayUri, session.ActiveUri);
        Assert.Empty(session.Spotify.Queue);
    }

    [Fact]
    public void Several_runs_with_next_presses_never_ratchet_the_queue_past_one_run()
    {
        // The reported symptom: "it should only ever have 3 or 4 ish but sometimes it is over 10".
        // Across the captured sessions the leftovers only ever climbed -- 6, then 28, then 46 --
        // because each run queued its tail again on top of everything the last one left behind.
        // Runs of five songs with a podcast between them, long enough that the session can't run
        // off the end of the radio and quietly stop part way through.
        const int runLength = 5;
        var radio = new List<IRadioQueueItem>();
        for (int run = 0; run < 12; run++)
        {
            for (int song = 0; song < runLength; song++) radio.Add(Song($"r{run}s{song}"));
            radio.Add(Segment($"ep{run}"));
        }

        var session = new RadioSession(radio);
        session.Start();

        // Play through, pressing Next every third song the way Reece does.
        for (int step = 0; step < 20; step++)
        {
            if (step % 3 == 2) session.PressNext();
            else session.PlayCurrentToEnd();
        }

        // If the radio stopped, the rest of the run never happened and the bound below proves nothing.
        Assert.False(session.Stopped);
        Assert.True(session.Spotify.PeakQueueLength <= runLength - 1,
            $"Spotify's queue peaked at {session.Spotify.PeakQueueLength}, over the {runLength - 1} songs behind a run's first");
    }

    [Fact]
    public void A_run_starting_against_an_already_cluttered_queue_adds_no_duplicates()
    {
        // Recovering a phone that's already in the bad state: Spotify is holding songs from runs
        // gone by. They'll play out of turn and the radio deals with that, but the new run must not
        // pile a second copy of itself on top.
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Segment("ep1") });

        session.Spotify.Enqueue("spotify:track:stale1");
        session.Spotify.Enqueue("spotify:track:b");
        session.Mirror.NoteQueued("spotify:track:stale1");
        session.Mirror.NoteQueued("spotify:track:b");

        session.Start();

        // b is already in there and stays where it is; only c is missing from the run.
        Assert.Equal(1, session.Spotify.Queue.Count(uri => uri == "spotify:track:b"));
        Assert.Equal(1, session.Spotify.Queue.Count(uri => uri == "spotify:track:c"));
    }

    [Fact]
    public void No_song_is_heard_twice_across_a_run_interrupted_by_next()
    {
        // The listener-visible half of the same bug: "played song, then restarted song, then played
        // a different song". Each song in the run should reach the speaker once.
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Song("d"), Segment("ep1") });

        session.Start();
        session.PressNext();
        session.PlayCurrentToEnd().PlayCurrentToEnd().PlayCurrentToEnd();

        Assert.Equal(session.Spotify.Heard.Distinct().Count(), session.Spotify.Heard.Count);
    }
}
