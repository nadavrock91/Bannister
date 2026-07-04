using Bannister.Services;

namespace Bannister.Views;

public class PromptsLibraryPage : ContentPage
{
    private readonly AuthService _auth;

    public PromptsLibraryPage(AuthService auth)
    {
        _auth = auth;

        Title = "Prompts Library";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 18
        };

        stack.Children.Add(new Label
        {
            Text = " Prompts Library",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#00838F")
        });

        stack.Children.Add(new Label
        {
            Text = "Coming soon.",
            FontSize = 18,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#999"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 80, 0, 0)
        });

        Content = new ScrollView { Content = stack };
    }
}
