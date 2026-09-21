using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyDeadlinesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DailyDeadlineService _service;
    private readonly ResetTimeService _resetTime;
    private readonly VerticalStackLayout _content = new() { Spacing = 12 };
    private CancellationTokenSource? _timerCts;

    public DailyDeadlinesPage(AuthService auth, DailyDeadlineService service)
    {
        _auth = auth;
        _service = service;
        _resetTime = Application.Current?.Handler?.MauiContext?.Services
            .GetService<ResetTimeService>()
            ?? throw new InvalidOperationException(
                "ResetTimeService is not registered.");
        Title = "Daily Deadlines";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync(true);
        await ScheduleNextResetAsync();
    }

    protected override void OnDisappearing()
    {
        _timerCts?.Cancel();
        base.OnDisappearing();
    }

    private async Task LoadAsync(bool showAlerts)
    {
        string username = _auth.CurrentUsername;
        var reset = await _service.CheckAndResetAsync(username);
        if (showAlerts && reset.WasReset)
        {
            if (reset.AllowanceIncreased)
                await DisplayAlert("Daily Deadlines", "Three completed days! Your allowance increased.", "OK");
            else if (reset.AllowanceLost)
                await DisplayAlert("Daily Deadlines", "The previous day was incomplete. One allowance slot was lost.", "OK");
        }

        var state = await _service.GetStateAsync(username);
        var active = await _service.GetActiveItemsAsync(username);
        var possible = await _service.GetPossibleItemsAsync(username);
        var log = await _service.GetCurrentLogAsync(username);

        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "Daily Deadlines",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        _content.Children.Add(new Label
        {
            Text = $"Allowance: {state.Allowance} slots    Streak: {state.ConsecutiveStreak} / 3 days",
            FontSize = 15,
            TextColor = Color.FromArgb("#555")
        });

        if (active.Count > state.Allowance)
        {
            _content.Children.Add(new Label
            {
                Text = "Too many active deadlines. Move one to Possible.",
                TextColor = Color.FromArgb("#C62828"),
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                Padding = 10
            });
            if (showAlerts)
                await DisplayAlert("Daily Deadlines", "Your active deadlines exceed the current allowance. Move one to Possible.", "OK");
        }

        var completedIds = ParseIds(log?.CompletedItemIds);
        var activeStack = new VerticalStackLayout { Spacing = 6 };
        activeStack.Children.Add(new Label { Text = "Today's Deadlines", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var item in active)
            activeStack.Children.Add(BuildActiveRow(item, completedIds, active.Count));
        if (active.Count > 0 && active.All(i => completedIds.Contains(i.Id)))
            activeStack.Children.Add(new Label { Text = "✓ All done!", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32"), BackgroundColor = Color.FromArgb("#E8F5E9"), Padding = 10 });
        _content.Children.Add(Card(activeStack));

        _content.Children.Add(BuildAddSection(state, active.Count));

        var possibleStack = new VerticalStackLayout { Spacing = 6 };
        possibleStack.Children.Add(new Label { Text = "Possible", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var item in possible)
            possibleStack.Children.Add(BuildPossibleRow(item, active.Count >= state.Allowance));
        _content.Children.Add(Card(possibleStack));
    }

    private View BuildActiveRow(DailyDeadlineItem item, HashSet<int> completed, int activeCount)
    {
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8, Padding = 8 };
        var check = new CheckBox { IsChecked = completed.Contains(item.Id), Color = Color.FromArgb("#2E7D32") };
        check.CheckedChanged += async (_, e) =>
        {
            var log = await _service.GetCurrentLogAsync(_auth.CurrentUsername) ?? new DailyDeadlineLog { Username = _auth.CurrentUsername, LogDate = await _resetTime.GetTodayResetKey(_auth.CurrentUsername) };
            var ids = ParseIds(log.CompletedItemIds);
            if (e.Value) ids.Add(item.Id); else ids.Remove(item.Id);
            log.CompletedItemIds = string.Join(",", ids.OrderBy(i => i));
            var active = await _service.GetActiveItemsAsync(_auth.CurrentUsername);
            log.AllCompleted = active.Count > 0 && active.All(i => ids.Contains(i.Id));
            await _service.SaveLogAsync(log);
            await LoadAsync(false);
        };
        row.Add(check, 0, 0);
        row.Add(new Label { Text = item.Title, FontSize = 14, VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#222") }, 1, 0);
        var move = new Button { Text = "Move to Possible", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        move.Clicked += async (_, _) => { item.IsActive = false; await _service.SaveItemAsync(item); await LoadAsync(false); };
        row.Add(move, 2, 0);
        return new Border { Content = row, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private View BuildPossibleRow(DailyDeadlineItem item, bool full)
    {
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 6, Padding = 8 };
        row.Add(new Label { Text = item.Title, FontSize = 14, VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#222") }, 0, 0);
        var move = new Button { Text = "Move to Active", IsEnabled = !full, FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0") };
        move.Clicked += async (_, _) => { item.IsActive = true; await _service.SaveItemAsync(item); await LoadAsync(false); };
        row.Add(move, 1, 0);
        var del = new Button { Text = "Delete", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828") };
        del.Clicked += async (_, _) => { await _service.DeleteItemAsync(item.Id); await LoadAsync(false); };
        row.Add(del, 2, 0);
        return new Border { Content = row, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private View BuildAddSection(DailyDeadlineState state, int activeCount)
    {
        var title = new Entry { Placeholder = "Deadline title" };
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label { Text = "Add Deadline", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        stack.Children.Add(title);
        var row = new HorizontalStackLayout { Spacing = 8 };
        var active = new Button { Text = "Add to Active", IsEnabled = activeCount < state.Allowance, BackgroundColor = Color.FromArgb("#E8F5E9"), TextColor = Color.FromArgb("#2E7D32") };
        active.Clicked += async (_, _) => await AddItemAsync(title, true);
        var possible = new Button { Text = "Add to Possible", BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        possible.Clicked += async (_, _) => await AddItemAsync(title, false);
        row.Children.Add(active); row.Children.Add(possible); stack.Children.Add(row);
        return Card(stack);
    }

    private async Task AddItemAsync(Entry entry, bool active)
    {
        if (string.IsNullOrWhiteSpace(entry.Text)) return;
        var items = active ? await _service.GetActiveItemsAsync(_auth.CurrentUsername) : new List<DailyDeadlineItem>();
        var state = await _service.GetStateAsync(_auth.CurrentUsername);
        if (active && items.Count >= state.Allowance) return;
        await _service.SaveItemAsync(new DailyDeadlineItem { Username = _auth.CurrentUsername, Title = entry.Text.Trim(), IsActive = active, SortOrder = DateTime.Now.GetHashCode() });
        await LoadAsync(false);
    }

    private async Task ScheduleNextResetAsync()
    {
        _timerCts?.Cancel();
        _timerCts = new CancellationTokenSource();
        var token = _timerCts.Token;
        var nextReset = await _resetTime.GetNextResetDateTime(_auth.CurrentUsername);
        var delay = nextReset - DateTime.Now;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1), token); }
            catch (TaskCanceledException) { return; }
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadAsync(true);
                await ScheduleNextResetAsync();
            });
        }, token);
    }

    private static HashSet<int> ParseIds(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id > 0).ToHashSet();

    private static Border Card(View view) => new() { Content = view, Padding = 12, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Colors.White };
}
