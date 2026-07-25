using System.Diagnostics;

namespace SpotiSharp;

public delegate void RefreshUi();

public class UiLoop
{
    private static UiLoop _uiLoop;
    public static UiLoop Instance => _uiLoop ??= new UiLoop();

    private const int UI_REFRESH_INTERVAL_IN_MILLI = 2000;

    public event RefreshUi OnRefreshUi;

    private UiLoop() {}

    public void Loop()
    {
        while (true)
        {
              var handlers = OnRefreshUi;
            if (handlers != null)
            {
                foreach (RefreshUi handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[UiLoop] {handler.Method.DeclaringType?.Name}.{handler.Method.Name} threw: {ex}");
                    }
                }
            }
            Thread.Sleep(UI_REFRESH_INTERVAL_IN_MILLI);
        }
    }
}