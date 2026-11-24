using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class AudiobookshelfRecentEpisodesFlatListView : ContentView
{
    // How long a single tap waits before firing, to see whether a second tap follows and turns it
    // into a double-tap instead - same idea as a native double-tap gesture's timeout.
    private const int DoubleTapWindowMs = 320;

    public AudiobookshelfRecentEpisodesFlatListView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (BindingContext is AudiobookshelfRecentEpisodesFlatViewModel vm) vm.Section = FlatList;
        };
    }

    private object? _pendingTapKey;
    private AudiobookshelfEpisode? _pendingTapEpisode;
    private IDispatcherTimer? _pendingTapTimer;

    /// <summary>A lone tap plays the episode once the window above passes with no second tap; a
    /// second tap on the same row within the window cancels that and marks/unmarks the episode as
    /// up next for the next AudioBookshelf radio generation instead.</summary>
    private void HandleRowTap(object key, AudiobookshelfEpisode episode)
    {
        if (BindingContext is not AudiobookshelfRecentEpisodesFlatViewModel vm) return;

        if (Equals(_pendingTapKey, key))
        {
            _pendingTapTimer?.Stop();
            _pendingTapTimer = null;
            _pendingTapKey = null;
            _pendingTapEpisode = null;
            vm.ToggleMarkCommand.Execute(episode);
            return;
        }

        _pendingTapTimer?.Stop();
        _pendingTapKey = key;
        _pendingTapEpisode = episode;

        var timer = Application.Current?.Dispatcher.CreateTimer();
        if (timer == null)
        {
            // No dispatcher available (shouldn't happen in practice) - fall back to playing right away.
            _pendingTapKey = null;
            _pendingTapEpisode = null;
            vm.PlayEpisodeCommand.Execute(episode);
            return;
        }

        timer.Interval = TimeSpan.FromMilliseconds(DoubleTapWindowMs);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            if (Equals(_pendingTapKey, key) && _pendingTapEpisode == episode) vm.PlayEpisodeCommand.Execute(episode);
            _pendingTapTimer = null;
            _pendingTapKey = null;
            _pendingTapEpisode = null;
        };
        _pendingTapTimer = timer;
        timer.Start();
    }

#if ANDROID
    private readonly Dictionary<global::Android.Views.View, View> _rowElements = new();

    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element) return;

        if (element.Handler?.PlatformView is not global::Android.Views.View platformView)
        {
            foreach (var stale in _rowElements.Where(entry => ReferenceEquals(entry.Value, element)).Select(entry => entry.Key).ToList())
            {
                _rowElements.Remove(stale);
            }
            return;
        }

        _rowElements[platformView] = element;

        platformView.Clickable = true;
        platformView.Click -= OnRowClick;
        platformView.Click += OnRowClick;
    }

    private void OnRowClick(object sender, EventArgs e)
    {
        if (sender is not global::Android.Views.View platformView) return;
        if (ResolveItem(platformView) is not AudiobookshelfEpisode episode) return;
        HandleRowTap(platformView, episode);
    }

    private AudiobookshelfEpisode? ResolveItem(global::Android.Views.View platformView)
    {
        return _rowElements.TryGetValue(platformView, out var element) ? element.BindingContext as AudiobookshelfEpisode : null;
    }
#else
    // Non-Android fallback (desktop/iOS), same debounce logic driven by a plain tap gesture.
    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element || element.GestureRecognizers.Count > 0) return;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, _) =>
        {
            if ((s as VisualElement)?.BindingContext is AudiobookshelfEpisode episode) HandleRowTap(s!, episode);
        };
        element.GestureRecognizers.Add(tap);
    }
#endif
}
