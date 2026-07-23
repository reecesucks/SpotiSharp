using SpotiSharpBackend;
using SpotiSharpBackend.Radio;
using static SpotiSharpBackend.Tests.Radio;

namespace SpotiSharpBackend.Tests;

public class RadioTickStateTests
{
    private const int SongMs = 180000;
    private const int EpisodeMs = 60 * 60 * 1000;

    private static RadioHarness PlayThrough(RadioHarness harness, IRadioQueueItem item, int durationMs, int stopShortMs)
    {
        harness.Tick(Playing(item, 5000, durationMs));
        harness.Wait(TimeSpan.FromMilliseconds(durationMs - stopShortMs - 5000));
        harness.Tick(Playing(item, durationMs - stopShortMs, durationMs));
        return harness;
    }

    #region the reported bug

    [Fact]
    public void Plays_the_podcast_when_the_song_before_it_ends_and_the_last_poll_was_slow()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast });

        // the last sample while playing lands 8 seconds short of the end -- further out than the
        // fixed 5s tolerance the old check used, which is what left the radio silent here forever
        PlayThrough(harness, song, SongMs, stopShortMs: 8000);

        // the song finishes and Spotify reports nothing playing at all
        harness.Wait(9).Tick(Silent);

        Assert.Equal(podcast.PlayUri, harness.ActiveUri);
        Assert.Equal(new[] { podcast.PlayUri }, harness.Started);
        Assert.False(harness.Stopped);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2000)]
    [InlineData(8000)]
    [InlineData(20000)]
    [InlineData(45000)]
    [InlineData(120000)]
    public void Plays_the_podcast_however_stale_the_last_sample_was(int stopShortMs)
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast });

        PlayThrough(harness, song, SongMs, stopShortMs);
        harness.Wait(TimeSpan.FromMilliseconds(stopShortMs + 1000)).Tick(Silent);

        Assert.Equal(podcast.PlayUri, harness.ActiveUri);
        Assert.Single(harness.Started);
    }

    [Fact]
    public void Never_gets_stuck_on_an_item_it_cannot_judge()
    {
        // a song whose duration we never learned: the end is unknowable, so the only two acceptable
        // outcomes are moving on or stopping -- what must not happen is sitting there forever
        var harness = new RadioHarness(new[] { Song("a"), Segment("ep1") });

        harness.Tick(new PlaybackSnapshot(true, "device-1", Song("a").PlayUri, 1000, 0));

        for (int i = 0; i < 200; i++) harness.Wait(3).Tick(Silent);

        Assert.True(harness.Stopped || harness.Started.Count > 0);
    }

    #endregion

    #region finishing an item

    [Fact]
    public void Advances_when_Spotify_reports_the_finished_track_paused_on_its_last_moment()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast });

        PlayThrough(harness, song, SongMs, stopShortMs: 10000);
        harness.Wait(10).Tick(Paused(song, SongMs, SongMs));

        Assert.Equal(podcast.PlayUri, harness.ActiveUri);
    }

    [Fact]
    public void Advances_at_the_end_of_a_podcast_segment_not_the_end_of_the_episode()
    {
        var segment = Segment("ep1");
        var song = Song("a");
        var harness = new RadioHarness(new[] { segment, song });

        harness.Tick(Playing(segment, 5000, EpisodeMs));
        harness.Wait(60).Tick(Playing(segment, RadioTuning.SEGMENT_LENGTH_MS - 1000, EpisodeMs));
        Assert.Equal(segment.PlayUri, harness.ActiveUri);

        harness.Wait(2).Tick(Playing(segment, RadioTuning.SEGMENT_LENGTH_MS, EpisodeMs));
        Assert.Equal(song.PlayUri, harness.ActiveUri);
    }

    [Fact]
    public void Ends_a_segment_early_when_the_episode_runs_out_before_the_boundary()
    {
        // the last segment of an episode: its 15 minute boundary is past the end of the episode
        var segment = Segment("ep1", positionMs: 45 * 60 * 1000);
        var song = Song("a");
        var harness = new RadioHarness(new[] { segment, song });

        const int shortEpisodeMs = 50 * 60 * 1000;

        harness.Tick(Playing(segment, 45 * 60 * 1000, shortEpisodeMs));
        harness.Wait(300).Tick(Playing(segment, shortEpisodeMs, shortEpisodeMs));

        Assert.Equal(song.PlayUri, harness.ActiveUri);
    }

    [Fact]
    public void Stops_when_the_last_item_in_the_queue_finishes()
    {
        var song = Song("a");
        var harness = new RadioHarness(new[] { song });

        PlayThrough(harness, song, SongMs, stopShortMs: 3000);
        harness.Wait(4).Tick(Silent);

        Assert.True(harness.Stopped);
        Assert.False(harness.State.IsActive);
        Assert.Empty(harness.Started);
    }

    #endregion

    #region a play command that doesn't take

    [Fact]
    public void Retries_a_play_command_that_failed_then_gives_up_on_the_item()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var next = Song("b");
        var harness = new RadioHarness(new[] { song, podcast, next }) { DefaultOutcome = PlaybackAttempt.Failed };

        PlayThrough(harness, song, SongMs, stopShortMs: 3000);
        harness.Wait(4).Tick(Silent);

        // it moved to the podcast and asked for it once; the request failed transiently
        Assert.Equal(new[] { podcast.PlayUri }, harness.Started);

        // nothing happens while the command still has time to land
        harness.Wait(5).Tick(Silent);
        Assert.Single(harness.Started);

        // past the grace window with still nothing playing, it asks again
        harness.Wait(9).Tick(Silent);
        Assert.Equal(2, harness.Started.Count);

        harness.Wait(13).Tick(Silent);
        Assert.Equal(3, harness.Started.Count);

        // three attempts spent: give up on this item rather than sit on it in silence
        harness.Wait(13).Tick(Silent);
        Assert.Equal(next.PlayUri, harness.ActiveUri);
    }

    [Fact]
    public void Stops_asking_once_a_retried_command_finally_takes()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast })
        {
            Outcomes = { PlaybackAttempt.Failed, PlaybackAttempt.Success }
        };

        PlayThrough(harness, song, SongMs, stopShortMs: 3000);
        harness.Wait(4).Tick(Silent);
        harness.Wait(13).Tick(Silent);
        Assert.Equal(2, harness.Started.Count);

        // it turns up on the device, so the watchdog stands down
        harness.Wait(2).Tick(Playing(podcast, 1000, EpisodeMs));
        harness.Wait(30).Tick(Playing(podcast, 31000, EpisodeMs));

        Assert.Equal(2, harness.Started.Count);
        Assert.Equal(podcast.PlayUri, harness.ActiveUri);
    }

    [Fact]
    public void Skips_an_episode_Spotify_says_is_gone()
    {
        var song = Song("a");
        var dead = Segment("removed");
        var next = Song("b");
        var harness = new RadioHarness(new[] { song, dead, next })
        {
            Outcomes = { PlaybackAttempt.Unavailable, PlaybackAttempt.Success }
        };

        PlayThrough(harness, song, SongMs, stopShortMs: 3000);
        harness.Wait(4).Tick(Silent);

        Assert.Equal(new[] { dead.PlayUri, next.PlayUri }, harness.Started);
        Assert.Equal(next.PlayUri, harness.ActiveUri);
        Assert.False(harness.Stopped);
    }

    [Fact]
    public void Stops_rather_than_churning_the_whole_queue_when_everything_reports_unavailable()
    {
        // an outage misreported as "unavailable" must not burn the queue down
        var items = new List<IRadioQueueItem> { Song("a") };
        for (int i = 0; i < 40; i++) items.Add(Segment($"ep{i}"));

        var harness = new RadioHarness(items) { DefaultOutcome = PlaybackAttempt.Unavailable };

        PlayThrough(harness, (QueueItem)items[0], SongMs, stopShortMs: 3000);
        harness.Wait(4).Tick(Silent);

        Assert.True(harness.Stopped);
        Assert.Equal(RadioTuning.MAX_UNAVAILABLE_SKIPS + 1, harness.Started.Count);
    }

    [Fact]
    public void Waits_for_a_freshly_issued_command_before_deciding_it_never_started()
    {
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { podcast, Song("a") });

        harness.Wait(3).Tick(Silent);
        harness.Wait(3).Tick(Silent);

        Assert.Empty(harness.Started);
        Assert.False(harness.Stopped);

        harness.Wait(8).Tick(Silent);
        Assert.Single(harness.Started);
    }

    #endregion

    #region someone else is driving

    [Fact]
    public void Reclaims_the_device_when_Spotify_autoplays_past_the_end_of_the_queue()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast });

        PlayThrough(harness, song, SongMs, stopShortMs: 4000);

        // our uris ran out and Spotify carried on with a recommendation of its own
        harness.Wait(5).Tick(Foreign("spotify:track:spotify-picked-this"));

        Assert.Equal(podcast.PlayUri, harness.ActiveUri);
        Assert.Equal(new[] { podcast.PlayUri }, harness.Started);
    }

    [Fact]
    public void Bows_out_when_the_user_puts_something_else_on()
    {
        var song = Song("a");
        var harness = new RadioHarness(new[] { song, Segment("ep1") });

        harness.Tick(Playing(song, 20000, SongMs));
        harness.Wait(9).Tick(Foreign("spotify:track:user-picked-this"));

        Assert.True(harness.Stopped);
        Assert.Empty(harness.Started);
    }

    [Fact]
    public void Waits_out_a_pause_without_advancing()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast });

        harness.Tick(Playing(song, 20000, SongMs));

        for (int i = 0; i < 30; i++) harness.Wait(3).Tick(Paused(song, 22000, SongMs));

        Assert.Equal(song.PlayUri, harness.ActiveUri);
        Assert.Empty(harness.Started);
        Assert.False(harness.Stopped);

        // and picks straight back up
        harness.Wait(3).Tick(Playing(song, 23000, SongMs));
        Assert.Equal(song.PlayUri, harness.ActiveUri);
    }

    [Fact]
    public void Does_not_mistake_a_long_pause_for_the_item_having_played_through()
    {
        var song = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { song, podcast });

        harness.Tick(Playing(song, 20000, SongMs));
        harness.Wait(5).Tick(Paused(song, 22000, SongMs));

        // the user wandered off and the device eventually dropped off Spotify Connect. Carrying the
        // last playing sample forward across ten minutes would put it way past the end of the song.
        harness.Wait(TimeSpan.FromMinutes(10)).Tick(Silent);

        Assert.Empty(harness.Started);
        Assert.NotEqual(podcast.PlayUri, harness.ActiveUri);

        harness.Wait(31).Tick(Silent);
        Assert.True(harness.Stopped);
    }

    [Fact]
    public void Stops_after_sustained_silence_that_is_not_the_end_of_an_item()
    {
        var song = Song("a");
        var harness = new RadioHarness(new[] { song, Segment("ep1") });

        harness.Tick(Playing(song, 20000, SongMs));
        harness.Wait(9).Tick(Silent);

        Assert.False(harness.Stopped);

        harness.Wait(TimeSpan.FromMilliseconds(RadioTuning.DEAD_AIR_TIMEOUT_MS + 1000)).Tick(Silent);

        Assert.True(harness.Stopped);
        Assert.Empty(harness.Started);
    }

    #endregion

    #region song runs

    [Fact]
    public void Follows_Spotify_through_a_song_run_without_issuing_new_commands()
    {
        var a = Song("a");
        var b = Song("b");
        var c = Song("c");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { a, b, c, podcast });

        harness.Tick(Playing(a, 1000, SongMs));
        harness.Wait(180).Tick(Playing(b, 1000, SongMs));
        Assert.Equal(b.PlayUri, harness.ActiveUri);

        harness.Wait(180).Tick(Playing(c, 1000, SongMs));
        Assert.Equal(c.PlayUri, harness.ActiveUri);

        // the whole run was queued in one command up front, so nothing more was sent
        Assert.Empty(harness.Started);
    }

    [Fact]
    public void Moves_forward_when_a_song_appears_twice_in_the_same_run()
    {
        var a = Song("a");
        var b = Song("b");
        var aAgain = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { a, b, aAgain, podcast });

        harness.Tick(Playing(a, 1000, SongMs));
        harness.Wait(180).Tick(Playing(b, 1000, SongMs));
        harness.Wait(180).Tick(Playing(aAgain, 1000, SongMs));

        Assert.Equal(2, harness.State.ActiveIndex);
    }

    [Fact]
    public void Treats_a_repeat_of_the_current_episode_as_the_same_segment_not_a_run()
    {
        // every segment of an episode shares one uri, so run matching must not apply to podcasts
        var first = Segment("ep1", 0);
        var second = Segment("ep1", RadioTuning.SEGMENT_LENGTH_MS);
        var harness = new RadioHarness(new[] { first, second });

        harness.Tick(Playing(first, 1000, EpisodeMs));
        Assert.Equal(0, harness.State.ActiveIndex);

        harness.Wait(60).Tick(Playing(first, RadioTuning.SEGMENT_LENGTH_MS, EpisodeMs));
        Assert.Equal(1, harness.State.ActiveIndex);
    }

    #endregion

    #region editing the queue while it plays

    [Fact]
    public void Resyncing_an_edited_queue_does_not_restart_what_is_playing()
    {
        var a = Song("a");
        var b = Song("b");
        var c = Song("c");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { a, b, c, podcast });

        harness.Tick(Playing(a, 20000, SongMs));
        harness.State.Resync(new[] { a, c, podcast }, 0);

        Assert.Equal(a.PlayUri, harness.ActiveUri);
        Assert.Empty(harness.Started);

        harness.Wait(30).Tick(Playing(a, 50000, SongMs));
        Assert.Empty(harness.Started);
    }

    [Fact]
    public void Keeps_going_when_the_device_plays_a_song_that_was_removed_from_the_queue()
    {
        var a = Song("a");
        var b = Song("b");
        var c = Song("c");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { a, b, c, podcast });

        PlayThrough(harness, a, SongMs, stopShortMs: 4000);

        // b is removed from the list, but Spotify was handed the whole run up front and still has it
        harness.State.Resync(new[] { a, c, podcast }, 0);
        harness.Wait(5).Tick(Foreign(b.PlayUri));

        Assert.Equal(c.PlayUri, harness.ActiveUri);
        Assert.Equal(new[] { c.PlayUri }, harness.Started);
        Assert.False(harness.Stopped);
    }

    #endregion

    #region skipping by hand

    [Fact]
    public void Skipping_by_hand_starts_the_next_item()
    {
        var a = Song("a");
        var podcast = Segment("ep1");
        var harness = new RadioHarness(new[] { a, podcast });

        harness.Tick(Playing(a, 20000, SongMs)).Skip();

        Assert.Equal(podcast.PlayUri, harness.ActiveUri);
        Assert.Equal(new[] { podcast.PlayUri }, harness.Started);
    }

    [Fact]
    public void Skipping_past_the_last_item_stops_the_radio()
    {
        var harness = new RadioHarness(new[] { Song("a") });

        harness.Tick(Playing(Song("a"), 20000, SongMs)).Skip();

        Assert.True(harness.Stopped);
        Assert.False(harness.State.IsActive);
    }

    [Fact]
    public void Does_nothing_once_stopped()
    {
        var harness = new RadioHarness(new[] { Song("a"), Segment("ep1") });
        harness.State.Stop();

        harness.Wait(60).Tick(Silent).Tick(Playing(Song("a"), 1000, SongMs)).Skip();

        Assert.Empty(harness.Started);
        Assert.False(harness.State.IsActive);
    }

    #endregion

    #region a long session

    [Fact]
    public void Plays_a_full_queue_end_to_end_under_a_poll_that_keeps_getting_slower()
    {
        // the reported shape of the bug: the longer it runs, the worse the sampling gets. Every item
        // must still be reached.
        var queue = new List<IRadioQueueItem>();
        for (int block = 0; block < 8; block++)
        {
            queue.Add(Song($"s{block}a"));
            queue.Add(Song($"s{block}b"));
            queue.Add(Song($"s{block}c"));
            queue.Add(Segment($"ep{block}"));
        }

        var harness = new RadioHarness(queue);
        var reached = new List<string> { harness.ActiveUri! };

        int pollSeconds = 2;

        for (int i = 0; i < queue.Count; i++)
        {
            var item = queue[i];
            int durationMs = item.IsPodcastSegment ? EpisodeMs : SongMs;
            int endMs = item.IsPodcastSegment
                ? item.PositionMs + RadioTuning.SEGMENT_LENGTH_MS
                : durationMs;

            // poll through the item at the current cadence
            for (int progress = 1000; progress < endMs; progress += pollSeconds * 1000)
            {
                harness.Tick(Playing(item, progress, durationMs)).Wait(pollSeconds);
            }

            // it finishes; Spotify reports nothing playing until the next command lands
            harness.Wait(pollSeconds).Tick(Silent);

            // the loop degrades as the session goes on, well past anything a fixed tolerance could
            // have been picked to cover
            pollSeconds = Math.Min(pollSeconds * 2, 120);

            if (harness.State.IsActive) reached.Add(harness.ActiveUri!);
        }

        Assert.Equal(queue.Select(item => item.PlayUri), reached);
        Assert.True(harness.Stopped);
    }

    #endregion
}
