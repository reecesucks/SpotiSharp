using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public class BasePage : ContentPage
{
    public BasePage()
    {
        ControlTemplate = new ControlTemplate(CreatePageTemplate);
    }

    private static object CreatePageTemplate()
    {
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        var contentPresenter = new ContentPresenter();
        layout.Add(contentPresenter);

        var playerBar = new PlayerBarView();
        Grid.SetRow(playerBar, 1);
        layout.Add(playerBar);

        return layout;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        (BindingContext as BaseViewModel)?.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as BaseViewModel)?.OnDisappearing();
    }
}