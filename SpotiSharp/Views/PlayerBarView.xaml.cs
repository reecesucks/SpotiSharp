using Microsoft.Maui.Controls.Shapes;
using SpotiSharp.Themes;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PlayerBarView : ContentView
{
    private const string IpodPrevData = "M4,6h2v12H4z M13,6L6,12L13,18Z M20,6L13,12L20,18Z";
    private const string IpodNextData = "M4,6L11,12L4,18Z M11,6L18,12L11,18Z M18,6h2v12h-2z";

    private readonly Geometry _spotifyPrevData;
    private readonly Geometry _spotifyNextData;

    public PlayerBarView()
    {
        InitializeComponent();
        BindingContext = PlayerBarViewModel.Instance;

        _spotifyPrevData = PrevIcon.Data;
        _spotifyNextData = NextIcon.Data;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, EventArgs e)
    {
        ThemeService.ThemeChanged += OnThemeChanged;
        ApplyThemeIcons(ThemeService.Current);
    }

    private void OnUnloaded(object sender, EventArgs e)
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(AppThemeVariant variant) => ApplyThemeIcons(variant);

    private void ApplyThemeIcons(AppThemeVariant variant)
    {
        if (variant == AppThemeVariant.Ipod)
        {
            PrevIcon.Data = ParseGeometry(IpodPrevData);
            NextIcon.Data = ParseGeometry(IpodNextData);
        }
        else
        {
            PrevIcon.Data = _spotifyPrevData;
            NextIcon.Data = _spotifyNextData;
        }
    }

    private static Geometry ParseGeometry(string data) =>
        (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data);
}