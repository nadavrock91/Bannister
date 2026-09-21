using Bannister.Services;

namespace Bannister.Views;

public class MotivationHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MotivationService _service;

    public MotivationHubPage(AuthService auth, MotivationService service)
    {
        _auth = auth;
        _service = service;
        Title = "Motivation";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        stack.Children.Add(new Label
        {
            Text = "Motivation",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        var sources = new Button
        {
            Text = "Motivation Sources",
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#E65100"),
            CornerRadius = 8,
            HeightRequest = 48
        };
        sources.Clicked += async (_, _) =>
            await Navigation.PushAsync(new MotivationSourcesPage(_auth, _service));
        stack.Children.Add(sources);
        Content = new ScrollView { Content = stack };
    }
}
