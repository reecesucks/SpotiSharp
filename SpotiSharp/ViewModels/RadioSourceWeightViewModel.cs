namespace SpotiSharp.ViewModels;

public class RadioSourceWeightViewModel : BaseViewModel
{
    public string Id { get; }
    public string Name { get; }
    public string ImageUrl { get; }

    private int _weight;
    private readonly Action<string, int> _onChanged;

    public int Weight
    {
        get { return _weight; }
        set
        {
            if (SetProperty(ref _weight, value)) _onChanged(Id, value);
        }
    }

    public RadioSourceWeightViewModel(string id, string name, string imageUrl, int weight, Action<string, int> onChanged)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        _weight = weight;
        _onChanged = onChanged;
    }
}
