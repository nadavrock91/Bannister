using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyDeadlinesPage : ContentPage
{
    private static List<Activity>? _cachedActivities;
    private static bool _activitiesCached;
    private readonly AuthService _auth;
    private readonly DailyDeadlineService _service;
    private readonly ResetTimeService _resetTime;
    private readonly ActivityService _activities;
    private readonly PrivacyModeService _privacyMode;
    private readonly GameService _games;
    private readonly VerticalStackLayout _content = new() { Spacing = 12 };
    private readonly Grid _rootGrid = new();
    private CancellationTokenSource? _timerCts;
    private List<Activity> _allActivities = new();
    private List<Activity> _availableActivities = new();
    private Activity? _selectedPickerActivity;
    private bool _activitiesLoaded;
    private Picker? _gamePicker;
    private Entry? _activitySearch;
    private VerticalStackLayout? _activityResultStack;
    private Label? _activityRemainingLabel;
    private Grid? _activityLoadingOverlay;
    private HashSet<int> _usedDeadlineActivityIds = new();
    private int _currentActiveCount;
    private int _currentAllowance;

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
        _rootGrid.Children.Add(new ScrollView { Content = _content });
        Content = _rootGrid;
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

        _activitiesLoaded = false;
        _allActivities.Clear();
        _availableActivities.Clear();
        _selectedPickerActivity = null;

        var state = await _service.GetStateAsync(username);
        var items = await _service.GetItemsWithActivitiesAsync(username);
        var active = items.Where(i => i.IsActive).ToList();
        var possible = items.Where(i => !i.IsActive).ToList();
        _usedDeadlineActivityIds = items.Select(i => i.ActivityId).ToHashSet();
        _currentActiveCount = active.Count;
        _currentAllowance = state.Allowance;
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
            activeStack.Children.Add(await BuildActiveRowAsync(item, completedIds));
        if (active.Count > 0 && active.All(i => completedIds.Contains(i.Id)))
            activeStack.Children.Add(new Label { Text = "✓ All done!", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32"), BackgroundColor = Color.FromArgb("#E8F5E9"), Padding = 10 });
        _content.Children.Add(Card(activeStack));

        _content.Children.Add(BuildAddSection(
            active.Count >= state.Allowance));

        var possibleStack = new VerticalStackLayout { Spacing = 6 };
        possibleStack.Children.Add(new Label { Text = "Possible", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var item in possible)
            possibleStack.Children.Add(await BuildPossibleRowAsync(item, active.Count >= state.Allowance));
        _content.Children.Add(Card(possibleStack));
    }

    private async Task<View> BuildActiveRowAsync(DailyDeadlineItem item, HashSet<int> completed)
    {
        var activity = await ResolveActivityAsync(item);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8, Padding = 8 };
        if (activity == null)
        {
            row.BackgroundColor = Color.FromArgb("#F0F0F0");
            row.Add(new Label { Text = "Activity not found", TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center }, 0, 0);
            var missingDelete = MakeDeleteButton(item);
            row.Add(missingDelete, 1, 0);
            return new Border { Content = row, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Color.FromArgb("#F0F0F0") };
        }

        row.Add(BuildActivityVisual(activity), 0, 0);
        if (completed.Contains(item.Id))
            row.Add(new Label { Text = "✓ Done", TextColor = Color.FromArgb("#777"), FontSize = 12, VerticalOptions = LayoutOptions.Center }, 1, 0);
        else
        {
            var done = new Button { Text = "Done ✓", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#E8F5E9"), TextColor = Color.FromArgb("#2E7D32") };
            done.Clicked += async (_, _) => await MarkDoneAsync(item);
            row.Add(done, 1, 0);
        }
        var move = new Button { Text = "Move to Possible", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        move.Clicked += async (_, _) => { item.IsActive = false; await _service.SaveItemAsync(item); await LoadAsync(false); };
        row.Add(move, 2, 0);
        row.Add(MakeDeleteButton(item), 3, 0);
        return new Border { Content = row, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private async Task<View> BuildPossibleRowAsync(DailyDeadlineItem item, bool full)
    {
        var activity = await ResolveActivityAsync(item);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 6, Padding = 8 };
        if (activity == null)
        {
            row.BackgroundColor = Color.FromArgb("#F0F0F0");
            row.Add(new Label { Text = "Activity not found", TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center }, 0, 0);
            row.Add(MakeDeleteButton(item), 1, 0);
            return new Border { Content = row, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Color.FromArgb("#F0F0F0") };
        }
        row.Add(BuildActivityVisual(activity), 0, 0);
        var move = new Button { Text = "Move to Active", IsEnabled = !full, FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0") };
        move.Clicked += async (_, _) => { item.IsActive = true; await _service.SaveItemAsync(item); await LoadAsync(false); };
        row.Add(move, 1, 0);
        var del = new Button { Text = "Delete", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828") };
        del.Clicked += async (_, _) => { await _service.DeleteItemAsync(item.Id); await LoadAsync(false); };
        row.Add(del, 2, 0);
        return new Border { Content = row, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private async Task<Activity?> ResolveActivityAsync(DailyDeadlineItem item)
    {
        return FindActivity(item) ?? await _activities.GetActivityAsync(item.ActivityId);
    }

    private Button MakeDeleteButton(DailyDeadlineItem item)
    {
        var del = new Button { Text = "Delete", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828") };
        del.Clicked += async (_, _) => { await _service.DeleteItemAsync(item.Id); await LoadAsync(false); };
        return del;
    }

    private async Task MarkDoneAsync(DailyDeadlineItem item)
    {
        var log = await _service.GetCurrentLogAsync(_auth.CurrentUsername) ?? new DailyDeadlineLog { Username = _auth.CurrentUsername, LogDate = await _resetTime.GetTodayResetKey(_auth.CurrentUsername) };
        var ids = ParseIds(log.CompletedItemIds);
        ids.Add(item.Id);
        log.CompletedItemIds = string.Join(",", ids.OrderBy(i => i));
        var active = await _service.GetActiveItemsAsync(_auth.CurrentUsername);
        log.AllCompleted = active.Count > 0 && active.All(i => ids.Contains(i.Id));
        await _service.SaveLogAsync(log);
        await LoadAsync(false);
    }

    private View BuildAddSection(bool full)
    {
        var outer = new VerticalStackLayout { Spacing = 8 };
        var toggle = new Button
        {
            Text = "＋ Add Deadline From Activity",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            HorizontalOptions = LayoutOptions.Fill
        };
        var stack = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        var sectionHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        sectionHeader.Add(new Label { Text = "Add Deadline From Activity", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var refresh = new Button { Text = "↺ Refresh list", FontSize = 11, HeightRequest = 32, Padding = new Thickness(8, 0), BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        sectionHeader.Add(refresh, 1, 0);
        stack.Children.Add(sectionHeader);
        _gamePicker = new Picker { Title = "Game", ItemsSource = new[] { "All Games" }, SelectedIndex = 0 };
        _activitySearch = new Entry { Placeholder = "Search activities..." };
        _activityResultStack = new VerticalStackLayout { Spacing = 4 };
        var activityResults = new ScrollView
        {
            Content = _activityResultStack,
            MaximumHeightRequest = 250
        };
        _gamePicker.SelectedIndexChanged += (_, _) => RefreshActivityResults();
        _activitySearch.TextChanged += (_, _) => RefreshActivityResults();
        stack.Children.Add(_gamePicker);
        stack.Children.Add(_activitySearch);
        stack.Children.Add(activityResults);
        var row = new HorizontalStackLayout { Spacing = 8 };
        var active = new Button { Text = "Add to Active", IsEnabled = !full, FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#E8F5E9"), TextColor = Color.FromArgb("#2E7D32") };
        active.Clicked += async (_, _) =>
        {
            var selected = _selectedPickerActivity;
            if (selected != null) await AddActivityAsync(selected, true);
        };
        var possible = new Button { Text = "Add to Possible", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34, BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F") };
        possible.Clicked += async (_, _) =>
        {
            var selected = _selectedPickerActivity;
            if (selected != null) await AddActivityAsync(selected, false);
        };
        row.Children.Add(active); row.Children.Add(possible);
        stack.Children.Add(row);
        refresh.Clicked += async (_, _) =>
        {
            _activitiesCached = false;
            _cachedActivities = null;
            _activitiesLoaded = false;
            await LoadActivitiesAsync();
        };
        toggle.Clicked += async (_, _) =>
        {
            stack.IsVisible = !stack.IsVisible;
            toggle.Text = stack.IsVisible
                ? "－ Add Deadline From Activity"
                : "＋ Add Deadline From Activity";
            if (stack.IsVisible)
            {
                if (_activitiesCached && _cachedActivities != null)
                {
                    _allActivities = _cachedActivities.ToList();
                    _availableActivities = _allActivities
                        .Where(a => !_usedDeadlineActivityIds.Contains(a.Id))
                        .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    _activitiesLoaded = true;
                    RefreshActivityResults();
                }
                else
                {
                    await LoadActivitiesAsync();
                }
            }
        };
        outer.Children.Add(toggle);
        outer.Children.Add(stack);
        return Card(outer);
    }

    private async Task LoadActivitiesAsync()
    {
        if (_activitiesLoaded) return;
        if (_activitiesCached && _cachedActivities != null)
        {
            _allActivities = _cachedActivities.ToList();
            _availableActivities = _allActivities
                .Where(a => !_usedDeadlineActivityIds.Contains(a.Id))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _activitiesLoaded = true;
            RefreshActivityResults();
            return;
        }
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = 100
        };
        var remainingLabel = new Label
        {
            Text = "0 games remaining",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        var loadingCard = new Frame
        {
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "Loading activities...",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    remainingLabel
                }
            },
            Padding = 24,
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        overlay.Children.Add(loadingCard);
        _rootGrid.Children.Add(overlay);
        _activityLoadingOverlay = overlay;
        try
        {
            string username = _auth.CurrentUsername;
            var games = await _games.GetGamesAsync(username);
            remainingLabel.Text = $"{games.Count} games remaining";
            var displayMode = _privacyMode.GetDisplayMode(username);
            _allActivities = new List<Activity>();
            for (int i = 0; i < games.Count; i++)
            {
                var loaded = await _activities.GetActivitiesAsync(username, games[i].GameId);
                _allActivities.AddRange(loaded.Where(a => displayMode switch
                {
                    ActivityDisplayMode.PublicOnly => a.ActivityVisibility == 1 || a.ActivityVisibility == 2,
                    ActivityDisplayMode.PrivateOnly => a.ActivityVisibility == 0 || a.ActivityVisibility == 2,
                    _ => true
                }));
                int remaining = games.Count - i - 1;
                remainingLabel.Text = $"{remaining} games remaining";
            }
            _allActivities = _allActivities.GroupBy(a => a.Id).Select(g => g.First()).ToList();
            _cachedActivities = _allActivities.ToList();
            _activitiesCached = true;
            _availableActivities = _allActivities
                .Where(a => !_usedDeadlineActivityIds.Contains(a.Id))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
            if (_gamePicker != null)
            {
                _gamePicker.ItemsSource = new[] { "All Games" }
                    .Concat(_availableActivities.Select(a => a.Game)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g))
                    .ToList();
                _gamePicker.SelectedIndex = 0;
            }
            _activitiesLoaded = true;
            RefreshActivityResults();
        }
        finally
        {
            _rootGrid.Children.Remove(overlay);
            _activityLoadingOverlay = null;
        }
    }

    private void RefreshActivityResults()
    {
        if (!_activitiesLoaded || _activityResultStack == null) return;
        string game = _gamePicker?.SelectedItem?.ToString() ?? "All Games";
        string text = _activitySearch?.Text?.Trim() ?? "";
        if (game == "All Games" && string.IsNullOrWhiteSpace(text))
        {
            _selectedPickerActivity = null;
            _activityResultStack.Children.Clear();
            _activityResultStack.Children.Add(new Label
            {
                Text = "Select a game or type to search",
                FontSize = 12,
                TextColor = Color.FromArgb("#777")
            });
            return;
        }
        var filtered = _availableActivities.Where(a =>
            (game == "All Games" || string.Equals(a.Game, game, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(text) || a.Name.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
        _activityResultStack.Children.Clear();
        if (_selectedPickerActivity == null || !filtered.Contains(_selectedPickerActivity))
            _selectedPickerActivity = filtered.FirstOrDefault();
        foreach (var activity in filtered)
        {
            var card = new Border { Content = BuildActivityVisual(activity), Padding = 6, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = ReferenceEquals(activity, _selectedPickerActivity) ? Color.FromArgb("#E3F2FD") : Colors.White };
            var captured = activity;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _selectedPickerActivity = captured;
                foreach (var child in _activityResultStack.Children)
                    if (child is Border other) other.BackgroundColor = Colors.White;
                card.BackgroundColor = Color.FromArgb("#E3F2FD");
            };
            card.GestureRecognizers.Add(tap);
            _activityResultStack.Children.Add(card);
        }
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
