using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using QinF25.Input;
using SpotiSharp.Models;
using SpotiSharp.Platforms.Android;
using SpotiSharpBackend;

namespace SpotiSharp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : KeypadActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 13+ treats posting a notification as a dangerous permission the user must grant at
        // runtime - without this, the foreground-service notifications for both Spotify radio and
        // AudioBookshelf playback (and their play/pause controls) silently never appear, even though
        // the service itself starts fine and audio keeps playing. Nothing elsewhere in the app ever
        // asked for it, so there was no way to pause from the notification shade/lock screen short of
        // killing the app outright.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(this, new[] { global::Android.Manifest.Permission.PostNotifications }, 0);
        }

        RadioBackgroundService.StartRequested = () =>
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(RadioForegroundService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        };

        RadioBackgroundService.StopRequested = () =>
        {
            var context = global::Android.App.Application.Context;
            context.StopService(new Intent(context, typeof(RadioForegroundService)));
        };

        RadioDiagnostics.SavePublicFile = PublicDownloads.Save;
        RadioAudiobookshelfDiagnostics.SavePublicFile = PublicDownloads.Save;

        AudiobookshelfDownloadBackgroundService.StartRequested = () =>
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(AudiobookshelfDownloadService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        };

        AudiobookshelfDownloadBackgroundService.StopRequested = () =>
        {
            var context = global::Android.App.Application.Context;
            context.StopService(new Intent(context, typeof(AudiobookshelfDownloadService)));
        };

        AudiobookshelfPlayer.Initialize();

        if (Authentication.HasStoredSession)
            SpotifyAppRemoteConnector.Connect(StorageHandler.ClientId, "http://127.0.0.1:5000/callback");

        Authentication.OnAuthenticate += () =>
            MainThread.BeginInvokeOnMainThread(() =>
                SpotifyAppRemoteConnector.Connect(StorageHandler.ClientId, "http://127.0.0.1:5000/callback"));
    }
}
