using Android.App;
using Android.Content;
using Android.OS;
using AndroidOS = Android.OS;

namespace SpotiSharp.Platforms.Android;

/// <summary>A do-nothing foreground service whose only job is to keep the app process alive (and
/// exempt from Doze/background kills) while an AudioBookshelf episode download is running - the same
/// role <see cref="RadioForegroundService"/> plays for Spotify radio. (ForegroundService)1 ==
/// FOREGROUND_SERVICE_TYPE_DATA_SYNC, which fits a one-off network transfer better than media
/// playback's type (and, unlike media playback, needs the explicit FOREGROUND_SERVICE_DATA_SYNC
/// permission, declared in the manifest).</summary>
[Service(Exported = false, ForegroundServiceType = (global::Android.Content.PM.ForegroundService)1)]
public class AudiobookshelfDownloadService : Service
{
    private const int NotificationId = 7303;
    private const string ChannelId = "spotisharp_audiobookshelf_download";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        SpotiSharpBackend.DiagnosticLog.Write(intent == null
            ? "[Audiobookshelf] download foreground service restarted by Android (the process had been killed)"
            : "[Audiobookshelf] download foreground service started");
        StartForeground(NotificationId, BuildNotification());
        return StartCommandResult.Sticky;
    }

    private Notification BuildNotification()
    {
        var context = global::Android.App.Application.Context;

        if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Downloads", NotificationImportance.Low);
            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);

            return new Notification.Builder(context, ChannelId)
                .SetContentTitle("SpotiSharp")
                .SetContentText("Downloading episode")
                .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)
                .SetOngoing(true)
                .Build();
        }

#pragma warning disable CS0618 // pre-channel builder for API < 26
        return new Notification.Builder(context)
            .SetContentTitle("SpotiSharp")
            .SetContentText("Downloading episode")
            .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)
            .SetOngoing(true)
            .Build();
#pragma warning restore CS0618
    }

    public override void OnDestroy()
    {
        SpotiSharpBackend.DiagnosticLog.Write("[Audiobookshelf] download foreground service stopped");
        base.OnDestroy();
    }
}
