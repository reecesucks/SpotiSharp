namespace SpotiSharp;

public class AppState
{
    private static AppState _appState;
    public static AppState Instance => _appState ??= new AppState();

    public DeviceIdiom Idiom { get; private set; } = DeviceInfo.Current.Idiom;
    public double ScreenWidth { get; private set; }
    public double ScreenHeight { get; private set; }

    public bool IsDesktop => Idiom == DeviceIdiom.Desktop;
    public bool IsMobile => Idiom == DeviceIdiom.Phone || Idiom == DeviceIdiom.Tablet;

    private AppState() {}

    public void RefreshDisplayMetrics()
    {
        var display = DeviceDisplay.Current.MainDisplayInfo;
        ScreenWidth = display.Width / display.Density;
        ScreenHeight = display.Height / display.Density;
    }
}
