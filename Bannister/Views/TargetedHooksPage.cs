using Bannister.Services;

namespace Bannister.Views;

public class TargetedHooksPage : ContentPage
{
    private readonly AuthService _auth;

    public TargetedHooksPage(AuthService auth)
    {
        _auth = auth;

        Title = "Targeted Hooks";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = " Targeted Hooks",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new Label
                {
                    Text = "Generate scroll-stopping hooks for a specific topic or niche. Coming soon.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666")
                }
            }
        };

        Content = new ScrollView { Content = stack };
    }
}
