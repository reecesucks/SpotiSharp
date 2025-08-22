using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;

namespace SpotiSharp.Controls;

public partial class MultiSelectPicker : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(MultiSelectPicker), default(IEnumerable));

    public static readonly BindableProperty SelectedItemsProperty =
        BindableProperty.Create(nameof(SelectedItems), typeof(ObservableCollection<object>), typeof(MultiSelectPicker), new ObservableCollection<object>(), BindingMode.TwoWay);

    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(MultiSelectPicker), default(string));

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ObservableCollection<object> SelectedItems
    {
        get => (ObservableCollection<object>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public MultiSelectPicker()
    {
        InitializeComponent();
    }

    private async void OnTapped(object sender, EventArgs e)
    {
        var page = new ContentPage
        {
            Title = "Select Items"
        };

        var stack = new StackLayout();

        foreach (var item in ItemsSource)
        {
            string displayText = GetDisplayText(item);

            var cb = new CheckBox { IsChecked = SelectedItems.Contains(item) };
            var lbl = new Label { Text = displayText, VerticalOptions = LayoutOptions.Center };

            var row = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Children = { cb, lbl }
            };

            cb.CheckedChanged += (s, args) =>
            {
                if (args.Value && !SelectedItems.Contains(item))
                    SelectedItems.Add(item);
                else if (!args.Value && SelectedItems.Contains(item))
                    SelectedItems.Remove(item);

                DisplayLabel.Text = SelectedItems.Any()
                    ? string.Join(", ", SelectedItems.Select(GetDisplayText))
                    : "Select...";
            };

            stack.Children.Add(row);
        }

        var closeButton = new Button { Text = "Done" };
        closeButton.Clicked += async (s, args) => await Application.Current.MainPage.Navigation.PopModalAsync();

        stack.Children.Add(closeButton);

        page.Content = new ScrollView { Content = stack };

        await Application.Current.MainPage.Navigation.PushModalAsync(page);
    }

    private string GetDisplayText(object item)
    {
        if (item == null) return string.Empty;

        if (string.IsNullOrEmpty(DisplayMemberPath))
            return item.ToString() ?? string.Empty;

        var prop = item.GetType().GetProperty(DisplayMemberPath, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(item)?.ToString() ?? item.ToString() ?? string.Empty;
    }
}