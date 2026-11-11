using Android.App;
using Android.Content;
using AndroidX.Media3.Session;
using SpotiSharpBackend;
using AndroidOS = Android.OS;

namespace SpotiSharp.Platforms.Android;

/// <summary>
/// Wraps the shared <see cref="AudiobookshelfPlayer"/> instance in a Media3 <see cref="MediaSession"/>
/// so AudioBookshelf playback gets lock-screen/notification controls and survives the app leaving the
/// foreground — the same role <see cref="RadioForegroundService"/> plays for Spotify, but Media3 builds
/// the notification and media-button handling for us instead of it being a do-nothing keep-alive.
/// Doesn't own the player: <see cref="AudiobookshelfPlayer"/> is the single source of truth for it, so
/// the player keeps working even if this service is killed and restarted, or never started at all
/// (e.g. before any episode has been played).
/// </summary>
[Service(Exported = false, ForegroundServiceType = (global::Android.Content.PM.ForegroundService)2)]
[IntentFilter(new[] { "androidx.media3.session.MediaSessionService" })]
internal class AudiobookshelfPlaybackService : MediaSessionService
{
    private const int NotificationId = 7302;
    private const string ChannelId = "spotisharp_audiobookshelf";

    public override void OnCreate()
    {
        base.OnCreate();
        DiagnosticLog.Write("[Audiobookshelf] playback service started");
    }

    // The session is owned by AudiobookshelfPlayer, not this service - it's created up front
    // (see AudiobookshelfPlayer.EnsureSession) so it already exists by the time this is asked for.
    public override MediaSession? OnGetSession(MediaSession.ControllerInfo controllerInfo) => AudiobookshelfPlayer.EnsureSession();

    // Android requires startForeground() within seconds of a startForegroundService() call, or it
    // kills the process - relying on Media3's own notification manager to call it (on some delay tied
    // to buffering/playback state) was too late/unreliable. Calling it ourselves here, immediately,
    // with a placeholder notification guarantees compliance; Media3 then freely replaces/updates this
    // notification (title/art/controls) once the session is ready, which is just a normal re-notify.
    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        return base.OnStartCommand(intent, flags, startId);
    }

    private Notification BuildNotification()
    {
        var context = global::Android.App.Application.Context;

        if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Audiobookshelf", NotificationImportance.Low);
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);

            return new Notification.Builder(context, ChannelId)
                .SetContentTitle("SpotiSharp")
                .SetContentText("Playing a podcast")
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
                .SetOngoing(true)
                .Build();
        }

#pragma warning disable CS0618 // pre-channel builder for API < 26
        return new Notification.Builder(context)
            .SetContentTitle("SpotiSharp")
            .SetContentText("Playing a podcast")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetOngoing(true)
            .Build();
#pragma warning restore CS0618
    }

    public override void OnDestroy()
    {
        DiagnosticLog.Write("[Audiobookshelf] playback service stopped");
        base.OnDestroy();
    }
}
