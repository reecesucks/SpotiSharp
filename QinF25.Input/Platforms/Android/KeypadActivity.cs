using Android.Views;

namespace QinF25.Input;

/// <summary>
/// Base <c>Activity</c> that funnels physical key presses into
/// <see cref="KeypadManager"/>. Android apps that want keypad support simply
/// derive their <c>MainActivity</c> from this class instead of
/// <c>MauiAppCompatActivity</c>.
/// </summary>
/// <remarks>
/// A key that no subscriber handles falls through to the base implementation,
/// so system keys such as Back and the volume rocker keep their default
/// behaviour until a page explicitly opts to consume them.
/// </remarks>
public abstract class KeypadActivity : MauiAppCompatActivity
{
    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (DispatchKeypadKey(keyCode))
            return true;

        return base.OnKeyDown(keyCode, e);
    }

    private static bool DispatchKeypadKey(Keycode keyCode)
    {
        var key = AndroidKeyMapper.Map(keyCode);
        return KeypadManager.Instance.Dispatch(key);
    }
}
