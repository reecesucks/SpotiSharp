using System.Windows.Input;

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

    public ICommand ToggleBinge { get; }
    public bool SupportsBinge => ToggleBinge != null;

    private string _bingeStatus;

    public string BingeStatus
    {
        get { return _bingeStatus; }
    }

    public bool HasBinge => !string.IsNullOrEmpty(_bingeStatus);

    internal void SetBingeStatus(string status)
    {
        _bingeStatus = status;
        OnPropertyChanged(nameof(BingeStatus));
        OnPropertyChanged(nameof(HasBinge));
    }

    public RadioSourceWeightViewModel(string id, string name, string imageUrl, int weight, Action<string, int> onChanged,
        string bingeStatus = null, Func<RadioSourceWeightViewModel, Task> onToggleBinge = null)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        _weight = weight;
        _onChanged = onChanged;
        _bingeStatus = bingeStatus;
        if (onToggleBinge != null) ToggleBinge = new Command(async () => await onToggleBinge(this));
    }
}
