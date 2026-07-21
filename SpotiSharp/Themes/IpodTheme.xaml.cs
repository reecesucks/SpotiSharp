namespace SpotiSharp.Themes;

public partial class IpodTheme : ResourceDictionary, IThemeResourceDictionary
{
    public IpodTheme()
    {
        InitializeComponent();
    }

    public AppThemeVariant Variant => AppThemeVariant.Ipod;
}
