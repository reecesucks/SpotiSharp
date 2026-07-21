using System.Windows.Input;

namespace SpotiSharp.Behaviors;

// Native long-press for a view. PointerGestureRecognizer is unreliable inside a
// CollectionView on Android (the scroll container steals the touch), so this hooks
// Android's own LongClick, which coexists with list scrolling. No-op elsewhere.
#if ANDROID
public class LongPressBehavior : PlatformBehavior<View, Android.Views.View>
#else
public class LongPressBehavior : PlatformBehavior<View>
#endif
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

#if ANDROID
    private View _virtualView;

    protected override void OnAttachedTo(View bindable, Android.Views.View platformView)
    {
        _virtualView = bindable;
        platformView.LongClickable = true;
        platformView.LongClick += OnLongClick;
    }

    protected override void OnDetachedFrom(View bindable, Android.Views.View platformView)
    {
        platformView.LongClick -= OnLongClick;
        _virtualView = null;
    }

    private void OnLongClick(object sender, Android.Views.View.LongClickEventArgs e)
    {
        e.Handled = true;
        var parameter = _virtualView?.BindingContext;
        if (Command?.CanExecute(parameter) == true) Command.Execute(parameter);
    }
#endif
}
