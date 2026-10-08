namespace SpotiSharp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		Themes.ThemeService.Initialize();

		MainPage = new AppShell();
	}

	protected override Window CreateWindow(IActivationState activationState)
	{
		var window = base.CreateWindow(activationState);

		window.Created += (_, _) => AppState.Instance.RefreshDisplayMetrics();
		window.Stopped += (_, _) => SpotiSharpBackend.DiagnosticLog.Write("[App] went to the background");
		window.Resumed += (_, _) => SpotiSharpBackend.DiagnosticLog.Write("[App] back in the foreground");
		window.Destroying += (_, _) => SpotiSharpBackend.DiagnosticLog.Write("[App] window destroyed");

		return window;
	}
}
