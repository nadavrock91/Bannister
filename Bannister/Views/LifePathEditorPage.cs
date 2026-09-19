using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Graphics;

namespace Bannister.Views;

public class RowBarDrawable : IDrawable
{
    public DateTime RangeStart { get; set; }
    public double TotalSpanDays { get; set; } = 1;
    public float Width { get; set; }
    public float Height { get; set; } = 22;
    public Color BarColor { get; set; } = Color.FromArgb("#388BFD");
    public bool IsGameRow { get; set; }
    public bool IsSelected { get; set; }
    public DateTime BlockStart { get; set; }
    public DateTime? BlockEnd { get; set; }
    public List<PeriodData> Periods { get; set; } = new();
    public List<(DateTime Start, DateTime? End, Color BlockColor)> FocusSegments { get; set; } = new();
    private float X(DateTime d) => (float)((d - RangeStart).TotalDays / Math.Max(1d, TotalSpanDays) * Width);
    public void Draw(ICanvas canvas, RectF dirty)
    {
        canvas.FillColor = Color.FromArgb("#161B22"); canvas.FillRoundedRectangle(0, 2, Width, Height - 4, 3);
        if (IsGameRow)
        {
            foreach (var seg in FocusSegments)
            {
                float sx0 = Math.Max(0, X(seg.Start));
                float sx1 = Math.Min(Width, X(seg.End ?? DateTime.Now));
                if (sx1 <= sx0) continue;
                canvas.FillColor = seg.BlockColor.WithAlpha(0.35f);
                canvas.FillRoundedRectangle(sx0, 2, sx1 - sx0, Height - 4, 2);
            }
            foreach (var p in Periods)
            {
                if (p.IsGap) continue;
                float x0 = Math.Max(0, X(p.Start));
                float x1 = Math.Min(Width, X(p.End));
                if (x1 <= x0) continue;
                float alpha = Math.Min(0.9f, 0.2f + p.LogCount / 150f);
                canvas.FillColor = Colors.White.WithAlpha(alpha);
                canvas.FillRoundedRectangle(x0, 2, x1 - x0, 5, 2);
            }
            float tx = X(DateTime.Now);
            if (tx > 0 && tx < Width)
            {
                canvas.StrokeColor = Color.FromArgb("#3FB950");
                canvas.StrokeSize = 1.5f;
                canvas.DrawLine(tx, 0, tx, Height);
            }
        }
        else
        {
            canvas.FillColor = Color.FromArgb("#0D1117");
            canvas.FillRectangle(0, 0, Width, Height);
            float x0 = Math.Max(0, X(BlockStart));
            float x1 = Math.Min(Width, X(BlockEnd ?? DateTime.Now));
            if (x1 < x0 + 3) x1 = x0 + 3;
            canvas.FillColor = BarColor;
            canvas.FillRoundedRectangle(x0, 3, x1 - x0, Height - 6, 3);
            if (IsSelected) { canvas.StrokeColor = Colors.White; canvas.StrokeSize = 2f; canvas.DrawRoundedRectangle(x0, 3, x1 - x0, Height - 6, 3); }
            if (x1 - x0 > 60) { int days = (int)((BlockEnd ?? DateTime.Now) - BlockStart).TotalDays; string duration = days >= 365 ? $"{days / 365}y" : days >= 30 ? $"{days / 30}mo" : $"{days}d"; canvas.FontColor = Colors.White.WithAlpha(.7f); canvas.FontSize = 9; canvas.DrawString(duration, x0 + 4, 3, x1 - x0 - 6, Height - 6, HorizontalAlignment.Left, VerticalAlignment.Center); }
        }
        if (!IsGameRow)
        {
            float today = X(DateTime.Now);
            if (today > 0 && today < Width)
            {
                canvas.StrokeColor = Color.FromArgb("#3FB950").WithAlpha(.5f);
                canvas.StrokeSize = 1f;
                canvas.DrawLine(today, 0, today, Height);
            }
        }
    }
}

public class EditorAxisDrawable : IDrawable
{
    private readonly List<(float X, string Label)> _ticks = new(); private readonly float _width;
    public EditorAxisDrawable(DateTime start, DateTime end, double span, float width)
    {
        _width = width; float X(DateTime d) => (float)((d - start).TotalDays / Math.Max(1d, span) * width); bool decade = span > 3650; bool year = span > 730;
        if (decade) for (int y = start.Year / 10 * 10; y <= end.Year + 10; y += 10) Add(X(new DateTime(y, 1, 1)), y.ToString());
        else if (year) for (int y = start.Year; y <= end.Year + 1; y++) Add(X(new DateTime(y, 1, 1)), y.ToString());
        else for (var d = new DateTime(start.Year, start.Month, 1); d <= end; d = d.AddMonths(1)) Add(X(d), d.ToString("MMM yy"));
    }
    private void Add(float x, string label) { if (x >= 0 && x <= _width) _ticks.Add((x, label)); }
    public void Draw(ICanvas canvas, RectF dirty) { canvas.StrokeColor = Color.FromArgb("#21262D"); canvas.StrokeSize = 1; canvas.DrawLine(0, 19, _width, 19); foreach (var (x, label) in _ticks) { canvas.StrokeColor = Color.FromArgb("#30363D"); canvas.DrawLine(x, 13, x, 19); canvas.FontColor = Color.FromArgb("#8B949E"); canvas.FontSize = 9; canvas.DrawString(label, x + 2, 0, 52, 13, HorizontalAlignment.Left, VerticalAlignment.Bottom); } }
}

public class EditorBlock
{
    public int Id { get; set; } public int? ParentId { get; set; } public string GameId { get; set; } = ""; public string Label { get; set; } = ""; public string Reason { get; set; } = ""; public string AnalysisNote { get; set; } = ""; public string EvidenceEntryIds { get; set; } = "[]"; public DateTime StartDate { get; set; } public DateTime? EndDate { get; set; } public int Level { get; set; } public string ColorHex { get; set; } = "";
}

public class LifePathEditorPage : ContentPage
{
    private readonly Game _game; private readonly LifePathService _lifePathService; private readonly GameService _gameService; private readonly AuthService _auth; private readonly DatabaseService _db;
    private bool _isSaving; private int? _selectedBlockId; private VerticalStackLayout _timelineRows = null!; private VerticalStackLayout _detailPanel = null!; private List<EditorBlock> _blocks = new(); private List<PeriodData> _periods = new(); private DateTime _rangeStart, _rangeEnd; private double _totalSpanDays; private float _barW;
    private static readonly string[] Palette = { "#1F6FEB", "#F78166", "#3FB950", "#D2A8FF", "#FFA657", "#79C0FF", "#56D364", "#FF7B72", "#E3B341", "#58A6FF", "#BC8CFF", "#63E6BE" };
    public LifePathEditorPage(Game game, LifePathService lifePathService, GameService gameService, AuthService auth, DatabaseService? db = null)
    { _game = game; _lifePathService = lifePathService; _gameService = gameService; _auth = auth; _db = db ?? Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>() ?? throw new InvalidOperationException("DatabaseService not available"); Title = game.DisplayName; BackgroundColor = Color.FromArgb("#0D1117"); BuildUI(); }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private void BuildUI()
    {
        var page = new VerticalStackLayout { Padding = new Thickness(0, 12, 0, 32), Spacing = 0, BackgroundColor = Color.FromArgb("#0D1117") }; var header = new VerticalStackLayout { Padding = new Thickness(16, 0), Spacing = 4, Margin = new Thickness(0, 0, 0, 10) }; header.Children.Add(new Label { Text = _game.DisplayName, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }); var sub = new HorizontalStackLayout { Spacing = 12 }; sub.Children.Add(new Label { Text = $"Started {_game.CreatedAt.ToLocalTime():dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#8B949E") }); var end = new Button { Text = _game.LifePathEndedAt.HasValue ? $" Ended {_game.LifePathEndedAt.Value.ToLocalTime():dd MMM yy}" : " Set End Date", HeightRequest = 26, Padding = new Thickness(8, 0), BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#F85149") }; end.Clicked += async (_, _) => await SetEndDateAsync(end); sub.Children.Add(end); header.Children.Add(sub); page.Children.Add(header);
        _timelineRows = new VerticalStackLayout { Spacing = 3, BackgroundColor = Color.FromArgb("#0D1117") }; page.Children.Add(_timelineRows); page.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#21262D"), Margin = new Thickness(16, 12, 16, 10) }); _detailPanel = new VerticalStackLayout { Padding = new Thickness(16, 0), Spacing = 8 }; page.Children.Add(_detailPanel); var add = new Button { Text = "+ Add Focus Block", HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(16, 12, 0, 0), Padding = new Thickness(16, 0), BackgroundColor = Color.FromArgb("#1F6FEB"), TextColor = Colors.White }; add.Clicked += async (_, _) => await AddBlockAsync(null, 1); page.Children.Add(add); Content = new ScrollView { Content = page, BackgroundColor = Color.FromArgb("#0D1117") };
    }
    private async Task LoadAsync()
    {
        _timelineRows.Children.Clear(); var conn = await _db.GetConnectionAsync(); var logs = await conn.Table<ExpLog>().Where(e => e.Username == _auth.CurrentUsername && e.Game == _game.GameId).ToListAsync(); var start = _game.CreatedAt.ToLocalTime(); var last = logs.OrderBy(l => l.LoggedAt).LastOrDefault()?.LoggedAt.ToLocalTime(); var end = _game.LifePathEndedAt?.ToLocalTime() ?? last ?? _game.LastVisitedAt?.ToLocalTime() ?? start; _periods = BuildPeriods(logs, start); var raw = await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId); _blocks = raw.Select(b => new EditorBlock { Id = b.Id, ParentId = b.ParentBlockId, GameId = b.GameId, Label = b.Label, Reason = b.Reason, AnalysisNote = b.AnalysisNote, EvidenceEntryIds = b.EvidenceEntryIds, StartDate = b.StartDate, EndDate = b.EndDate, Level = b.Level, ColorHex = b.ColorHex }).ToList(); var dates = new List<DateTime> { start, end }; foreach (var b in _blocks) { dates.Add(b.StartDate); if (b.EndDate.HasValue) dates.Add(b.EndDate.Value); } _rangeStart = new DateTime(dates.Min().Year, 1, 1); _rangeEnd = new DateTime(Math.Max(dates.Max().Year, DateTime.Now.Year) + 1, 1, 1); _totalSpanDays = Math.Max(1, (_rangeEnd - _rangeStart).TotalDays); const float LabelW = 200f; const float Pad = 32f; double screenW = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density; _barW = Math.Max(160f, (float)screenW - LabelW - Pad); BuildTimelineRows(200f, start, end); RenderDetailPanel();
    }
    private void BuildTimelineRows(float labelW, DateTime gameStart, DateTime gameEnd)
    {
        _timelineRows.Children.Clear();
        var axis = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(labelW)), new ColumnDefinition(new GridLength(_barW)) } }; axis.Add(new Label { Text = "" }, 0, 0); axis.Add(new GraphicsView { Drawable = new EditorAxisDrawable(_rangeStart, _rangeEnd, _totalSpanDays, _barW), HeightRequest = 22, WidthRequest = _barW }, 1, 0); _timelineRows.Children.Add(axis);
        Color gameColor = Color.FromArgb("#6E7681"); var level1 = _blocks.Where(b => b.ParentId == null).OrderBy(b => b.StartDate).ToList();
        var blockColors = new Dictionary<int, Color>(); for (int i = 0; i < level1.Count; i++) { var b = level1[i]; blockColors[b.Id] = string.IsNullOrWhiteSpace(b.ColorHex) ? Color.FromArgb(Palette[i % Palette.Length]) : Color.FromArgb(b.ColorHex); }
        int childColorIdx = level1.Count; foreach (var child in _blocks.Where(b => b.ParentId != null).OrderBy(b => b.StartDate)) { if (!string.IsNullOrWhiteSpace(child.ColorHex)) { try { blockColors[child.Id] = Color.FromArgb(child.ColorHex); continue; } catch { } } blockColors[child.Id] = Color.FromArgb(Palette[childColorIdx % Palette.Length]); childColorIdx++; }
        var focusSegments = level1.Select(b => (b.StartDate, b.EndDate, blockColors[b.Id])).ToList();
        _timelineRows.Children.Add(BuildRow(_game.DisplayName, Colors.White, true, true, gameStart, gameEnd, gameColor, null, 30, focusSegments));
        if (level1.Count == 0) { _timelineRows.Children.Add(new Label { Text = "  No focus blocks yet — tap + to add", FontSize = 12, TextColor = Color.FromArgb("#484F58"), FontAttributes = FontAttributes.Italic, Margin = new Thickness(16, 8) }); return; } _timelineRows.Children.Add(new BoxView { HeightRequest = 4, BackgroundColor = Color.FromArgb("#0D1117") });
        for (int i = 0; i < level1.Count; i++) { var b = level1[i]; var color = blockColors[b.Id]; int bDays = (int)((b.EndDate ?? DateTime.Now) - b.StartDate).TotalDays; string bDur = bDays >= 365 ? $"{bDays / 365}y {bDays % 365}d" : $"{bDays}d"; string bLabel = b.Label; float bH = ComputeRowHeight(bLabel, 200f, 12f, 28f); _timelineRows.Children.Add(BuildRow(bLabel, Colors.White, _selectedBlockId == b.Id, false, b.StartDate, b.EndDate, color, b.Id, bH, null, bDur)); foreach (var c in _blocks.Where(c => c.ParentId == b.Id).OrderBy(c => c.StartDate)) { int cDays = (int)((c.EndDate ?? DateTime.Now) - c.StartDate).TotalDays; string cDur = cDays >= 365 ? $"{cDays / 365}y {cDays % 365}d" : $"{cDays}d"; string cLabel = $"  ↳ {c.Label}"; float cH = ComputeRowHeight(cLabel, 186f, 11f, 24f); var childColor = blockColors.TryGetValue(c.Id, out var cc) ? cc : color; _timelineRows.Children.Add(BuildRow(cLabel, Color.FromArgb("#C9D1D9"), _selectedBlockId == c.Id, false, c.StartDate, c.EndDate, childColor, c.Id, cH, null, cDur)); } }
    }
    private static float ComputeRowHeight(string label, float labelW, float baseFontSize, float minH) { float charsPerLine = labelW / (baseFontSize * 0.62f); int lines = (int)Math.Ceiling(label.Length / charsPerLine); lines = Math.Max(1, lines); float h = lines * (baseFontSize + 6) + 10; return Math.Max(minH, h); }
    private View BuildRow(string label, Color labelColor, bool selected, bool gameRow, DateTime start, DateTime? end, Color color, int? blockId, float height, List<(DateTime, DateTime?, Color)>? focusSegments = null, string subtitle = "")
    {
        var drawable = new RowBarDrawable { RangeStart = _rangeStart, TotalSpanDays = _totalSpanDays, Width = _barW, Height = height, BarColor = color, IsGameRow = gameRow, IsSelected = selected, BlockStart = start, BlockEnd = end, Periods = _periods, FocusSegments = focusSegments ?? new() }; var canvas = new GraphicsView { Drawable = drawable, HeightRequest = height, WidthRequest = _barW }; if (blockId.HasValue) { var id = blockId.Value; var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => { _selectedBlockId = _selectedBlockId == id ? null : id; BuildTimelineRows(200f, _game.CreatedAt.ToLocalTime(), _game.LifePathEndedAt?.ToLocalTime() ?? DateTime.Now); RenderDetailPanel(); }; canvas.GestureRecognizers.Add(tap); } var row = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(200)), new ColumnDefinition(new GridLength(_barW)) }, Margin = new Thickness(0, 1, 0, 1) }; var labelStack = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(16, 4, 4, 4) }; labelStack.Children.Add(new Label { Text = label, FontSize = gameRow ? 12 : 12, FontAttributes = selected || gameRow ? FontAttributes.Bold : FontAttributes.None, TextColor = labelColor, LineBreakMode = LineBreakMode.WordWrap }); if (!string.IsNullOrWhiteSpace(subtitle)) labelStack.Children.Add(new Label { Text = subtitle, FontSize = 10, TextColor = Color.FromArgb("#6E7681"), LineBreakMode = LineBreakMode.NoWrap }); row.Add(labelStack, 0, 0); row.Add(canvas, 1, 0); return row;
    }
    private async void RenderDetailPanel()
    { _detailPanel.Children.Clear(); if (_selectedBlockId == null) { _detailPanel.Children.Add(new Label { Text = "Tap a focus row to see details, edit, or add a sub-block.", FontSize = 12, TextColor = Color.FromArgb("#484F58"), FontAttributes = FontAttributes.Italic }); return; } var b = _blocks.FirstOrDefault(x => x.Id == _selectedBlockId); if (b == null) return; var inner = new VerticalStackLayout { Spacing = 6 }; inner.Children.Add(new Label { Text = b.Label, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }); int days = (int)((b.EndDate ?? DateTime.Now) - b.StartDate).TotalDays; var dateRow = new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 2, 0, 2) }; var startBtn = new Button { Text = $"▶ {b.StartDate:dd MMM yyyy}", BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#58A6FF"), CornerRadius = 6, FontSize = 11, HeightRequest = 28, Padding = new Thickness(8, 0), BorderColor = Color.FromArgb("#30363D"), BorderWidth = 1 }; startBtn.Clicked += async (_, _) => { string? ds = await DisplayPromptAsync("Start Date", "New start date (DD/MM/YYYY):", "Save", "Cancel", initialValue: b.StartDate.ToString("dd/MM/yyyy")); if (string.IsNullOrWhiteSpace(ds)) return; if (!DateTime.TryParseExact(ds, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime nd)) { await DisplayAlert("Invalid", "Use DD/MM/YYYY.", "OK"); return; } var raw = (await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId)).FirstOrDefault(x => x.Id == b.Id); if (raw == null) return; raw.StartDate = nd; await _lifePathService.UpdateAsync(raw); await LoadAsync(); }; dateRow.Children.Add(startBtn); var endBtn = new Button { Text = b.EndDate.HasValue ? $"◼ {b.EndDate.Value:dd MMM yyyy}" : "◼ ongoing", BackgroundColor = Color.FromArgb("#21262D"), TextColor = b.EndDate.HasValue ? Color.FromArgb("#58A6FF") : Color.FromArgb("#3FB950"), CornerRadius = 6, FontSize = 11, HeightRequest = 28, Padding = new Thickness(8, 0), BorderColor = Color.FromArgb("#30363D"), BorderWidth = 1 }; endBtn.Clicked += async (_, _) => { string? ds = await DisplayPromptAsync("End Date", "End date (DD/MM/YYYY) or leave empty for ongoing:", "Save", "Cancel", initialValue: b.EndDate.HasValue ? b.EndDate.Value.ToString("dd/MM/yyyy") : ""); if (ds == null) return; var raw = (await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId)).FirstOrDefault(x => x.Id == b.Id); if (raw == null) return; if (string.IsNullOrWhiteSpace(ds)) raw.EndDate = null; else if (DateTime.TryParseExact(ds, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime nd)) raw.EndDate = nd; else { await DisplayAlert("Invalid", "Use DD/MM/YYYY.", "OK"); return; } await _lifePathService.UpdateAsync(raw); await LoadAsync(); }; dateRow.Children.Add(endBtn); dateRow.Children.Add(new Label { Text = $"{days}d", FontSize = 11, TextColor = Color.FromArgb("#484F58"), VerticalOptions = LayoutOptions.Center }); inner.Children.Add(dateRow); if (!string.IsNullOrWhiteSpace(b.Reason)) inner.Children.Add(new Label { Text = b.Reason, FontSize = 12, TextColor = Color.FromArgb("#8B949E") }); var edit = new Button { Text = "✏️ Edit", HeightRequest = 34, BackgroundColor = Color.FromArgb("#21262D"), TextColor = Colors.White }; edit.Clicked += async (_, _) => await EditBlockAsync(b); var buttons = new HorizontalStackLayout { Spacing = 8 }; buttons.Children.Add(edit); if (b.Level < 2) { var sub = new Button { Text = "+ Sub-block", HeightRequest = 34, BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#58A6FF") }; sub.Clicked += async (_, _) => await AddBlockAsync(b.Id, b.Level + 1); buttons.Children.Add(sub); } var rawForPrompt = (await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId)).FirstOrDefault(x => x.Id == b.Id); bool promptOn = rawForPrompt?.HomePromptEnabled ?? false; var promptBtn = new Button { Text = promptOn ? " Check-in: ON" : " Check-in: OFF", BackgroundColor = promptOn ? Color.FromArgb("#2D3A2E") : Color.FromArgb("#21262D"), TextColor = promptOn ? Color.FromArgb("#3FB950") : Color.FromArgb("#484F58"), CornerRadius = 6, FontSize = 11, HeightRequest = 34, Padding = new Thickness(10, 0), BorderColor = promptOn ? Color.FromArgb("#3FB950") : Color.FromArgb("#30363D"), BorderWidth = 1 }; promptBtn.Clicked += async (_, _) => { var raw2 = (await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId)).FirstOrDefault(x => x.Id == b.Id); if (raw2 == null) return; raw2.HomePromptEnabled = !raw2.HomePromptEnabled; await _lifePathService.UpdateAsync(raw2); await LoadAsync(); }; buttons.Children.Add(promptBtn); var del = new Button { Text = "", HeightRequest = 34, WidthRequest = 40, BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#F85149") }; del.Clicked += async (_, _) => { if (await DisplayAlert("Delete", $"Delete \"{b.Label}\" and sub-blocks?", "Delete", "Cancel")) { await _lifePathService.DeleteAsync(b.Id); _selectedBlockId = null; await LoadAsync(); } }; buttons.Children.Add(del); inner.Children.Add(buttons); _detailPanel.Children.Add(new Frame { Content = inner, Padding = 14, BackgroundColor = Color.FromArgb("#161B22"), BorderColor = Color.FromArgb("#30363D"), CornerRadius = 8, HasShadow = false }); }
    private static List<PeriodData> BuildPeriods(List<ExpLog> logs, DateTime start) { var result = new List<PeriodData>(); if (logs.Count == 0) { result.Add(new PeriodData(start, start, true, 0)); return result; } var s = logs.OrderBy(l => l.LoggedAt).ToList(); var ps = s[0].LoggedAt.ToLocalTime(); var pe = ps; int count = 1; for (int i = 1; i < s.Count; i++) { var cur = s[i].LoggedAt.ToLocalTime(); if ((cur - pe).TotalDays > 30) { result.Add(new PeriodData(ps, pe, false, count)); result.Add(new PeriodData(pe, cur, true, 0)); ps = cur; pe = cur; count = 1; } else { pe = cur; count++; } } result.Add(new PeriodData(ps, pe, false, count)); return result; }
    private async Task SetEndDateAsync(Button btn) { var text = await DisplayPromptAsync("End Date", "DD/MM/YYYY or empty to clear:", "Save", "Cancel", initialValue: _game.LifePathEndedAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? ""); if (text == null) return; if (string.IsNullOrWhiteSpace(text)) { _game.LifePathEndedAt = null; _game.LifePathEndReason = ""; } else if (DateTime.TryParseExact(text, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) { _game.LifePathEndedAt = date; _game.LifePathEndReason = await DisplayPromptAsync("Reason", "Optional:", "Save", "Skip") ?? ""; } else { await DisplayAlert("Invalid Date", "Use DD/MM/YYYY.", "OK"); return; } await _gameService.UpdateGameAsync(_game); await LoadAsync(); }
    private async Task AddBlockAsync(int? parentId, int level) { var label = await DisplayPromptAsync(level == 1 ? "New Focus Block" : "New Sub-Focus", "Label:", "Next", "Cancel"); if (string.IsNullOrWhiteSpace(label)) return; var st = await DisplayPromptAsync("Start Date", "DD/MM/YYYY:", "Next", "Cancel", initialValue: DateTime.Today.ToString("dd/MM/yyyy")); if (st == null || !DateTime.TryParseExact(st, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)) return; var et = await DisplayPromptAsync("End Date", "Optional DD/MM/YYYY:", "Save", "Skip"); DateTime? end = DateTime.TryParseExact(et, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null; var reason = await DisplayPromptAsync("Reason", "Optional:", "Save", "Skip"); if (_isSaving) return; _isSaving = true; try { await _lifePathService.CreateAsync(_auth.CurrentUsername, _game.GameId, label.Trim(), start, end, reason ?? "", parentId, level); await LoadAsync(); } finally { _isSaving = false; } }
    private async Task EditBlockAsync(EditorBlock block) { var label = await DisplayPromptAsync("Edit Label", "Label:", "Next", "Cancel", initialValue: block.Label); if (label == null) return; var st = await DisplayPromptAsync("Start Date", "DD/MM/YYYY:", "Next", "Cancel", initialValue: block.StartDate.ToString("dd/MM/yyyy")); if (st == null || !DateTime.TryParseExact(st, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)) return; var et = await DisplayPromptAsync("End Date", "Optional DD/MM/YYYY:", "Save", "Skip", initialValue: block.EndDate?.ToString("dd/MM/yyyy") ?? ""); DateTime? end = DateTime.TryParseExact(et, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null; var reason = await DisplayPromptAsync("Reason", "Reason:", "Save", "Skip", initialValue: block.Reason); var raw = (await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId)).FirstOrDefault(x => x.Id == block.Id); if (raw == null) return; if (!string.IsNullOrWhiteSpace(label)) raw.Label = label.Trim(); raw.StartDate = start; raw.EndDate = end; if (reason != null) raw.Reason = reason.Trim(); await _lifePathService.UpdateAsync(raw); await LoadAsync(); }
}
