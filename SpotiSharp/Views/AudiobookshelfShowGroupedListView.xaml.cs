using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class AudiobookshelfShowGroupedListView : ContentView
{
    public AudiobookshelfShowGroupedListView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (BindingContext is AudiobookshelfPodcastsViewModel vm) vm.Section = ShowList;
        };
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
        platformView.LongClickable = true;

        platformView.Click -= OnRowClick;
        platformView.Click += OnRowClick;
        platformView.LongClick -= OnRowLongClick;
        platformView.LongClick += OnRowLongClick;
    }

    private void OnRowClick(object sender, EventArgs e)
    {
        if (ResolveItem(sender) is not AudiobookshelfEpisode episode) return;
        if (BindingContext is AudiobookshelfPodcastsViewModel vm) vm.PlayEpisodeCommand.Execute(episode);
    }

    private async void OnRowLongClick(object sender, global::Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;

        if (ResolveItem(sender) is not AudiobookshelfEpisode episode) return;
        if (BindingContext is not AudiobookshelfPodcastsViewModel vm) return;

        var choice = await Shell.Current.DisplayActionSheet(episode.EpisodeName, "Cancel", null, "Binge from here");
        if (choice == "Binge from here") await vm.BingeFromHereAsync(episode);
    }

    private AudiobookshelfEpisode? ResolveItem(object platformSender)
    {
        if (platformSender is not global::Android.Views.View platformView) return null;
        return _rowElements.TryGetValue(platformView, out var element) ? element.BindingContext as AudiobookshelfEpisode : null;
    }
#else
    // No long-press on non-Android targets (desktop/iOS) - tap still works via ActivateCommand,
    // "Binge from here" is Android-only for now, same as the Radio page's long-press actions.
    private void OnRowHandlerChanged(object sender, EventArgs e)
    {
        if (sender is not View element || element.GestureRecognizers.Count > 0) return;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, _) =>
        {
            if ((s as VisualElement)?.BindingContext is AudiobookshelfEpisode episode && BindingContext is AudiobookshelfPodcastsViewModel vm)
                vm.PlayEpisodeCommand.Execute(episode);
        };
        element.GestureRecognizers.Add(tap);
    }
#endif
}
