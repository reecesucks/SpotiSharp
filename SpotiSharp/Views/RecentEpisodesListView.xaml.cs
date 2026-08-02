using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RecentEpisodesListView : ContentView
{
    public RecentEpisodesListView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (BindingContext is RecentEpisodesListViewModel vm) vm.Section = GroupedList;
        };
    }
}
