using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Graphics;

namespace Bannister.Views;

public class EditorBlock
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string GameId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Reason { get; set; } = "";
    public string AnalysisNote { get; set; } = "";
    public string EvidenceEntryIds { get; set; } = "[]";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Level { get; set; }
    public string ColorHex { get; set; } = "";
}

public class EditorTimelineDrawable : IDrawable
{
    public DateTime RangeStart { get; set; }
    public double TotalSpanDays { get; set; } = 1;
    public float Width { get; set; }
    public float GameBarH { get; set; } = 18;
    public float BlockH { get; set; } = 12;
    public float BlockGap { get; set; } = 3;
    public float IndentW { get; set; } = 14;
    public Color GameColor { get; set; } = Color.FromArgb("#388BFD");
    public List<PeriodData> Periods { get; set; } = new();
    public List<EditorBlock> Blocks { get; set; } = new();
    public int? SelectedBlockId { get; set; }
    public Dictionary<int, RectF> BlockRects { get; } = new();
    private float X(DateTime d) => (float)((d - RangeStart).TotalDays / Math.Max(1, TotalSpanDays) * Width);
    public float TotalHeight => GameBarH + BlockGap + Blocks.Where(b => b.ParentId == null).Sum(b => BlockH + BlockGap + Blocks.Count(c => c.ParentId == b.Id) * (BlockH + BlockGap)) + 8;
    public void Draw(ICanvas canvas, RectF dirty)
    {
        BlockRects.Clear(); canvas.FillColor = Color.FromArgb("#1C2128"); canvas.FillRoundedRectangle(0, 0, Width, GameBarH, 3);
        foreach (var p in Periods.Where(p => !p.IsGap)) { var x0 = Math.Max(0, X(p.Start)); var x1 = Math.Min(Width, X(p.End)); if (x1 <= x0) continue; canvas.FillColor = GameColor.WithAlpha(Math.Min(1f, .35f + p.LogCount / 200f)); canvas.FillRoundedRectangle(x0, 0, x1 - x0, GameBarH, 2); }
        var today = X(DateTime.Now); if (today > 0 && today < Width) { canvas.StrokeColor = Color.FromArgb("#3FB950"); canvas.StrokeSize = 1.5f; canvas.DrawLine(today, 0, today, GameBarH); }
        float y = GameBarH + BlockGap + 4;
        foreach (var b in Blocks.Where(b => b.ParentId == null).OrderBy(b => b.StartDate)) { DrawBlock(canvas, b, ref y, false); foreach (var c in Blocks.Where(c => c.ParentId == b.Id).OrderBy(c => c.StartDate)) DrawBlock(canvas, c, ref y, true); }
    }
    private void DrawBlock(ICanvas canvas, EditorBlock b, ref float y, bool child)
    {
        float left = child ? IndentW : 0, x0 = Math.Max(left, X(b.StartDate)), x1 = Math.Min(Width, X(b.EndDate ?? DateTime.Now)); if (x1 < x0) x1 = x0 + 4;
        canvas.FillColor = Color.FromArgb(child ? "#0D1117" : "#161B22"); canvas.FillRectangle(left, y, Width - left, BlockH);
        if (child) { canvas.StrokeColor = Color.FromArgb("#30363D"); canvas.StrokeSize = 1; canvas.DrawLine(IndentW / 2, y - BlockGap / 2, IndentW / 2, y + BlockH / 2); canvas.DrawLine(IndentW / 2, y + BlockH / 2, IndentW, y + BlockH / 2); }
        var color = BlockColor(b, child); canvas.FillColor = SelectedBlockId == b.Id ? color : color.WithAlpha(child ? .65f : .75f); canvas.FillRoundedRectangle(x0, y, x1 - x0, BlockH, 2);
        if (SelectedBlockId == b.Id) { canvas.StrokeColor = Colors.White; canvas.StrokeSize = 1.5f; canvas.DrawRoundedRectangle(x0, y, x1 - x0, BlockH, 2); }
        if (x1 - x0 > (child ? 24 : 30)) { canvas.FontColor = Colors.White.WithAlpha(.9f); canvas.FontSize = child ? 7 : 8; var text = b.Label.Length > (child ? 22 : 28) ? b.Label[..(child ? 19 : 25)] + "…" : b.Label; canvas.DrawString(text, x0 + 3, y, x1 - x0 - 4, BlockH, HorizontalAlignment.Left, VerticalAlignment.Center); }
        BlockRects[b.Id] = new RectF(0, y, Width, BlockH); y += BlockH + BlockGap;
    }
    private static Color BlockColor(EditorBlock b, bool child)
    { if (!string.IsNullOrWhiteSpace(b.ColorHex)) try { return Color.FromArgb(b.ColorHex); } catch { } var hue = Math.Abs((b.GameId + b.Label).GetHashCode()) % 360; return Color.FromHsv(hue / 360f, child ? .5f : .65f, child ? .65f : .75f); }
}

public class LifePathEditorPage : ContentPage
{
    private readonly Game _game; private readonly LifePathService _lifePathService; private readonly GameService _gameService; private readonly AuthService _auth; private readonly DatabaseService _db;
    private bool _isSaving; private int? _selectedBlockId; private GraphicsView _timelineCanvas = null!; private EditorTimelineDrawable _drawable = null!; private VerticalStackLayout _detailPanel = null!; private List<EditorBlock> _blocks = new(); private List<PeriodData> _periods = new();
    public LifePathEditorPage(Game game, LifePathService lifePathService, GameService gameService, AuthService auth, DatabaseService? db = null)
    { _game = game; _lifePathService = lifePathService; _gameService = gameService; _auth = auth; _db = db ?? Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>() ?? throw new InvalidOperationException("DatabaseService not available"); Title = game.DisplayName; BackgroundColor = Color.FromArgb("#0D1117"); BuildUI(); }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private void BuildUI()
    {
        var page = new VerticalStackLayout { Padding = new Thickness(16, 16, 16, 24), Spacing = 10, BackgroundColor = Color.FromArgb("#0D1117") };
        page.Children.Add(new Label { Text = _game.DisplayName, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }); page.Children.Add(new Label { Text = $"Started {_game.CreatedAt.ToLocalTime():dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#8B949E") });
        var endRow = new HorizontalStackLayout { Spacing = 8 }; var endLabel = new Label { Text = _game.LifePathEndedAt.HasValue ? $"Ended {_game.LifePathEndedAt.Value.ToLocalTime():dd MMM yyyy}" : "No chapter end set", FontSize = 11, TextColor = _game.LifePathEndedAt.HasValue ? Color.FromArgb("#F85149") : Color.FromArgb("#8B949E") }; endRow.Children.Add(endLabel); var endBtn = new Button { Text = _game.LifePathEndedAt.HasValue ? "✏️" : " Set End", HeightRequest = 28, Padding = new Thickness(8, 0), BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#F85149") }; endBtn.Clicked += async (_, _) => await SetEndDateAsync(endLabel); endRow.Children.Add(endBtn); page.Children.Add(endRow);
        page.Children.Add(new Label { Text = "TIMELINE", FontSize = 9, TextColor = Color.FromArgb("#484F58") }); _drawable = new EditorTimelineDrawable(); _timelineCanvas = new GraphicsView { Drawable = _drawable, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Fill }; var tap = new TapGestureRecognizer(); tap.Tapped += OnTimelineTapped; _timelineCanvas.GestureRecognizers.Add(tap); page.Children.Add(_timelineCanvas);
        page.Children.Add(new Label { Text = "SELECTED BLOCK", FontSize = 9, TextColor = Color.FromArgb("#484F58") }); _detailPanel = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(0, 4) }; page.Children.Add(_detailPanel); var add = new Button { Text = "+ Add Focus Block", HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, Padding = new Thickness(16, 0), BackgroundColor = Color.FromArgb("#1F6FEB"), TextColor = Colors.White }; add.Clicked += async (_, _) => await AddBlockAsync(null, 1); page.Children.Add(add); Content = new ScrollView { Content = page, BackgroundColor = Color.FromArgb("#0D1117") };
    }
    private async Task LoadAsync()
    {
        var conn = await _db.GetConnectionAsync(); var logs = await conn.Table<ExpLog>().Where(e => e.Username == _auth.CurrentUsername && e.Game == _game.GameId).ToListAsync(); var start = _game.CreatedAt.ToLocalTime(); var last = logs.OrderBy(l => l.LoggedAt).LastOrDefault()?.LoggedAt.ToLocalTime(); var end = last ?? _game.LastVisitedAt?.ToLocalTime() ?? start; _periods = BuildPeriods(logs, start);
        var raw = await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId); _blocks = raw.Select(b => new EditorBlock { Id = b.Id, ParentId = b.ParentBlockId, GameId = b.GameId, Label = b.Label, Reason = b.Reason, AnalysisNote = b.AnalysisNote, EvidenceEntryIds = b.EvidenceEntryIds, StartDate = b.StartDate, EndDate = b.EndDate, Level = b.Level, ColorHex = b.ColorHex }).ToList(); var dates = new List<DateTime> { start, end }; foreach (var b in _blocks) { dates.Add(b.StartDate); if (b.EndDate.HasValue) dates.Add(b.EndDate.Value); }
        var rangeStart = new DateTime(dates.Min().Year, 1, 1); var rangeEnd = new DateTime(Math.Max(dates.Max().Year, DateTime.Now.Year) + 1, 1, 1); var span = Math.Max(1, (rangeEnd - rangeStart).TotalDays); var width = (float)(DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 32; _drawable.RangeStart = rangeStart; _drawable.TotalSpanDays = span; _drawable.Width = width; _drawable.Periods = _periods; _drawable.Blocks = _blocks; _drawable.SelectedBlockId = _selectedBlockId; _timelineCanvas.HeightRequest = _drawable.TotalHeight; _timelineCanvas.WidthRequest = width; _timelineCanvas.Invalidate(); RenderDetailPanel();
    }
    private void OnTimelineTapped(object? sender, TappedEventArgs e)
    { if (e.GetPosition(_timelineCanvas) is not Point pt) return; foreach (var (id, rect) in _drawable.BlockRects) if (pt.Y >= rect.Top && pt.Y <= rect.Bottom) { _selectedBlockId = _selectedBlockId == id ? null : id; _drawable.SelectedBlockId = _selectedBlockId; _timelineCanvas.Invalidate(); RenderDetailPanel(); return; } _selectedBlockId = null; _drawable.SelectedBlockId = null; _timelineCanvas.Invalidate(); RenderDetailPanel(); }
    private void RenderDetailPanel()
    { _detailPanel.Children.Clear(); if (_selectedBlockId == null) { _detailPanel.Children.Add(new Label { Text = "Tap a block to select it.", FontSize = 12, TextColor = Color.FromArgb("#484F58"), FontAttributes = FontAttributes.Italic }); return; } var block = _blocks.FirstOrDefault(b => b.Id == _selectedBlockId); if (block == null) return; var inner = new VerticalStackLayout { Spacing = 6 }; inner.Children.Add(new Label { Text = block.Label, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }); var days = (int)((block.EndDate ?? DateTime.Now) - block.StartDate).TotalDays; inner.Children.Add(new Label { Text = $"{block.StartDate:dd MMM yyyy} → {(block.EndDate.HasValue ? block.EndDate.Value.ToString("dd MMM yyyy") : "ongoing")} ({days}d)", FontSize = 12, TextColor = Color.FromArgb("#58A6FF") }); if (!string.IsNullOrWhiteSpace(block.Reason)) inner.Children.Add(new Label { Text = block.Reason, FontSize = 12, TextColor = Color.FromArgb("#8B949E") }); if (!string.IsNullOrWhiteSpace(block.AnalysisNote)) inner.Children.Add(new Label { Text = " " + block.AnalysisNote, FontSize = 11, TextColor = Color.FromArgb("#6E7681") }); var buttons = new HorizontalStackLayout { Spacing = 8 }; var edit = new Button { Text = "✏️ Edit", HeightRequest = 32, BackgroundColor = Color.FromArgb("#21262D"), TextColor = Colors.White }; edit.Clicked += async (_, _) => await EditBlockAsync(block); buttons.Children.Add(edit); if (block.Level < 2) { var sub = new Button { Text = "+ Sub-block", HeightRequest = 32, BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#58A6FF") }; sub.Clicked += async (_, _) => await AddBlockAsync(block.Id, block.Level + 1); buttons.Children.Add(sub); } var del = new Button { Text = "", HeightRequest = 32, WidthRequest = 36, BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#F85149") }; del.Clicked += async (_, _) => { if (!await DisplayAlert("Delete", $"Delete \"{block.Label}\" and all its sub-blocks?", "Delete", "Cancel")) return; await _lifePathService.DeleteAsync(block.Id); _selectedBlockId = null; await LoadAsync(); }; buttons.Children.Add(del); inner.Children.Add(buttons); _detailPanel.Children.Add(new Frame { Content = inner, Padding = 12, BackgroundColor = Color.FromArgb("#161B22"), BorderColor = Color.FromArgb("#30363D"), CornerRadius = 8, HasShadow = false }); }
    private static List<PeriodData> BuildPeriods(List<ExpLog> logs, DateTime start)
    { var result = new List<PeriodData>(); if (logs.Count == 0) { result.Add(new PeriodData(start, start, true, 0)); return result; } var sorted = logs.OrderBy(l => l.LoggedAt).ToList(); var ps = sorted[0].LoggedAt.ToLocalTime(); var pe = ps; var count = 1; for (var i = 1; i < sorted.Count; i++) { var current = sorted[i].LoggedAt.ToLocalTime(); if ((current - pe).TotalDays > 30) { result.Add(new PeriodData(ps, pe, false, count)); result.Add(new PeriodData(pe, current, true, 0)); ps = current; pe = current; count = 1; } else { pe = current; count++; } } result.Add(new PeriodData(ps, pe, false, count)); return result; }
    private async Task SetEndDateAsync(Label label)
    { var value = await DisplayPromptAsync("End Date", "Enter end date (DD/MM/YYYY) or leave empty to clear:", "Save", "Cancel", initialValue: _game.LifePathEndedAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? ""); if (value == null) return; if (string.IsNullOrWhiteSpace(value)) { _game.LifePathEndedAt = null; _game.LifePathEndReason = ""; } else if (DateTime.TryParseExact(value, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) { _game.LifePathEndedAt = date; _game.LifePathEndReason = await DisplayPromptAsync("Reason", "Optional reason:", "Save", "Skip", initialValue: _game.LifePathEndReason) ?? ""; } else { await DisplayAlert("Invalid Date", "Use DD/MM/YYYY.", "OK"); return; } await _gameService.UpdateGameAsync(_game); await LoadAsync(); }
    private async Task AddBlockAsync(int? parentId, int level)
    { var label = await DisplayPromptAsync(level == 1 ? "New Focus Block" : "New Sub-Focus", "Label:", "Next", "Cancel"); if (string.IsNullOrWhiteSpace(label)) return; var startText = await DisplayPromptAsync("Start Date", "Start date (DD/MM/YYYY):", "Next", "Cancel", initialValue: DateTime.Today.ToString("dd/MM/yyyy")); if (startText == null || !DateTime.TryParseExact(startText, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)) { await DisplayAlert("Invalid Date", "Use DD/MM/YYYY.", "OK"); return; } var endText = await DisplayPromptAsync("End Date", "Optional end date:", "Save", "Skip"); DateTime? end = DateTime.TryParseExact(endText, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null; var reason = await DisplayPromptAsync("Reason", "Optional reason:", "Save", "Skip"); if (_isSaving) return; _isSaving = true; try { await _lifePathService.CreateAsync(_auth.CurrentUsername, _game.GameId, label.Trim(), start, end, reason ?? "", parentId, level); await LoadAsync(); } finally { _isSaving = false; } }
    private async Task EditBlockAsync(EditorBlock block)
    { var label = await DisplayPromptAsync("Edit Label", "Label:", "Next", "Cancel", initialValue: block.Label); if (label == null) return; var startText = await DisplayPromptAsync("Start Date", "Start date (DD/MM/YYYY):", "Next", "Cancel", initialValue: block.StartDate.ToString("dd/MM/yyyy")); if (startText == null || !DateTime.TryParseExact(startText, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)) return; var endText = await DisplayPromptAsync("End Date", "Optional end date:", "Save", "Skip", initialValue: block.EndDate?.ToString("dd/MM/yyyy") ?? ""); DateTime? end = DateTime.TryParseExact(endText, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null; var reason = await DisplayPromptAsync("Reason", "Reason:", "Save", "Skip", initialValue: block.Reason); var raw = (await _lifePathService.GetBlocksForGameAsync(_auth.CurrentUsername, _game.GameId)).FirstOrDefault(b => b.Id == block.Id); if (raw == null) return; if (!string.IsNullOrWhiteSpace(label)) raw.Label = label.Trim(); raw.StartDate = start; raw.EndDate = end; if (reason != null) raw.Reason = reason.Trim(); await _lifePathService.UpdateAsync(raw); await LoadAsync(); }
}
