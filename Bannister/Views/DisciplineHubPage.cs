using Bannister.Services;

namespace Bannister.Views;

public class DisciplineHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ResetEnforcerService _resetService;

    public DisciplineHubPage(
        AuthService auth,
        ResetEnforcerService resetService)
    {
        _auth = auth;
        _resetService = resetService;
        Title = "Discipline";
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
                    Text = " Discipline",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222"),
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = "Systems for maintaining discipline and " +
                           "accountability.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666"),
                    HorizontalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                CreateHubCard(
                    "Consequences",
                    "Define and track the consequences of breaking " +
                    "your commitments.",
                    OnConsequencesTapped)
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

    private async void OnConsequencesTapped(
        object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
            new ConsequencesHubPage(_auth, _resetService));
    }
}
