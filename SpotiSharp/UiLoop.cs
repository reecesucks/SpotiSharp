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
                        LogFailure(handler, ex);
                    }
                }
            }
            Thread.Sleep(UI_REFRESH_INTERVAL_IN_MILLI);
        }
    }

    private readonly Dictionary<string, DateTime> _lastFailureLoggedUtc = new Dictionary<string, DateTime>();

    // Debug.WriteLine is invisible in a Release build, so failures also go to the diagnostics
    // log, at most once a minute per handler in case one throws on every pass.
    private void LogFailure(RefreshUi handler, Exception ex)
    {
        var name = $"{handler.Method.DeclaringType?.Name}.{handler.Method.Name}";
        var now = DateTime.UtcNow;
        if (_lastFailureLoggedUtc.TryGetValue(name, out var last) && now - last < TimeSpan.FromMinutes(1)) return;

        _lastFailureLoggedUtc[name] = now;
        SpotiSharpBackend.DiagnosticLog.Write($"[UiLoop] {name} threw: {ex}");
    }
}