using Android.App;
using Android.Content;
using AndroidX.Core.App;
using AndroidX.Media3.Session;
using SpotiSharp.Models;
using SpotiSharpBackend;
using AndroidOS = Android.OS;

namespace SpotiSharp.Platforms.Android;

/// <summary>Foreground service for AudioBookshelf playback. Media3's own automatic MediaStyle
/// notification (what <see cref="MediaSessionService"/> is documented to provide for free) never
/// actually appeared in testing - the notification stayed a static placeholder with no controls no
/// matter what the session reported - so this builds and maintains the notification by hand instead,
/// using <see cref="MediaStyleNotificationHelper.MediaStyle"/> with a single play/pause action wired
/// back into this service via a custom intent action.</summary>
[Service(Exported = false, ForegroundServiceType = (global::Android.Content.PM.ForegroundService)2)]
[IntentFilter(new[] { "androidx.media3.session.MediaSessionService" })]
internal class AudiobookshelfPlaybackService : MediaSessionService
{
    private const int NotificationId = 7302;
    private const string ChannelId = "spotisharp_audiobookshelf";
    private const string ActionTogglePlayPause = "com.companyname.spotisharp.audiobookshelf.TOGGLE_PLAY_PAUSE";

    public override void OnCreate()
    {
        base.OnCreate();
        DiagnosticLog.Write("[Audiobookshelf] playback service started");
    }

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo controllerInfo) => AudiobookshelfPlayer.EnsureSession();

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionTogglePlayPause)
        {
            var isPlaying = AudiobookshelfPlaybackCommands.GetIsPlaying?.Invoke() ?? false;
            if (isPlaying) AudiobookshelfPlaybackCommands.Pause?.Invoke();
            else AudiobookshelfPlaybackCommands.Play?.Invoke();
            // The player's own listener also calls UpdateNotification() on the resulting state
            // change, but that lands on the main thread asynchronously - refresh here too so the
            // notification's icon flips immediately on tap instead of lagging a frame.
            UpdateNotification();
            return StartCommandResult.Sticky;
        }

        StartForeground(NotificationId, BuildNotification(global::Android.App.Application.Context));
        return base.OnStartCommand(intent, flags, startId);
    }

    /// <summary>Rebuilds and reposts the notification so its play/pause icon reflects current
    /// playback state. Safe to call even if the service isn't running - NotificationManagerCompat.Notify
    /// is a no-op if there's nothing to update onto.</summary>
    internal static void UpdateNotification()
    {
        var context = global::Android.App.Application.Context;
        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context));
    }

    private static Notification BuildNotification(Context context)
    {
        if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Audiobookshelf", NotificationImportance.Low);
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);
        }

        var info = AudiobookshelfPlayer.CurrentInfo;
        var isPlaying = AudiobookshelfPlaybackCommands.GetIsPlaying?.Invoke() ?? false;

        var toggleIntent = new Intent(context, typeof(AudiobookshelfPlaybackService)).SetAction(ActionTogglePlayPause);
        var togglePendingIntent = AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O
            ? PendingIntent.GetForegroundService(context, 0, toggleIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)
            : PendingIntent.GetService(context, 0, toggleIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var toggleAction = new NotificationCompat.Action(
            isPlaying ? global::Android.Resource.Drawable.IcMediaPause : global::Android.Resource.Drawable.IcMediaPlay,
            isPlaying ? "Pause" : "Play",
            togglePendingIntent);

        var session = AudiobookshelfPlayer.EnsureSession();

        return new NotificationCompat.Builder(context, ChannelId)
            .SetContentTitle(info?.Title ?? "SpotiSharp Radio")
            // The service now starts as soon as Radio plays anything, not just once a podcast
            // episode has actually loaded - "Playing a podcast" would be a lie for however long
            // the run is on music before (if ever) reaching one.
            .SetContentText(info?.ShowName ?? "Ready for AudioBookshelf")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetOngoing(isPlaying)
            .AddAction(toggleAction)
            .SetStyle(new MediaStyleNotificationHelper.MediaStyle(session).SetShowActionsInCompactView(new[] { 0 }))
            .Build();
    }

    public override void OnDestroy()
    {
        DiagnosticLog.Write("[Audiobookshelf] playback service stopped");
        base.OnDestroy();
    }
}
