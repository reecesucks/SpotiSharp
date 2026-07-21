namespace SpotiSharp.Themes;

/// <summary>
/// Marker for the swappable palette dictionaries so <see cref="ThemeService"/> can find and
/// replace the one currently merged into the application resources.
/// </summary>
public interface IThemeResourceDictionary
{
    AppThemeVariant Variant { get; }
}
