using SpotiSharp.ViewModels;

namespace SpotiSharp.Views;

public class BasePage : ContentPage
{
    protected virtual double TitleTranslationX => -35;

    public BasePage()
    {
        ControlTemplate = new ControlTemplate(CreatePageTemplate);
        Shell.SetTitleView(this, CreateTitleView());
    }

    private View CreateTitleView()
    {
        var title = new Label
        {
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };
        title.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
        title.SetDynamicResource(Label.FontFamilyProperty, "BodyFont");
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

#if ANDROID
        title.TranslationX = TitleTranslationX;
#endif

        return new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            Children = { title }
        };
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