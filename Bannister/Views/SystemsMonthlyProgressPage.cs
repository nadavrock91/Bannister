using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SystemsMonthlyProgressPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;
    private readonly VerticalStackLayout _rowsStack = new() { Spacing = 8 };

    public SystemsMonthlyProgressPage(AuthService auth, GameService games, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _exp = exp;
        _db = db;

        Title = "Monthly Progress";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProgressAsync();
    }

    private void BuildUI()
    {
        var periodStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        stack.Children.Add((new Label
        {
            Text = " Monthly Progress",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = $"{periodStart:MMMM d} → {DateTime.Today:MMMM d, yyyy}",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(_rowsStack);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadProgressAsync()
    {
        var periodStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadProgressForPeriodAsync(periodStart);
    }

    private async Task LoadProgressForPeriodAsync(DateTime periodStart)
    {
        _rowsStack.Children.Clear();

        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        if (games.Count == 0)
        {
            _rowsStack.Children.Add(CreateEmptyState());
            return;
        }

        var rows = new List<(string name, int levelBefore, int levelAfter, int delta)>();
        foreach (var game in games)
        {
            var allLogs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, game.GameId);
            var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, game.GameId);

            var priorLog = allLogs
                .Where(l => l.LoggedAt < periodStart)
                .OrderByDescending(l => l.LoggedAt)
                .FirstOrDefault();

            int levelBefore = priorLog?.LevelAfter ?? 1;
            int delta = currentLevel - levelBefore;
            rows.Add((game.DisplayName, levelBefore, currentLevel, Math.Max(0, delta)));
        }

        var sorted = rows
            .OrderByDescending(r => r.delta)
            .ThenByDescending(r => r.levelAfter)
            .ThenBy(r => r.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var row in sorted)
        {
            _rowsStack.Children.Add(BuildProgressRow(row.name, row.levelBefore, row.levelAfter, row.delta));
        }
    }

    private static View CreateEmptyState() => (new Label
    {
        Text = "No games yet. Create games to track progress.",
        FontSize = 14,
        HorizontalTextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 40, 0, 0)
    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true);

    private static View BuildProgressRow(string gameName, int levelBefore, int levelAfter, int delta)
    {
        var badgeColor = delta > 0 ? ThemeKey.SuccessText : ThemeKey.ButtonPrimaryText;
        var badgeTextColor = delta > 0 ? ThemeKey.ButtonPrimaryText : ThemeKey.NeutralDarkButton;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        row.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                (new Label { Text = gameName, FontSize = 16, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                (new Label { Text = $"Level {levelBefore} → {levelAfter}", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true)
            }
        }, 0, 0);

        row.Add((new Frame
        {
            Padding = new Thickness(10, 4),
            CornerRadius = 12,
            HasShadow = false,
            VerticalOptions = LayoutOptions.Center,
            Content = (new Label
            {
                Text = delta > 0 ? $"+{delta} levels" : "0 levels",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, badgeTextColor, ThemeKey.PageText3, classifyPageText: true)
        }).ThemeColor(VisualElement.BackgroundColorProperty, badgeColor), 1, 0);

        return (new Frame
        {
            CornerRadius = 10,
            Padding = 14,
            HasShadow = false,
            Content = row
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
    }
}
