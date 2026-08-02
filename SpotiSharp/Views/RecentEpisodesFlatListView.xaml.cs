using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RecentEpisodesFlatListView : ContentView
{
    public RecentEpisodesFlatListView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (BindingContext is RecentEpisodesFlatViewModel vm) vm.Section = FlatList;
        };
    }
}
