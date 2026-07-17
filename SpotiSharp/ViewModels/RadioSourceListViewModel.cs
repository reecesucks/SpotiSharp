namespace SpotiSharp.ViewModels;

public class RadioSourceListViewModel : BaseViewModel
{
    public string Title { get; }

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

    public RadioSourceListViewModel(string title, Func<List<RadioSourceWeightViewModel>> loadItems)
    {
        Title = title;
        _ = LoadAsync(loadItems);
    }

    private async Task LoadAsync(Func<List<RadioSourceWeightViewModel>> loadItems)
    {
        IsLoading = true;
        Items = await Task.Run(loadItems);
        IsLoading = false;
    }
}
