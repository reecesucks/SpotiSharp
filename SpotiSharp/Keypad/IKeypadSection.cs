using QinF25.Input;

namespace SpotiSharp.Keypad;

/// <summary>
/// A focusable region on a page that can be driven by the physical keypad — a
/// list, a row of buttons, a set of toggles. A page with a single section lets
/// that section handle keys directly; a page with several will (later) own a
/// focus scope that routes keys to whichever section is active.
/// </summary>
public interface IKeypadSection
{
    /// <summary>
    /// Handle <paramref name="key"/> if it applies to this section.
    /// </summary>
    /// <returns><c>true</c> if the key was consumed; <c>false</c> to let it pass
    /// (e.g. so a scope can move focus to another section, or Back can bubble).</returns>
    bool HandleKey(KeypadKey key);

    /// <summary>
    /// Called by a focus scope when this section gains or loses focus, so it can
    /// show or hide its focus visuals. Sections on a single-section page never
    /// lose focus, so this is a no-op there.
    /// </summary>
    void SetActive(bool active);
}
