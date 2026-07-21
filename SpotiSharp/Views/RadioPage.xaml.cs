using System.Windows.Input;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public partial class RadioPage : BasePage
{
    private bool _longPressFired;

    // bound from the row's LongPressBehavior; the radio item is the command parameter
    public ICommand ItemLongPressed { get; }

    public RadioPage()
    {
        InitializeComponent();
        BindingContext = new RadioPageViewModel();
        ItemLongPressed = new Command<RadioItem>(OnItemLongPressed);
    }

    private void OnItemTapped(object sender, TappedEventArgs e)
    {
        // a long press just handled this touch, don't also play
        if (_longPressFired)
        {
            _longPressFired = false;
            return;
        }

        if ((sender as VisualElement)?.BindingContext is RadioItem item && BindingContext is RadioPageViewModel vm)
            vm.ClickItem(item);
    }

    private async void OnItemLongPressed(RadioItem item)
    {
        _longPressFired = true;
        if (item == null || BindingContext is not RadioPageViewModel vm) return;

        string what = item.IsPodcastSegment ? "podcast" : "song";
        bool remove = await DisplayAlert("Remove", $"Remove this {what} from the radio?", "Remove", "Cancel");
        if (remove) vm.RemoveRadioItem(item);
    }
}
