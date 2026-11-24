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
    public void A_run_starting_against_an_already_cluttered_queue_flushes_it_first()
    {
        // Reece's ask (2026-11-13): rather than tolerating whatever's stray in the real queue and
        // hoping it plays out of turn harmlessly, clear a path before every play. "a" isn't already
        // queued, so both stale leftovers get skipped through before it starts, and the run queues
        // fresh behind it.
        var session = new RadioSession(new IRadioQueueItem[] { Song("a"), Song("b"), Song("c"), Segment("ep1") });

        session.Spotify.Enqueue("spotify:track:stale1");
        session.Spotify.Enqueue("spotify:track:b");
        session.Mirror.NoteQueued("spotify:track:stale1");
        session.Mirror.NoteQueued("spotify:track:b");

        session.Start();

        Assert.Equal(new[] { "spotify:track:b", "spotify:track:c" }, session.Spotify.Queue);
    }

    [Fact]
    public void Picking_a_song_already_queued_further_down_skips_past_everything_ahead_of_it_when_the_rest_matches()
    {
        // A row tapped directly further down the radio can already be sitting third or so in
        // Spotify's real queue, with the rest of the run genuinely already behind it - skip past
        // what's ahead rather than flush the lot and replay.
        var mirror = new SpotifyQueueMirror();
        mirror.NoteQueued("spotify:track:b");
        mirror.NoteQueued("spotify:track:c");
        mirror.NoteQueued("spotify:track:d");

        var plan = mirror.PlanPlay("spotify:track:c", new[] { "spotify:track:d", "spotify:track:e" });

        Assert.Equal(QueueStart.SkipTo, plan.Start);
        Assert.Equal(2, plan.PreSkipCount);
        Assert.Equal(new[] { "spotify:track:e" }, plan.ToQueue);
    }

    [Fact]
    public void A_mismatch_behind_the_target_flushes_instead_of_being_left_stray()
    {
        // Reece's ask (2026-11-14): a skip can only ever land on the next thing queued, never also
        // clear what's behind it without abandoning what it just landed on - so the moment what's
        // behind the target isn't a provable match for the run, nothing is trusted and everything
        // tracked is flushed instead of leaving the mismatch to sit there and grow the queue.
        var mirror = new SpotifyQueueMirror();
        mirror.NoteQueued("spotify:track:b");
        mirror.NoteQueued("spotify:track:c");
        mirror.NoteQueued("spotify:track:d"); // not part of the run below

        var plan = mirror.PlanPlay("spotify:track:c", new[] { "spotify:track:e" });

        Assert.Equal(QueueStart.Play, plan.Start);
        Assert.Equal(3, plan.PreSkipCount);
        Assert.Equal(new[] { "spotify:track:e" }, plan.ToQueue);
    }

    [Fact]
    public void Picking_a_song_not_in_the_real_queue_flushes_everything_before_playing_it()
    {
        var mirror = new SpotifyQueueMirror();
        mirror.NoteQueued("spotify:track:stale1");
        mirror.NoteQueued("spotify:track:stale2");

        var plan = mirror.PlanPlay("spotify:track:x", new[] { "spotify:track:y" });

        Assert.Equal(QueueStart.Play, plan.Start);
        Assert.Equal(2, plan.PreSkipCount);
        Assert.Equal(new[] { "spotify:track:y" }, plan.ToQueue);
    }

    [Fact]
    public void The_very_first_play_of_a_session_flushes_generously_even_with_nothing_tracked_yet()
    {
        // App Remote has no way to read Spotify's real queue, so a brand new mirror (app start, or
        // right after installing a build) can't tell an empty queue from one still holding a long
        // backlog from earlier testing - Reece hit exactly this: picking a song in Radio left a pile
        // of unrelated leftovers still sitting behind it in the real queue. Trusting an empty
        // _notYetPlayed here would mean never flushing them.
        var mirror = new SpotifyQueueMirror();

        var plan = mirror.PlanPlay("spotify:track:a", new[] { "spotify:track:b" });

        Assert.Equal(QueueStart.Play, plan.Start);
        Assert.Equal(RadioTuning.MAX_QUEUE_FLUSH_SKIPS, plan.PreSkipCount);
    }

    [Fact]
    public void A_later_play_in_the_same_session_trusts_a_genuinely_empty_queue()
    {
        var mirror = new SpotifyQueueMirror();
        mirror.PlanPlay("spotify:track:a", null); // establishes a known baseline

        var plan = mirror.PlanPlay("spotify:track:b", new[] { "spotify:track:c" });

        Assert.Equal(0, plan.PreSkipCount);
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
