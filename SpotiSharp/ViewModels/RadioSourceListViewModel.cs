namespace SpotiSharp.ViewModels;

public class RadioSourceListViewModel : BaseViewModel
{
    public string Title { get; }

    private readonly Func<List<RadioSourceWeightViewModel>> _loadItems;
    private readonly Action _refreshSource;

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<RadioSourceWeightViewModel> _items = new List<RadioSourceWeightViewModel>();

    public List<RadioSourceWeightViewModel> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    private RadioSourceWeightViewModel _selectedItem;

    public RadioSourceWeightViewModel SelectedItem
    {
        get { return _selectedItem; }
        set { SetProperty(ref _selectedItem, value); }
    }

    public RadioSourceListViewModel(string title, Func<List<RadioSourceWeightViewModel>> loadItems, Action refreshSource)
    {
        Title = title;
        _loadItems = loadItems;
        _refreshSource = refreshSource;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        Items = await Task.Run(_loadItems);
        IsLoading = false;
    }

    protected override async Task RefreshDataAsync()
    {
        Items = await Task.Run(() =>
        {
            _refreshSource();
            return _loadItems();
        });
    }
}
