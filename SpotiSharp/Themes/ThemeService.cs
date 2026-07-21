namespace SpotiSharp.Themes;

/// <summary>
/// Swaps the palette dictionary merged into <see cref="Application.Resources"/>. Everything that
/// reacts to a theme change references its colours with <c>DynamicResource</c>, so replacing the
/// dictionary repaints the running UI without a restart.
/// </summary>
public static class ThemeService
{
    private const string PreferenceKey = "app_theme_variant";

    private const AppThemeVariant DefaultVariant = AppThemeVariant.Ipod;

    public static AppThemeVariant Current { get; private set; } = DefaultVariant;

    public static event Action<AppThemeVariant>? ThemeChanged;

    /// <summary>Applies the persisted theme. Call once, after the application resources exist.</summary>
    public static void Initialize()
    {
        var stored = Preferences.Default.Get(PreferenceKey, DefaultVariant.ToString());
        if (!Enum.TryParse<AppThemeVariant>(stored, out var variant))
        {
            variant = DefaultVariant;
        }

        Apply(variant, persist: false);
    }

    public static void Toggle() =>
        Apply(Current == AppThemeVariant.Spotify ? AppThemeVariant.Ipod : AppThemeVariant.Spotify);

    public static void Apply(AppThemeVariant variant, bool persist = true)
    {
        var merged = Application.Current?.Resources?.MergedDictionaries;
        if (merged is null) return;

        var existing = merged.OfType<IThemeResourceDictionary>().ToList();
        if (existing.Count == 1 && existing[0].Variant == variant && Current == variant) return;

        foreach (var dictionary in existing)
        {
            merged.Remove((ResourceDictionary)dictionary);
        }

        merged.Add(variant == AppThemeVariant.Ipod
            ? new IpodTheme()
            : new SpotifyTheme());

        Current = variant;

        if (persist)
        {
            Preferences.Default.Set(PreferenceKey, variant.ToString());
        }

        ThemeChanged?.Invoke(variant);
    }
}
