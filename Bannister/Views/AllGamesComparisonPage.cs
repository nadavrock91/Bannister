using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

/// <summary>
/// Shows bar chart comparison of all games, with monthly snapshots that you can scroll through
/// </summary>
public class AllGamesComparisonPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;

    private VerticalStackLayout _chartsContainer;
    private List<Game> _allGames = new();
    private Dictionary<string, List<ExpLog>> _gameLogsCache = new();

    public AllGamesComparisonPage(AuthService auth, GameService games, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _exp = exp;
        _db = db;

        Title = "All Games Comparison";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        mainStack.Children.Add((new Label
        {
            Text = "🏆 All Games Comparison",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        mainStack.Children.Add((new Label
        {
            Text = "Compare levels across all games\nScroll down to see monthly history",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        // Charts container
        _chartsContainer = new VerticalStackLayout { Spacing = 24 };
        mainStack.Children.Add(_chartsContainer);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async Task LoadDataAsync()
    {
        _chartsContainer.Children.Clear();

        _allGames = await _games.GetGamesAsync(_auth.CurrentUsername);

        if (_allGames.Count == 0)
        {
            _chartsContainer.Children.Add((new Label
            {
                Text = "No games found. Create a game to see charts!",
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        // Load all logs for each game
        _gameLogsCache.Clear();
        foreach (var game in _allGames)
        {
            var logs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, game.GameId);
            _gameLogsCache[game.GameId] = logs;
        }

        // Current levels chart
        _chartsContainer.Children.Add(await CreateCurrentLevelsChartAsync());

        // Monthly snapshots
        var months = GetMonthsWithData();
        
        if (months.Count > 0)
        {
            _chartsContainer.Children.Add((new Label
            {
                Text = "📅 Monthly History",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 16, 0, 8)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

            foreach (var month in months.OrderByDescending(m => m))
            {
                _chartsContainer.Children.Add(CreateMonthlyChart(month));
            }
        }
    }

    private async Task<Frame> CreateCurrentLevelsChartAsync()
    {
        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.SuccessButtonBackground);

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add((new Label
        {
            Text = "🎯 Current Levels",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true));

        // Horizontal scroll for bar chart
        var scrollView = new ScrollView { Orientation = ScrollOrientation.Horizontal };
        var barContainer = new HorizontalStackLayout { Spacing = 16, Padding = new Thickness(8) };

        foreach (var game in _allGames)
        {
            var (level, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, game.GameId);
            barContainer.Children.Add(CreateGameBar(game.DisplayName, level, ThemeKey.SuccessButtonBackground));
        }

        scrollView.Content = barContainer;
        stack.Children.Add(scrollView);

        // Legend
        stack.Children.Add((new Label
        {
            Text = "← Scroll horizontally if needed →",
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));

        frame.Content = stack;
        return frame;
    }

    private Frame CreateMonthlyChart(DateTime month)
    {
        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add((new Label
        {
            Text = $"📅 {month:MMMM yyyy}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        // Horizontal scroll for bar chart
        var scrollView = new ScrollView { Orientation = ScrollOrientation.Horizontal };
        var barContainer = new HorizontalStackLayout { Spacing = 16, Padding = new Thickness(8) };

        // Calculate level at end of this month for each game
        var endOfMonth = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month), 23, 59, 59);

        foreach (var game in _allGames)
        {
            int levelAtMonth = GetLevelAtDate(game.GameId, endOfMonth);
            
            // Use different colors for different months
            var color = GetMonthColor(month);
            barContainer.Children.Add(CreateGameBar(game.DisplayName, levelAtMonth, color));
        }

        scrollView.Content = barContainer;
        stack.Children.Add(scrollView);

        frame.Content = stack;
        return frame;
    }

    private View CreateGameBar(string gameName, int level, ThemeKey barColor)
    {
        var stack = new VerticalStackLayout 
        { 
            Spacing = 4,
            WidthRequest = 80,
            HorizontalOptions = LayoutOptions.Center
        };

        // Level label on top
        stack.Children.Add((new Label
        {
            Text = $"Lv {level}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3, classifyPageText: true));

        // Bar container (fixed height, bar grows from bottom)
        var barFrame = (new Frame
        {
            Padding = 0,
            CornerRadius = 4,
            HeightRequest = 120,
            WidthRequest = 50,
            HasShadow = false,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator);

        // Inner bar (height based on level, max 100)
        double barHeight = Math.Min(level, 100) / 100.0 * 110; // Max 110px
        
        var innerBar = new BoxView
        {
            HeightRequest = barHeight,
            WidthRequest = 46,
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Center,
            CornerRadius = 2
        };

        // Use a Grid to overlay the bar
        var grid = new Grid
        {
            HeightRequest = 120,
            WidthRequest = 50
        };
        innerBar.ThemeColor(BoxView.ColorProperty, barColor);
        grid.Children.Add(innerBar);

        barFrame.Content = grid;
        stack.Children.Add(barFrame);

        // Game name (truncated)
        string displayName = gameName.Length > 10 ? gameName.Substring(0, 8) + "..." : gameName;
        stack.Children.Add((new Label
        {
            Text = displayName,
            FontSize = 10,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        return stack;
    }

    private List<DateTime> GetMonthsWithData()
    {
        var months = new HashSet<DateTime>();

        foreach (var logs in _gameLogsCache.Values)
        {
            foreach (var log in logs)
            {
                var monthStart = new DateTime(log.LoggedAt.Year, log.LoggedAt.Month, 1);
                months.Add(monthStart);
            }
        }

        // Don't include current month (that's shown in "Current Levels")
        var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        months.Remove(currentMonth);

        return months.ToList();
    }

    private int GetLevelAtDate(string gameId, DateTime date)
    {
        if (!_gameLogsCache.TryGetValue(gameId, out var logs) || logs.Count == 0)
            return 1;

        // Find the last log entry before or on this date
        var logsBeforeDate = logs
            .Where(l => l.LoggedAt <= date)
            .OrderByDescending(l => l.LoggedAt)
            .FirstOrDefault();

        return logsBeforeDate?.LevelAfter ?? 1;
    }

    private ThemeKey GetMonthColor(DateTime month)
    {
        // Rotate through colors based on month
        var colors = new[]
        {
            ThemeKey.InfoButtonBackground, // Blue
            ThemeKey.HighlightVioletText, // Purple
            ThemeKey.WarningButtonBackground, // Orange
            ThemeKey.HighlightTealText, // Teal
            ThemeKey.HighlightPinkText, // Pink
            ThemeKey.HighlightIndigoText, // Indigo
            ThemeKey.NeutralDarkButton, // Brown
            ThemeKey.NeutralDarkButton, // Blue Grey
            ThemeKey.SuccessButtonBackground, // Light Green
            ThemeKey.WarningButtonBackground, // Amber
            ThemeKey.HighlightTealText, // Cyan
            ThemeKey.WarningButtonBackground, // Deep Orange
        };

        return colors[month.Month % colors.Length];
    }
}
