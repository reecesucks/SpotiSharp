using Android.Views;

namespace QinF25.Input;

/// <summary>
/// Base <c>Activity</c> that funnels physical key presses into
/// <see cref="KeypadManager"/>. Android apps that want keypad support simply
/// derive their <c>MainActivity</c> from this class instead of
/// <c>MauiAppCompatActivity</c>.
/// </summary>
/// <remarks>
/// Keys are intercepted at <see cref="DispatchKeyEvent"/> — the window's first
/// look at every key — rather than <c>OnKeyDown</c>, because a focused view
/// (a list or scroll view) can consume d-pad keys before <c>OnKeyDown</c> ever
/// runs on the activity. A key that no subscriber handles falls through to the
/// base implementation, so system keys such as Back and the volume rocker keep
/// their default behaviour until a page explicitly opts to consume them.
/// </remarks>
public abstract class KeypadActivity : MauiAppCompatActivity
{
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        // Only act on the initial press; ignore key-up and auto-repeat so a
        // single physical press produces a single dispatch.
        if (e is not null && e.Action == KeyEventActions.Down && e.RepeatCount == 0)
        {
            if (DispatchKeypadKey(e.KeyCode))
                return true;
        }

        return base.DispatchKeyEvent(e);
    }

    private static bool DispatchKeypadKey(Keycode keyCode)
    {
        var key = AndroidKeyMapper.Map(keyCode);
        return KeypadManager.Instance.Dispatch(key, (int)keyCode, keyCode.ToString());
    }
}
