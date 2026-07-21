using Android.App;
using Android.Content;
using Android.OS;
using AndroidOS = Android.OS;

namespace SpotiSharp.Platforms.Android;

// A do-nothing foreground service whose only job is to keep the app process alive
// (and exempt from Doze) while the radio is playing, so the conductor's 2s poll
// keeps running with the screen off. The playback itself happens in Spotify.
// (ForegroundService)2 == FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK; using the int avoids
// enum-member naming differences across android api bindings. media playback has no
// daily runtime cap (unlike data sync), which suits long radio sessions
[Service(Exported = false, ForegroundServiceType = (global::Android.Content.PM.ForegroundService)2)]
public class RadioForegroundService : Service
{
    private const int NotificationId = 7301;
    private const string ChannelId = "spotisharp_radio";

    public override IBinder OnBind(Intent intent) => null;

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        return StartCommandResult.Sticky;
    }

    private Notification BuildNotification()
    {
        var context = global::Android.App.Application.Context;

        if (AndroidOS.Build.VERSION.SdkInt >= AndroidOS.BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Radio", NotificationImportance.Low);
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            manager?.CreateNotificationChannel(channel);

            return new Notification.Builder(context, ChannelId)
                .SetContentTitle("SpotiSharp radio")
                .SetContentText("Keeping your radio playing")
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
                .SetOngoing(true)
                .Build();
        }

#pragma warning disable CS0618 // pre-channel builder for API < 26
        return new Notification.Builder(context)
            .SetContentTitle("SpotiSharp radio")
            .SetContentText("Keeping your radio playing")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetOngoing(true)
            .Build();
#pragma warning restore CS0618
    }
}
