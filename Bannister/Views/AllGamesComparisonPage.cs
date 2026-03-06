using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

/// <summary>
/// Shows bar chart comparison of all games, with monthly snapshots that you can scroll through
/// </summary>
public class AllGamesComparisonPage : ContentPage
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
        BackgroundColor = Color.FromArgb("#F5F5F5");

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
        mainStack.Children.Add(new Label
        {
            Text = "🏆 All Games Comparison",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        mainStack.Children.Add(new Label
        {
            Text = "Compare levels across all games\nScroll down to see monthly history",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

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
            _chartsContainer.Children.Add(new Label
            {
                Text = "No games found. Create a game to see charts!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
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
            _chartsContainer.Children.Add(new Label
            {
                Text = "📅 Monthly History",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333"),
                Margin = new Thickness(0, 16, 0, 8)
            });

            foreach (var month in months.OrderByDescending(m => m))
            {
                _chartsContainer.Children.Add(CreateMonthlyChart(month));
            }
        }
    }

    private async Task<Frame> CreateCurrentLevelsChartAsync()
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            BorderColor = Color.FromArgb("#4CAF50"),
            HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = "🎯 Current Levels",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });

        // Horizontal scroll for bar chart
        var scrollView = new ScrollView { Orientation = ScrollOrientation.Horizontal };
        var barContainer = new HorizontalStackLayout { Spacing = 16, Padding = new Thickness(8) };

        foreach (var game in _allGames)
        {
            var (level, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, game.GameId);
            barContainer.Children.Add(CreateGameBar(game.DisplayName, level, Color.FromArgb("#4CAF50")));
        }

        scrollView.Content = barContainer;
        stack.Children.Add(scrollView);

        // Legend
        stack.Children.Add(new Label
        {
            Text = "← Scroll horizontally if needed →",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        frame.Content = stack;
        return frame;
    }

    private Frame CreateMonthlyChart(DateTime month)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = $"📅 {month:MMMM yyyy}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

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

    private View CreateGameBar(string gameName, int level, Color barColor)
    {
        var stack = new VerticalStackLayout 
        { 
            Spacing = 4,
            WidthRequest = 80,
            HorizontalOptions = LayoutOptions.Center
        };

        // Level label on top
        stack.Children.Add(new Label
        {
            Text = $"Lv {level}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Bar container (fixed height, bar grows from bottom)
        var barFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HeightRequest = 120,
            WidthRequest = 50,
            HasShadow = false,
            HorizontalOptions = LayoutOptions.Center
        };

        // Inner bar (height based on level, max 100)
        double barHeight = Math.Min(level, 100) / 100.0 * 110; // Max 110px
        
        var innerBar = new BoxView
        {
            Color = barColor,
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
        grid.Children.Add(innerBar);

        barFrame.Content = grid;
        stack.Children.Add(barFrame);

        // Game name (truncated)
        string displayName = gameName.Length > 10 ? gameName.Substring(0, 8) + "..." : gameName;
        stack.Children.Add(new Label
        {
            Text = displayName,
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        });

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

    private Color GetMonthColor(DateTime month)
    {
        // Rotate through colors based on month
        var colors = new[]
        {
            Color.FromArgb("#2196F3"), // Blue
            Color.FromArgb("#9C27B0"), // Purple
            Color.FromArgb("#FF9800"), // Orange
            Color.FromArgb("#009688"), // Teal
            Color.FromArgb("#E91E63"), // Pink
            Color.FromArgb("#3F51B5"), // Indigo
            Color.FromArgb("#795548"), // Brown
            Color.FromArgb("#607D8B"), // Blue Grey
            Color.FromArgb("#8BC34A"), // Light Green
            Color.FromArgb("#FFC107"), // Amber
            Color.FromArgb("#00BCD4"), // Cyan
            Color.FromArgb("#FF5722"), // Deep Orange
        };

        return colors[month.Month % colors.Length];
    }
}
