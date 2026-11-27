using Bannister.Services;

namespace Bannister.Views;

public class MotivationHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly MotivationService _service;

    public MotivationHubPage(AuthService auth, MotivationService service)
    {
        _auth = auth;
        _service = service;
        Title = "Motivation";

        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        stack.Children.Add((new Label
        {
            Text = "Motivation",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        var sources = (new Button
        {
            Text = "Motivation Sources",
            CornerRadius = 8,
            HeightRequest = 48
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        sources.Clicked += async (_, _) =>
            await Navigation.PushAsync(new MotivationSourcesPage(_auth, _service));
        stack.Children.Add(sources);
        Content = new ScrollView { Content = stack };
    }
}
