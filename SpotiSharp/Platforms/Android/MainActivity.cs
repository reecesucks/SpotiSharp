using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using SpotiSharp.Models;
using SpotiSharp.Platforms.Android;

namespace SpotiSharp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

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
    }
}
