using Bannister.Models;
using Bannister.Services;
using System.Text.Json;

namespace Bannister.Views;

public class LifePathsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _gameService;
    private readonly DatabaseService _db;
    private Label _statusLabel = null!;
    private Button _generateBtn = null!;
    private VerticalStackLayout _previewContainer = null!;

    public LifePathsPage(AuthService auth, GameService gameService,
        DatabaseService db)
    {
        _auth = auth;
        _gameService = gameService;
        _db = db;
        Title = "Life Paths";
        BackgroundColor = Color.FromArgb("#0D1117");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await GenerateAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        stack.Children.Add(new Label
        {
            Text = " Life Paths", FontSize = 24,
            FontAttributes = FontAttributes.Bold, TextColor = Colors.White
        });
        stack.Children.Add(new Label
        {
            Text = "A visual history of your games over time — activity, gaps, and long-term patterns.",
            FontSize = 13, TextColor = Color.FromArgb("#8B949E"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        _statusLabel = new Label
        {
            Text = "Loading timeline data...", FontSize = 13,
            TextColor = Color.FromArgb("#58A6FF")
        };
        stack.Children.Add(_statusLabel);
        _generateBtn = new Button
        {
            Text = " Refresh Timeline",
            BackgroundColor = Color.FromArgb("#21262D"),
            TextColor = Color.FromArgb("#58A6FF"), CornerRadius = 8,
            FontSize = 13, HeightRequest = 40,
            BorderColor = Color.FromArgb("#30363D"), BorderWidth = 1
        };
        _generateBtn.Clicked += async (_, _) => await GenerateAsync();
        stack.Children.Add(_generateBtn);
        _previewContainer = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(new ScrollView { Content = _previewContainer });
        Content = new ScrollView { Content = stack };
    }

    private async Task GenerateAsync()
    {
        _statusLabel.Text = "Querying game history...";
        _generateBtn.IsEnabled = false;
        _previewContainer.Children.Clear();
        try
        {
            var data = await BuildTimelineDataAsync();
            var json = JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var jsonPath = Path.Combine(FileSystem.AppDataDirectory,
                "lifepaths_data.json");
            await File.WriteAllTextAsync(jsonPath, json);
            RenderPreview(data);
            var htmlPath = Path.Combine(FileSystem.AppDataDirectory,
                "lifepaths.html");
            await File.WriteAllTextAsync(htmlPath, BuildHtmlArtifact(json));
            _statusLabel.Text = $"✓ Timeline ready — {data.Games.Count} games, {data.TotalLogs} activity records";
            await Launcher.OpenAsync(new Uri($"file://{htmlPath}"));
        }
        catch (Exception ex) { _statusLabel.Text = $"Error: {ex.Message}"; }
        finally { _generateBtn.IsEnabled = true; }
    }

    private async Task<TimelineData> BuildTimelineDataAsync()
    {
        var username = _auth.CurrentUsername;
        var conn = await _db.GetConnectionAsync();
        var allGames = await conn.Table<Game>()
            .Where(g => g.Username == username).ToListAsync();
        var allLogs = await conn.Table<ExpLog>()
            .Where(e => e.Username == username).ToListAsync();
        var logsByGame = allLogs.GroupBy(e => e.Game).ToDictionary(
            g => g.Key, g => g.OrderBy(e => e.LoggedAt).ToList());
        var rows = new List<GameTimelineRow>();
        foreach (var game in allGames.OrderBy(g => g.CreatedAt))
        {
            var logs = logsByGame.TryGetValue(game.GameId, out var found)
                ? found : new List<ExpLog>();
            var start = game.CreatedAt.ToLocalTime();
            DateTime? last = logs.Count > 0
                ? logs[^1].LoggedAt.ToLocalTime() : null;
            var end = last ?? game.LastVisitedAt?.ToLocalTime() ?? start;
            rows.Add(new GameTimelineRow
            {
                GameId = game.GameId, DisplayName = game.DisplayName,
                StartDate = start, EndDate = end, IsActive = game.IsActive,
                TotalLogs = logs.Count, Periods = DetectPeriods(logs, start),
                MonthlyDensity = logs.GroupBy(l => new { l.LoggedAt.Year, l.LoggedAt.Month })
                    .ToDictionary(g => $"{g.Key.Year}-{g.Key.Month:D2}", g => g.Count())
            });
        }
        return new TimelineData
        {
            Username = username, GeneratedAt = DateTime.Now,
            Games = rows, TotalLogs = allLogs.Count
        };
    }

    private static List<ActivePeriod> DetectPeriods(
        List<ExpLog> logs, DateTime gameStart)
    {
        var periods = new List<ActivePeriod>();
        if (logs.Count == 0)
        {
            periods.Add(new ActivePeriod
            {
                Start = gameStart, End = gameStart, IsGap = true, LogCount = 0
            });
            return periods;
        }
        const int GapDays = 30;
        var sorted = logs.OrderBy(l => l.LoggedAt).ToList();
        var periodStart = sorted[0].LoggedAt.ToLocalTime();
        var periodEnd = periodStart;
        var count = 1;
        for (var i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i].LoggedAt.ToLocalTime();
            if ((current - periodEnd).TotalDays > GapDays)
            {
                periods.Add(new ActivePeriod { Start = periodStart,
                    End = periodEnd, IsGap = false, LogCount = count });
                periods.Add(new ActivePeriod { Start = periodEnd,
                    End = current, IsGap = true, LogCount = 0 });
                periodStart = current;
                periodEnd = current;
                count = 1;
            }
            else { periodEnd = current; count++; }
        }
        periods.Add(new ActivePeriod { Start = periodStart, End = periodEnd,
            IsGap = false, LogCount = count });
        return periods;
    }

    private void RenderPreview(TimelineData data)
    {
        foreach (var game in data.Games.OrderByDescending(g => g.TotalLogs))
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(160)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(60))
                }, ColumnSpacing = 8, Margin = new Thickness(0, 2)
            };
            row.Add(new Label { Text = game.DisplayName, FontSize = 12,
                TextColor = Colors.White, LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center }, 0, 0);
            row.Add(new BoxView { BackgroundColor = Color.FromArgb("#21262D"),
                HeightRequest = 18, CornerRadius = 3,
                VerticalOptions = LayoutOptions.Center }, 1, 0);
            row.Add(new Label { Text = game.TotalLogs.ToString(), FontSize = 11,
                TextColor = Color.FromArgb("#8B949E"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center }, 2, 0);
            _previewContainer.Children.Add(row);
        }
    }

    private static string BuildHtmlArtifact(string json)
    {
        var encoded = JsonSerializer.Serialize(json);
        return $"<!doctype html><html><head><meta charset='utf-8'><title>Life Paths</title>" +
            "<style>body{background:#0D1117;color:#C9D1D9;font-family:sans-serif;padding:20px}" +
            "h1{color:#E6EDF3}.game{margin:10px 0;padding:10px;background:#161B22;border-radius:6px}" +
            "</style></head><body><h1>Life Paths</h1><div id='app'></div><script>" +
            $"const data={encoded};const d=JSON.parse(data);document.getElementById('app').innerHTML=d.games.map(g=>`<div class='game'><b>${{g.displayName}}</b> — ${{g.totalLogs}} activity records</div>`).join('');" +
            "</script></body></html>";
    }

    private class TimelineData
    {
        public string Username { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public List<GameTimelineRow> Games { get; set; } = new();
        public int TotalLogs { get; set; }
    }
    private class GameTimelineRow
    {
        public string GameId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int TotalLogs { get; set; }
        public List<ActivePeriod> Periods { get; set; } = new();
        public Dictionary<string, int> MonthlyDensity { get; set; } = new();
    }
    private class ActivePeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsGap { get; set; }
        public int LogCount { get; set; }
    }
}
