using Android.App;
using Android.Content;
using Android.Runtime;
using SpotiSharp.Models;

namespace SpotiSharp;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override void OnCreate()
	{
		// a managed exception escaping into a Java callback (App Remote, Android views) lands here
		// rather than in AppDomain.UnhandledException
		AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
		{
			SpotiSharpBackend.DiagnosticLog.Write($"[Crash] unhandled exception in a Java callback: {e.Exception}");
			SpotiSharpBackend.DiagnosticLog.Flush(TimeSpan.FromSeconds(2));
		};

		base.OnCreate();
	}

	// Memory pressure is the usual reason Android kills a background process, which would
	// silently take the radio's in-memory state with it.
	public override void OnTrimMemory(TrimMemory level)
	{
		SpotiSharpBackend.DiagnosticLog.Write($"[System] Android asked the app to trim memory: {level}");
		base.OnTrimMemory(level);
	}

	public override void OnLowMemory()
	{
		SpotiSharpBackend.DiagnosticLog.Write("[System] Android reported low memory");
		base.OnLowMemory();
	}
}
