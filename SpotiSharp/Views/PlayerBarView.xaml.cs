using Microsoft.Maui.Controls.Shapes;
using SpotiSharp.Themes;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class PlayerBarView : ContentView
{
    private const string IpodPrevData = "M4,6h2v12H4z M13,6L6,12L13,18Z M20,6L13,12L20,18Z";
    private const string IpodNextData = "M4,6L11,12L4,18Z M11,6L18,12L11,18Z M18,6h2v12h-2z";

    private const string IpodShuffleData = "M2,5 H6 L18,17 V14 L22,19 L16,21 V18 L4,6 H2 Z M2,19 H6 L18,7 V10 L22,5 L16,3 V6 L4,18 H2 Z";
    private const string IpodRepeatData = "M5,8 H15 V11 L20,6.5 L15,2 V5 H3 V13 H6 V8 Z M19,16 H9 V13 L4,17.5 L9,22 V19 H21 V11 H18 V16 Z";

    private readonly Geometry _spotifyPrevData;
    private readonly Geometry _spotifyNextData;
    private readonly Geometry _spotifyShuffleData;
    private readonly Geometry _spotifyRepeatData;

    public PlayerBarView()
    {
        InitializeComponent();
        BindingContext = PlayerBarViewModel.Instance;

        _spotifyPrevData = PrevIcon.Data;
        _spotifyNextData = NextIcon.Data;
        _spotifyShuffleData = ShuffleIcon.Data;
        _spotifyRepeatData = RepeatIcon.Data;

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
            ShuffleIcon.Data = ParseGeometry(IpodShuffleData);
            RepeatIcon.Data = ParseGeometry(IpodRepeatData);
        }
        else
        {
            PrevIcon.Data = _spotifyPrevData;
            NextIcon.Data = _spotifyNextData;
            ShuffleIcon.Data = _spotifyShuffleData;
            RepeatIcon.Data = _spotifyRepeatData;
        }
    }

    private static Geometry ParseGeometry(string data) =>
        (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data);
}