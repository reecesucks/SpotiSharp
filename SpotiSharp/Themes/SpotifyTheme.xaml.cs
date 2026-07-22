namespace SpotiSharp.Themes;

public partial class SpotifyTheme : ResourceDictionary, IThemeResourceDictionary
{
    public SpotifyTheme()
    {
        InitializeComponent();
    }

    public AppThemeVariant Variant => AppThemeVariant.Spotify;
}
