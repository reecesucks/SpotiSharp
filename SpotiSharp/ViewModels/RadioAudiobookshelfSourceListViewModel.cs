namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfSourceListViewModel : BaseViewModel
{
    public string Title { get; }

    private readonly Func<List<RadioAudiobookshelfSourceWeightViewModel>> _loadItems;
    private readonly Action _refreshSource;

    private bool _isLoading;
    public bool IsLoading
    {
        get { return _isLoading; }
        private set { SetProperty(ref _isLoading, value); }
    }

    private List<RadioAudiobookshelfSourceWeightViewModel> _items = new List<RadioAudiobookshelfSourceWeightViewModel>();

    public List<RadioAudiobookshelfSourceWeightViewModel> Items
    {
        get { return _items; }
        private set { SetProperty(ref _items, value); }
    }

    private RadioAudiobookshelfSourceWeightViewModel _selectedItem;

    public RadioAudiobookshelfSourceWeightViewModel SelectedItem
    {
        get { return _selectedItem; }
        set { SetProperty(ref _selectedItem, value); }
    }

    public RadioAudiobookshelfSourceListViewModel(string title, Func<List<RadioAudiobookshelfSourceWeightViewModel>> loadItems, Action refreshSource)
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
