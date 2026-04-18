using Bannister.Models;
using Bannister.Services;
using Bannister.Drawables;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

/// <summary>
/// Shows Level over time chart for each game in a grid of square cards
/// </summary>
public class LevelOverTimeChartsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly DatabaseService _db;

    private FlexLayout _chartsGrid;

    public LevelOverTimeChartsPage(AuthService auth, GameService games, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _db = db;

        Title = "Level Over Time";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadChartsAsync();
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
            Text = "🎚️ Level Over Time",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        mainStack.Children.Add(new Label
        {
            Text = "Level progression for each game",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Charts grid using FlexLayout for wrapping
        _chartsGrid = new FlexLayout
        {
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start
        };
        mainStack.Children.Add(_chartsGrid);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async Task LoadChartsAsync()
    {
        _chartsGrid.Children.Clear();

        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);

        if (allGames.Count == 0)
        {
            _chartsGrid.Children.Add(new Label
            {
                Text = "No games found. Create a game to see charts!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        foreach (var game in allGames)
        {
            var chartCard = await CreateGameChartCardAsync(game);
            _chartsGrid.Children.Add(chartCard);
        }
    }

    private async Task<Frame> CreateGameChartCardAsync(Game game)
    {
        // Bigger card size
        double cardWidth = 280;
        double cardHeight = 240;

        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            WidthRequest = cardWidth,
            HeightRequest = cardHeight,
            Margin = new Thickness(8)
        };

        var stack = new VerticalStackLayout { Spacing = 6 };

        // Game title
        stack.Children.Add(new Label
        {
            Text = game.DisplayName,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        });

        // Load chart data
        var logs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, game.GameId);

        if (logs.Count == 0)
        {
            stack.Children.Add(new Label
            {
                Text = "No data",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                VerticalOptions = LayoutOptions.CenterAndExpand,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
        else
        {
            // Calculate level data points
            var chartData = CalculateLevelChartData(logs);
            
            // Current level
            int currentLevel = chartData.Count > 0 ? chartData.Last().Value : 1;

            stack.Children.Add(new Label
            {
                Text = $"Level {currentLevel}",
                FontSize = 14,
                TextColor = Color.FromArgb("#4CAF50"),
                FontAttributes = FontAttributes.Bold
            });

            // Chart - takes most of the space
            var drawable = new LevelChartCompactDrawable(chartData);
            var graphicsView = new GraphicsView
            {
                Drawable = drawable,
                HeightRequest = cardHeight - 70,
                WidthRequest = cardWidth - 30,
                BackgroundColor = Colors.Transparent,
                VerticalOptions = LayoutOptions.FillAndExpand
            };
            stack.Children.Add(graphicsView);
        }

        frame.Content = stack;
        return frame;
    }

    private List<ChartDataPoint> CalculateLevelChartData(List<ExpLog> logs)
    {
        if (logs.Count == 0)
            return new List<ChartDataPoint>();

        // Group by day and get max level reached that day
        var dailyData = logs
            .GroupBy(l => l.LoggedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key, Level = g.Max(l => l.LevelAfter) })
            .ToList();

        return dailyData
            .Select(d => new ChartDataPoint { Date = d.Date, Value = d.Level })
            .ToList();
    }
}
