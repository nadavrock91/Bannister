using Bannister.Services;

namespace Bannister.Views;

public class SystemsLevelProgressSummaryPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;

    public SystemsLevelProgressSummaryPage(AuthService auth, GameService games, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _exp = exp;
        _db = db;

        Title = "Systems Level Progress Summary";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        stack.Children.Add(new Label
        {
            Text = " Systems Level Progress Summary",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5E35B1")
        });

        stack.Children.Add(new Label
        {
            Text = "Rank games by level gained in the selected period.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(BuildHubCard(
            "Monthly Summary",
            "This calendar month, ranked by level gained.",
            Color.FromArgb("#1976D2"),
            async () => await Navigation.PushAsync(new SystemsMonthlyProgressPage(_auth, _games, _exp, _db))));

        stack.Children.Add(BuildHubCard(
            "Yearly Summary",
            "This calendar year, ranked by level gained.",
            Color.FromArgb("#5E35B1"),
            async () => await Navigation.PushAsync(new SystemsYearlyProgressPage(_auth, _games, _exp, _db))));

        Content = new ScrollView { Content = stack };
    }

    private Frame BuildHubCard(string header, string subtitle, Color accent, Func<Task> onTap)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = accent,
            CornerRadius = 12,
            Padding = 16,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = header, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = accent },
                    new Label { Text = subtitle, FontSize = 13, TextColor = Color.FromArgb("#666") }
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        frame.GestureRecognizers.Add(tap);

        return frame;
    }
}
