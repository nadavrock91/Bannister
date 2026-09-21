using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyDeadlinesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DailyDeadlineService _service;
    private readonly ResetTimeService _resetTime;
    private readonly ActivityService _activities;
    private readonly PrivacyModeService _privacyMode;
    private readonly GameService _games;
    private readonly VerticalStackLayout _content = new() { Spacing = 12 };
    private CancellationTokenSource? _timerCts;
    private List<Activity> _allActivities = new();
    private List<Activity> _availableActivities = new();

    public DailyDeadlinesPage(
        AuthService auth,
        DailyDeadlineService service,
        ActivityService activities,
        PrivacyModeService privacyMode,
        GameService games)
    {
        _auth = auth;
        _service = service;
        _resetTime = Application.Current?.Handler?.MauiContext?.Services
            .GetService<ResetTimeService>()
            ?? throw new InvalidOperationException("ResetTimeService is not registered.");
        _activities = activities;
        _privacyMode = privacyMode;
        _games = games;
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

        var games = await _games.GetGamesAsync(username);
        _allActivities = new List<Activity>();
        foreach (var game in games)
            _allActivities.AddRange(await _activities.GetActivitiesAsync(username, game.GameId));

        var displayMode = _privacyMode.GetDisplayMode(username);
        _allActivities = _allActivities.Where(a => displayMode switch
        {
            ActivityDisplayMode.PublicOnly => a.ActivityVisibility == 1 || a.ActivityVisibility == 2,
            ActivityDisplayMode.PrivateOnly => a.ActivityVisibility == 0 || a.ActivityVisibility == 2,
            _ => true
        }).GroupBy(a => a.Id).Select(g => g.First()).ToList();

        var state = await _service.GetStateAsync(username);
        var items = await _service.GetItemsWithActivitiesAsync(username);
        var active = items.Where(i => i.IsActive).ToList();
        var possible = items.Where(i => !i.IsActive).ToList();
        var log = await _service.GetCurrentLogAsync(username);
        var completedIds = ParseIds(log?.CompletedItemIds);

        _content.Children.Clear();
        _content.Children.Add(new Label { Text = "Daily Deadlines", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        _content.Children.Add(new Label { Text = $"Allowance: {state.Allowance} slots    Streak: {state.ConsecutiveStreak} / 3 days", FontSize = 15, TextColor = Color.FromArgb("#555") });

        if (active.Count > state.Allowance)
        {
            _content.Children.Add(new Label { Text = "Too many active deadlines. Move one to Possible.", TextColor = Color.FromArgb("#C62828"), FontAttributes = FontAttributes.Bold, BackgroundColor = Color.FromArgb("#FFEBEE"), Padding = 10 });
            if (showAlerts)
                await DisplayAlert("Daily Deadlines", "Your active deadlines exceed the current allowance. Move one to Possible.", "OK");
        }

        var activeStack = new VerticalStackLayout { Spacing = 6 };
        activeStack.Children.Add(new Label { Text = "Today's Deadlines", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var item in active)
            activeStack.Children.Add(BuildActiveRow(item, completedIds));
        if (active.Count > 0 && active.All(i => completedIds.Contains(i.Id)))
            activeStack.Children.Add(new Label { Text = "✓ All done!", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32"), BackgroundColor = Color.FromArgb("#E8F5E9"), Padding = 10 });
        _content.Children.Add(Card(activeStack));

        var usedActivityIds = items.Select(i => i.ActivityId).ToHashSet();
        _availableActivities = _allActivities
            .Where(a => !usedActivityIds.Contains(a.Id))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _content.Children.Add(BuildAddSection(
            _availableActivities, active.Count >= state.Allowance));

        var possibleStack = new VerticalStackLayout { Spacing = 6 };
        possibleStack.Children.Add(new Label { Text = "Possible", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var item in possible)
            possibleStack.Children.Add(BuildPossibleRow(item, active.Count >= state.Allowance));
        _content.Children.Add(Card(possibleStack));
    }

    private View BuildActiveRow(DailyDeadlineItem item, HashSet<int> completed)
    {
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8, Padding = 8 };
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
        row.Add(BuildActivityVisual(FindActivity(item)), 1, 0);
        var move = new Button { Text = "Move to Possible", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        move.Clicked += async (_, _) => { item.IsActive = false; await _service.SaveItemAsync(item); await LoadAsync(false); };
        row.Add(move, 2, 0);
        var del = new Button { Text = "Delete", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828") };
        del.Clicked += async (_, _) => { await _service.DeleteItemAsync(item.Id); await LoadAsync(false); };
        row.Add(del, 3, 0);
        return new Border { Content = row, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private View BuildPossibleRow(DailyDeadlineItem item, bool full)
    {
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 6, Padding = 8 };
        row.Add(BuildActivityVisual(FindActivity(item)), 0, 0);
        var move = new Button { Text = "Move to Active", IsEnabled = !full, FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0") };
        move.Clicked += async (_, _) => { item.IsActive = true; await _service.SaveItemAsync(item); await LoadAsync(false); };
        row.Add(move, 1, 0);
        var del = new Button { Text = "Delete", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828") };
        del.Clicked += async (_, _) => { await _service.DeleteItemAsync(item.Id); await LoadAsync(false); };
        row.Add(del, 2, 0);
        return new Border { Content = row, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private View BuildAddSection(List<Activity> available, bool full)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label { Text = "Add Deadline From Activity", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        var gamePicker = new Picker { Title = "Game", ItemsSource = new[] { "All Games" }.Concat(available.Select(a => a.Game).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(g => g)).ToList(), SelectedIndex = 0 };
        var search = new Entry { Placeholder = "Search activities..." };
        var activityPicker = new Picker { Title = "Select an activity" };
        void RefreshPicker()
        {
            string game = gamePicker.SelectedItem?.ToString() ?? "All Games";
            string text = search.Text?.Trim() ?? "";
            var filtered = available.Where(a =>
                (game == "All Games" || string.Equals(a.Game, game, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(text) || a.Name.Contains(text, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            activityPicker.ItemsSource = filtered.Select(a => a.Name).ToList();
            activityPicker.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        }
        gamePicker.SelectedIndexChanged += (_, _) => RefreshPicker();
        search.TextChanged += (_, _) => RefreshPicker();
        stack.Children.Add(gamePicker);
        stack.Children.Add(search);
        stack.Children.Add(activityPicker);
        var row = new HorizontalStackLayout { Spacing = 8 };
        var active = new Button { Text = "Add to Active", IsEnabled = !full, FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#E8F5E9"), TextColor = Color.FromArgb("#2E7D32") };
        active.Clicked += async (_, _) =>
        {
            var selected = GetSelectedActivity(available, gamePicker, search, activityPicker);
            if (selected != null) await AddActivityAsync(selected, true);
        };
        var possible = new Button { Text = "Add to Possible", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        possible.Clicked += async (_, _) =>
        {
            var selected = GetSelectedActivity(available, gamePicker, search, activityPicker);
            if (selected != null) await AddActivityAsync(selected, false);
        };
        row.Children.Add(active); row.Children.Add(possible);
        stack.Children.Add(row);
        RefreshPicker();
        return Card(stack);
    }

    private static Activity? GetSelectedActivity(List<Activity> available, Picker gamePicker, Entry search, Picker activityPicker)
    {
        string game = gamePicker.SelectedItem?.ToString() ?? "All Games";
        string text = search.Text?.Trim() ?? "";
        var filtered = available.Where(a =>
            (game == "All Games" || string.Equals(a.Game, game, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(text) || a.Name.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
        return activityPicker.SelectedIndex >= 0 && activityPicker.SelectedIndex < filtered.Count
            ? filtered[activityPicker.SelectedIndex] : null;
    }

    private async Task AddActivityAsync(Activity activity, bool active)
    {
        if (active && (await _service.GetActiveItemsAsync(_auth.CurrentUsername)).Count >= (await _service.GetStateAsync(_auth.CurrentUsername)).Allowance) return;
        await _service.SaveItemAsync(new DailyDeadlineItem { Username = _auth.CurrentUsername, ActivityId = activity.Id, GameId = activity.Game, IsActive = active, SortOrder = DateTime.Now.GetHashCode() });
        await LoadAsync(false);
    }

    private Activity? FindActivity(DailyDeadlineItem item) =>
        _allActivities.FirstOrDefault(a => a.Id == item.ActivityId && string.Equals(a.Game, item.GameId, StringComparison.OrdinalIgnoreCase))
        ?? _allActivities.FirstOrDefault(a => a.Id == item.ActivityId);

    private static View BuildActivityVisual(Activity? activity)
    {
        if (activity == null)
            return new Label { Text = "Unavailable activity", TextColor = Color.FromArgb("#999"), VerticalOptions = LayoutOptions.Center };
        if (activity.IsImageOnly && !string.IsNullOrWhiteSpace(activity.ImagePath))
        {
            string path = Path.IsPathRooted(activity.ImagePath)
                ? (File.Exists(activity.ImagePath) ? activity.ImagePath : Path.Combine(FileSystem.AppDataDirectory, "ActivityImages", Path.GetFileName(activity.ImagePath)))
                : Path.Combine(FileSystem.AppDataDirectory, "ActivityImages", activity.ImagePath);
            if (File.Exists(path))
                return new Image { Source = ImageSource.FromFile(path), HeightRequest = 50, WidthRequest = 50, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Start };
        }
        return new Label { Text = activity.Name, FontSize = 14, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center };
    }

    private async Task ScheduleNextResetAsync()
    {
        _timerCts?.Cancel();
        _timerCts = new CancellationTokenSource();
        var token = _timerCts.Token;
        var delay = await _resetTime.GetNextResetDateTime(_auth.CurrentUsername) - DateTime.Now;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1), token); }
            catch (TaskCanceledException) { return; }
            await MainThread.InvokeOnMainThreadAsync(async () => { await LoadAsync(true); await ScheduleNextResetAsync(); });
        }, token);
    }

    private static HashSet<int> ParseIds(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id > 0).ToHashSet();

    private static Border Card(View view) => new() { Content = view, Padding = 12, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Colors.White };
}
