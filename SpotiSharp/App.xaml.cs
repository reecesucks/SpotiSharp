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

		return window;
	}
}
