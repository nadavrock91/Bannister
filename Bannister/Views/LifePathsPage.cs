using Bannister.Models;
using Bannister.Services;
using System.Text.Json;

namespace Bannister.Views;

public class LifePathsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _gameService;
    private readonly DatabaseService _db;
    private readonly LifePathService _lifePathService;
    private Label _statusLabel = null!;
    private Button _generateBtn = null!;
    private VerticalStackLayout _previewContainer = null!;

    public LifePathsPage(AuthService auth, GameService gameService,
        DatabaseService db, LifePathService? lifePathService = null)
    {
        _auth = auth;
        _gameService = gameService;
        _db = db;
        _lifePathService = lifePathService
            ?? Application.Current?.Handler?.MauiContext?.Services
                .GetService<LifePathService>()
            ?? throw new InvalidOperationException(
                "LifePathService not registered.");
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
            var (data, allGames) = await BuildTimelineDataAsync();
            var json = JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var jsonPath = Path.Combine(FileSystem.AppDataDirectory,
                "lifepaths_data.json");
            await File.WriteAllTextAsync(jsonPath, json);
            RenderPreview(data, allGames);
            var htmlPath = Path.Combine(FileSystem.AppDataDirectory,
                "lifepaths.html");
            await File.WriteAllTextAsync(htmlPath, BuildHtmlArtifact(json));
            _statusLabel.Text = $"✓ Timeline ready — {data.Games.Count} games, {data.TotalLogs} activity records";
            await Launcher.OpenAsync(new Uri($"file://{htmlPath}"));
        }
        catch (Exception ex) { _statusLabel.Text = $"Error: {ex.Message}"; }
        finally { _generateBtn.IsEnabled = true; }
    }

    private async Task<(TimelineData Data, List<Game> Games)> BuildTimelineDataAsync()
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
            var lifePathEnd = game.LifePathEndedAt?.ToLocalTime();
            var blocks = await _lifePathService.GetBlocksForGameAsync(
                username, game.GameId);
            rows.Add(new GameTimelineRow
            {
                GameId = game.GameId, DisplayName = game.DisplayName,
                StartDate = start, EndDate = lifePathEnd ?? end,
                EndedAt = lifePathEnd,
                EndReason = game.LifePathEndReason ?? "",
                IsActive = game.IsActive,
                TotalLogs = logs.Count, Periods = DetectPeriods(logs, start),
                MonthlyDensity = logs.GroupBy(l => new { l.LoggedAt.Year, l.LoggedAt.Month })
                    .ToDictionary(g => $"{g.Key.Year}-{g.Key.Month:D2}", g => g.Count()),
                SubBlocks = blocks.Select(b => new SubBlock
                {
                    Id = b.Id, ParentId = b.ParentBlockId, Label = b.Label,
                    Reason = b.Reason, StartDate = b.StartDate,
                    EndDate = b.EndDate, Level = b.Level, ColorHex = b.ColorHex
                }).ToList()
            });
        }
        return (new TimelineData
        {
            Username = username, GeneratedAt = DateTime.Now,
            Games = rows, TotalLogs = allLogs.Count
        }, allGames);
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

    private void RenderPreview(TimelineData data, List<Game> allGames)
    {
        foreach (var game in data.Games.OrderByDescending(g => g.TotalLogs))
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(160)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(60)),
                    new ColumnDefinition(new GridLength(34))
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
            var capturedGame = allGames.FirstOrDefault(g => g.GameId == game.GameId);
            var editBtn = new Button { Text = "✏️", FontSize = 11,
                HeightRequest = 24, WidthRequest = 30, Padding = 0,
                BackgroundColor = Color.FromArgb("#21262D"),
                TextColor = Color.FromArgb("#58A6FF") };
            editBtn.Clicked += async (_, _) =>
            {
                if (capturedGame == null) return;
                await Navigation.PushAsync(new LifePathEditorPage(
                    capturedGame, _lifePathService, _gameService, _auth));
                await GenerateAsync();
            };
            row.Add(editBtn, 3, 0);
            _previewContainer.Children.Add(row);
        }
    }

    private static string BuildHtmlArtifact(string json)
    {
        var encoded = JsonSerializer.Serialize(json);
        return $"<!doctype html><html><head><meta charset='utf-8'><title>Life Paths</title>" +
            "<style>body{background:#0D1117;color:#C9D1D9;font-family:sans-serif;padding:20px}" +
            "h1{color:#E6EDF3}.timeline{width:100%;overflow:auto}svg{min-width:800px}" +
            "</style></head><body><h1>Life Paths</h1><div class='timeline'><svg id='timeline'></svg></div><script>" +
            $"const data={encoded};const d=JSON.parse(data);" +
            "const svg=document.getElementById('timeline');const LABEL_W=180;const ROW_H=42;" +
            "const starts=d.games.map(g=>new Date(g.startDate));const ends=d.games.map(g=>new Date(g.endDate));" +
            "const min=Math.min(...starts),max=Math.max(...ends),span=Math.max(max-min,86400000),W=1000;" +
            "function toX(v){return LABEL_W+((new Date(v)-min)/span)*(W-LABEL_W-20)};" +
            "function esc(v){return String(v??'').replace(/&/g,'&amp;').replace(/</g,'&lt;')}" +
            "let svgContent='';d.games.forEach((game,i)=>{const y=i*ROW_H+24;" +
            "svgContent+=`<text x='${LABEL_W-8}' y='${y+8}' fill='#C9D1D9' text-anchor='end'>${esc(game.displayName)}</text>`;" +
            "const x0=toX(game.startDate),x1=Math.max(toX(game.endDate),x0+2);" +
            "svgContent+=`<rect x='${x0}' y='${y}' width='${x1-x0}' height='16' rx='3' fill='#1F6FEB' opacity='.45'/>`;" +
            "// Sub-blocks: level 1 and nested level 2 blocks are rendered below the game bar.\n" +
            "if(game.subBlocks&&game.subBlocks.length>0){game.subBlocks.filter(b=>b.level===1&&!b.parentId).forEach(block=>{" +
            "const bx0=toX(block.startDate),bx1=block.endDate?toX(block.endDate):toX(new Date()),bw=Math.max(bx1-bx0,2);" +
            "const subY=y+19,subH=8,blockColor=block.colorHex||'#388BFD';" +
            "svgContent+=`<rect x='${bx0}' y='${subY}' width='${bw}' height='${subH}' rx='2' fill='${blockColor}' opacity='.85' data-label='${esc(block.label)}'/>`;" +
            "if(bw>60)svgContent+=`<text x='${bx0+4}' y='${subY+subH-1}' fill='white' font-size='8'>${esc(block.label.substring(0,20))}</text>`;" +
            "game.subBlocks.filter(c=>c.parentId===block.id&&c.level===2).forEach(child=>{" +
            "const cx0=toX(child.startDate),cx1=child.endDate?toX(child.endDate):toX(new Date());" +
            "svgContent+=`<rect x='${cx0}' y='${subY+subH+2}' width='${Math.max(cx1-cx0,2)}' height='6' rx='2' fill='${child.colorHex||blockColor}' opacity='.6' data-label='${esc(child.label)}'/>`;" +
            "});});}" +
            "if(game.endedAt){const ex=toX(game.endedAt);svgContent+=`<line x1='${ex}' y1='${y-2}' x2='${ex}' y2='${y+20}' stroke='#F85149' stroke-width='2'/>`;}});" +
            "svg.setAttribute('width',W);svg.setAttribute('height',Math.max(80,d.games.length*ROW_H+30));svg.innerHTML=svgContent;" +
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
        public DateTime? EndedAt { get; set; }
        public string EndReason { get; set; } = "";
        public bool IsActive { get; set; }
        public int TotalLogs { get; set; }
        public List<ActivePeriod> Periods { get; set; } = new();
        public Dictionary<string, int> MonthlyDensity { get; set; } = new();
        public List<SubBlock> SubBlocks { get; set; } = new();
    }
    private class SubBlock
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Label { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Level { get; set; }
        public string ColorHex { get; set; } = "";
    }
    private class ActivePeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsGap { get; set; }
        public int LogCount { get; set; }
    }
}
