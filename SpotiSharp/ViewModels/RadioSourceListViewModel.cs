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
        // pull-to-refresh forces the api, then rebuilds the toggle rows from fresh data
        Items = await Task.Run(() =>
        {
            _refreshSource();
            return _loadItems();
        });
    }
}
