using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioPage : BasePage
{
    public RadioPage()
    {
        InitializeComponent();
        BindingContext = new RadioPageViewModel();

        MainListView.SelectionChanged += (sender, args) =>
        {
            var selectedItem = args.CurrentSelection.FirstOrDefault();
            if (selectedItem != null && BindingContext is RadioPageViewModel radioPageViewModel)
                radioPageViewModel.ClickItem(selectedItem);
            MainListView.SelectedItem = null;
        };
    }
}
