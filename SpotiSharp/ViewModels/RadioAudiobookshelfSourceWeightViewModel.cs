using System.Windows.Input;

namespace SpotiSharp.ViewModels;

public class RadioAudiobookshelfSourceWeightViewModel : BaseViewModel
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

    // AudioBookshelf shows don't support weighting yet (the AudioBookshelf episode provider always
    // just takes the most recent episodes across shows) - hides the weight stepper there rather than
    // showing a control that does nothing.
    public bool ShowWeight { get; }

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

    public RadioAudiobookshelfSourceWeightViewModel(string id, string name, string imageUrl, int weight, Action<string, int> onChanged,
        string bingeStatus = null, Func<RadioAudiobookshelfSourceWeightViewModel, Task> onToggleBinge = null, bool showWeight = true)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        _weight = weight;
        _onChanged = onChanged;
        _bingeStatus = bingeStatus;
        ShowWeight = showWeight;
        if (onToggleBinge != null) ToggleBinge = new Command(async () => await onToggleBinge(this));
    }
}
