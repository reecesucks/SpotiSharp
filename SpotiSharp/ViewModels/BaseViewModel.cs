using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SpotiSharp.ViewModels;

public class BaseViewModel : INotifyPropertyChanged
{
    internal delegate void VisibilityChange();
    public event PropertyChangedEventHandler PropertyChanged;

    internal event VisibilityChange OnVisibilityChange;

    internal bool isVisible = false;

    private bool _isRefreshing;

    public bool IsRefreshing
    {
        get { return _isRefreshing; }
        set { SetProperty(ref _isRefreshing, value); }
    }

    public ICommand RefreshCommand { get; }

    protected BaseViewModel()
    {
        RefreshCommand = new Command(async () =>
        {
            try
            {
                await RefreshDataAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        });
    }

    protected virtual Task RefreshDataAsync()
    {
        return Task.CompletedTask;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (Object.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    internal virtual void OnAppearing()
    {
        isVisible = true;
        OnVisibilityChange?.Invoke();
    }

    internal virtual void OnDisappearing()
    {
        isVisible = false;
        OnVisibilityChange?.Invoke();
    }
}