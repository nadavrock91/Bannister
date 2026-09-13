using Bannister.Services;

namespace Bannister.Views;

public class ConsequencesHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ResetEnforcerService _resetService;

    public ConsequencesHubPage(
        AuthService auth,
        ResetEnforcerService resetService)
    {
        _auth = auth;
        _resetService = resetService;
        Title = "Consequences";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = "⚖️ Consequences",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222"),
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = "Define the consequences of breaking your commitments.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666"),
                    HorizontalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                CreateHubCard(
                    "Resets",
                    "Track commitments where breaking them resets your count.",
                    OnResetsTapped)
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private Frame CreateHubCard(
        string title, string subtitle,
        EventHandler<TappedEventArgs> tapped)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        grid.Add(textStack, 0, 0);
        grid.Add(new Label
        {
            Text = "→",
            FontSize = 24,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true,
            Content = grid
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += tapped;
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async void OnResetsTapped(
        object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
            new ResetsPage(_resetService, _auth));
    }
}
