using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using SpotiSharp.Models;
using SpotiSharpBackend;
using AndroidOS = Android.OS;

namespace SpotiSharp.Platforms.Android;

/// <summary>
/// Media3/ExoPlayer wrapper for streaming AudioBookshelf episodes. Entirely separate from
/// <see cref="SpotifyAppRemoteConnector"/> — no shared state, no shared queue, nothing Spotify.
/// Wires itself into <see cref="AudiobookshelfPlaybackCommands"/> the same way the Spotify connector
/// wires itself into <see cref="PlaybackCommands"/>. The player itself is a static singleton shared
/// with <see cref="AudiobookshelfPlaybackService"/>, which wraps it in a MediaSession for lock-screen
/// controls and background playback — see that class for why the player isn't owned by the service.
/// </summary>
internal static class AudiobookshelfPlayer
{
    private static IExoPlayer? _player;
    private static MediaSession? _session;
    private static bool _serviceStarted;

    internal static void Initialize()
    {
        AudiobookshelfPlaybackCommands.Load = Load;
        AudiobookshelfPlaybackCommands.Play = Play;
        AudiobookshelfPlaybackCommands.Pause = Pause;
        AudiobookshelfPlaybackCommands.SeekTo = SeekTo;
        // ExoPlayer enforces main-thread-only access to these properties and throws
        // Java.Lang.IllegalStateException otherwise - PlayerBarViewModel's shared polling loop
        // (UiLoop) that calls these getters runs on a background thread, so each one has to hop to
        // the main thread and block that background thread for the (fast) round trip, rather than
        // touching _player directly.
        AudiobookshelfPlaybackCommands.GetPositionMs = () => InvokeOnMainThread(() => (int)(_player?.CurrentPosition ?? 0));
        AudiobookshelfPlaybackCommands.GetDurationMs = () => InvokeOnMainThread(() => (int)Math.Max(_player?.Duration ?? 0, 0));
        AudiobookshelfPlaybackCommands.GetIsPlaying = () => InvokeOnMainThread(() => _player?.IsPlaying ?? false);

        // Deliberately NOT starting the foreground service here: Android 12+ refuses a foreground
        // service start with no active, user-initiated media session behind it, and starting it this
        // early - before anything is playing - left it with nothing to show in a notification, so it
        // never called startForeground() in time and got killed by the OS. The service is started from
        // Load() instead, once playback is actually about to begin. What IS created eagerly here is the
        // MediaSession itself (see EnsureSession), so it already exists by the time the service's
        // OnGetSession asks for it - avoiding the earlier lock-screen-controls race.
        EnsureSession();
    }

    /// <summary>Lazily creates the single shared player. Internal so <see cref="AudiobookshelfPlaybackService"/>
    /// can wrap the same instance in a MediaSession rather than each owning a separate player.</summary>
    internal static IExoPlayer EnsurePlayer()
    {
        if (_player != null) return _player;

        var context = global::Android.App.Application.Context;
        _player = new ExoPlayerBuilder(context).Build();
        return _player;
    }

    /// <summary>Lazily creates the single shared MediaSession wrapping <see cref="EnsurePlayer"/>'s player.
    /// Created here rather than in the service's OnCreate so it exists before playback starts.</summary>
    internal static MediaSession EnsureSession()
    {
        if (_session != null) return _session;

        var context = global::Android.App.Application.Context;
        _session = new MediaSession.Builder(context, EnsurePlayer()).Build();
        return _session;
    }

    private static void EnsureServiceStarted()
    {
        if (_serviceStarted) return;
        _serviceStarted = true;

        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(AudiobookshelfPlaybackService));
        if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    private static void Load(string streamUrl, IReadOnlyDictionary<string, string> headers, AudiobookshelfNowPlayingInfo info, int startPositionMs)
    {
        void LoadNow()
        {
            try
            {
                var player = EnsurePlayer();
                EnsureServiceStarted();

                var isLocalFile = !streamUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                   !streamUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                var headerMap = new Dictionary<string, string>(headers);
                var httpDataSourceFactory = new DefaultHttpDataSource.Factory()
                    .SetDefaultRequestProperties(headerMap);
                // Wraps the HTTP factory so a downloaded episode's local file:// path is served by
                // FileDataSource instead, without a separate code path for the two sources.
                var dataSourceFactory = new DefaultDataSourceFactory(global::Android.App.Application.Context, httpDataSourceFactory);

                var mediaUri = isLocalFile
                    ? global::Android.Net.Uri.FromFile(new Java.IO.File(streamUrl))
                    : global::Android.Net.Uri.Parse(streamUrl);

                var metadata = new MediaMetadata.Builder()
                    .SetTitle(info.Title)
                    .SetArtist(info.ShowName)
                    .SetArtworkUri(global::Android.Net.Uri.Parse(info.ArtworkUrl))
                    .Build();

                var mediaItem = new MediaItem.Builder()
                    .SetUri(mediaUri)
                    .SetMediaMetadata(metadata)
                    .Build();

                player.SetMediaSource(new AndroidX.Media3.ExoPlayer.Source.ProgressiveMediaSource.Factory(dataSourceFactory)
                    .CreateMediaSource(mediaItem));
                player.Prepare();
                if (startPositionMs > 0) player.SeekTo(startPositionMs);
                player.Play();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Audiobookshelf] load failed: {ex.Message}");
            }
        }

        if (MainThread.IsMainThread) LoadNow();
        else MainThread.BeginInvokeOnMainThread(LoadNow);
    }

    private static void Play()
    {
        if (MainThread.IsMainThread) _player?.Play();
        else MainThread.BeginInvokeOnMainThread(() => _player?.Play());
    }

    private static void Pause()
    {
        if (MainThread.IsMainThread) _player?.Pause();
        else MainThread.BeginInvokeOnMainThread(() => _player?.Pause());
    }

    private static void SeekTo(int positionMs)
    {
        if (MainThread.IsMainThread) _player?.SeekTo(positionMs);
        else MainThread.BeginInvokeOnMainThread(() => _player?.SeekTo(positionMs));
    }

    private static T InvokeOnMainThread<T>(Func<T> func)
    {
        if (MainThread.IsMainThread) return func();
        return MainThread.InvokeOnMainThreadAsync(func).GetAwaiter().GetResult();
    }
}
