using Bannister.Services;

namespace Bannister.Views;

public class ProgressHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;

    public ProgressHubPage(AuthService auth, GameService games, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _exp = exp;
        _db = db;

        Title = "Progress";


        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        stack.Children.Add((new Label
        {
            Text = " Progress",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.AccentVioletButton, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Track progress across your systems and games.",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(BuildHubCard(
            "Progress Summary",
            "Ranked level gains by game across time periods.",
            ThemeKey.AccentVioletButton,
            async () => await Navigation.PushAsync(new ProgressSummaryPage(_auth, _games, _exp, _db))));

        Content = new ScrollView { Content = stack };
    }

    private Frame BuildHubCard(string header, string subtitle, ThemeKey accent, Func<Task> onTap)
    {
        var frame = (new Frame
        {
            CornerRadius = 12,
            Padding = 16,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    (new Label { Text = header, FontSize = 16, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, accent, ThemeKey.PageText1, classifyPageText: true),
                    (new Label { Text = subtitle, FontSize = 13 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true)
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, accent);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        frame.GestureRecognizers.Add(tap);

        return frame;
    }
}
