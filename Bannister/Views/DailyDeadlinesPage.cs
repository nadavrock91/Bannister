using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyDeadlinesPage : Bannister.Views.ModeThemedPage
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
        var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);

        async Task<List<DailyDeadlineItem>> FilterForDisplayModeAsync(
            IEnumerable<DailyDeadlineItem> source)
        {
            var visible = new List<DailyDeadlineItem>();
            foreach (var item in source)
            {
                var activity = await ResolveActivityAsync(item);
                if (activity == null || displayMode switch
                    {
                        ActivityDisplayMode.PublicOnly =>
                            activity.ActivityVisibility == 1 || activity.ActivityVisibility == 2,
                        ActivityDisplayMode.PrivateOnly =>
                            activity.ActivityVisibility == 0 || activity.ActivityVisibility == 2,
                        _ => true
                    })
                {
                    visible.Add(item);
                }
            }

            return visible;
        }

        var visibleActive = await FilterForDisplayModeAsync(active);
        var visiblePossible = await FilterForDisplayModeAsync(possible);
        _usedDeadlineActivityIds = items.Select(i => i.ActivityId).ToHashSet();
        _currentActiveCount = active.Count;
        _currentAllowance = state.Allowance;
        var log = await _service.GetCurrentLogAsync(username);
        var completedIds = ParseIds(log?.CompletedItemIds);

        _content.Children.Clear();
        _content.Children.Add((new Label { Text = "Daily Deadlines", FontSize = 26, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        _content.Children.Add((new Label { Text = $"Allowance: {state.Allowance} slots    Streak: {state.ConsecutiveStreak} / 3 days", FontSize = 15 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        if (active.Count > state.Allowance)
        {
            _content.Children.Add((new Label { Text = "Too many active deadlines. Move one to Possible.", FontAttributes = FontAttributes.Bold, Padding = 10 }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText1, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground));
            if (showAlerts)
                await DisplayAlert("Daily Deadlines", "Your active deadlines exceed the current allowance. Move one to Possible.", "OK");
        }

        var activeStack = new VerticalStackLayout { Spacing = 6 };
        activeStack.Children.Add((new Label { Text = "Today's Deadlines", FontSize = 19, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        var activeHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            Padding = new Thickness(8, 4)
        };
        activeHeader.Add((new Label { Text = "Done", FontSize = 12, FontAttributes = FontAttributes.Bold, WidthRequest = 60 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true), 0, 0);
        activeHeader.Add((new Label { Text = "Deadline", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true), 1, 0);
        activeStack.Children.Add(activeHeader);
        if (displayMode != ActivityDisplayMode.All)
        {
            int visibleCompleted = completedIds.Count(id =>
                visibleActive.Any(item => item.Id == id));
            int totalCompleted = completedIds.Count;
            activeStack.Children.Add((new Label
            {
                Text = $"{visibleCompleted} / {visibleActive.Count} shown   ({totalCompleted} / {active.Count} total)",
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 6)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true));
        }
        foreach (var item in visibleActive.OrderBy(i => completedIds.Contains(i.Id) ? 1 : 0).ThenBy(i => i.SortOrder))
            activeStack.Children.Add(await BuildActiveRowAsync(item, completedIds));
        if (active.Count > 0 && active.All(i => completedIds.Contains(i.Id)))
            activeStack.Children.Add((new Label { Text = "✓ All done!", FontSize = 15, FontAttributes = FontAttributes.Bold, Padding = 10 }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground));
        _content.Children.Add(Card(activeStack));

        _content.Children.Add(BuildAddSection(
            active.Count >= state.Allowance));

        var possibleStack = new VerticalStackLayout { Spacing = 6 };
        possibleStack.Children.Add((new Label { Text = "Possible", FontSize = 19, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        foreach (var item in visiblePossible)
            possibleStack.Children.Add(await BuildPossibleRowAsync(item, active.Count >= state.Allowance));
        _content.Children.Add(Card(possibleStack));
    }

    private async Task<View> BuildActiveRowAsync(DailyDeadlineItem item, HashSet<int> completed)
    {
        var activity = await ResolveActivityAsync(item);
        return await DailyDeadlineViewHelper.BuildRowAsync(
            item,
            activity,
            completed,
            _service,
            _resetTime,
            _auth.CurrentUsername,
            () => LoadAsync(false),
            new DailyDeadlineRowOptions
            {
                ShowMoveButton = true,
                ShowDeleteButton = true,
                OnMove = async i => { i.IsActive = false; await _service.SaveItemAsync(i); await LoadAsync(false); },
                OnDelete = async i => { await _service.DeleteItemAsync(i.Id); await LoadAsync(false); }
            });
    }

    private async Task<View> BuildPossibleRowAsync(DailyDeadlineItem item, bool full)
    {
        var activity = await ResolveActivityAsync(item);
        return await DailyDeadlineViewHelper.BuildPossibleRowAsync(
            item,
            activity,
            full,
            _service,
            _auth.CurrentUsername,
            () => LoadAsync(false),
            stackButtons: false);
    }

    private async Task<Activity?> ResolveActivityAsync(DailyDeadlineItem item)
    {
        return FindActivity(item) ?? await _activities.GetActivityAsync(item.ActivityId);
    }

    private Button MakeDeleteButton(DailyDeadlineItem item)
    {
        var del = (new Button { Text = "Delete", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        del.Clicked += async (_, _) => { await _service.DeleteItemAsync(item.Id); await LoadAsync(false); };
        return del;
    }

    private async Task SetCompletedAsync(DailyDeadlineItem item, bool isCompleted)
    {
        await DailyDeadlineViewHelper.SetCompletedAsync(
            item, isCompleted, _service, _resetTime, _auth.CurrentUsername, () => LoadAsync(false));
    }

    private View BuildAddSection(bool full)
    {
        var outer = new VerticalStackLayout { Spacing = 8 };
        var toggle = (new Button
        {
            Text = "＋ Add Deadline From Activity",
            HorizontalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        var stack = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        var sectionHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        sectionHeader.Add((new Label { Text = "Add Deadline From Activity", FontSize = 19, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true), 0, 0);
        var refresh = (new Button { Text = "↺ Refresh list", FontSize = 11, HeightRequest = 32, Padding = new Thickness(8, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
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
        var active = (new Button { Text = "Add to Active", IsEnabled = !full, FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Button.TextColorProperty, ThemeKey.SuccessText);
        active.Clicked += async (_, _) =>
        {
            var selected = _selectedPickerActivity;
            if (selected != null) await AddActivityAsync(selected, true);
        };
        var possible = (new Button { Text = "Add to Possible", FontSize = 11, Padding = new Thickness(8, 0), HeightRequest = 34 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
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
        var overlay = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = 100
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);
        var remainingLabel = (new Label
        {
            Text = "0 games remaining",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        var loadingCard = (new Frame
        {
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    (new Label
                    {
                        Text = "Loading activities...",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    remainingLabel
                }
            },
            Padding = 24,
            CornerRadius = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
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
            _activityResultStack.Children.Add((new Label
            {
                Text = "Select a game or type to search",
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
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
            var card = (new Border { Content = DailyDeadlineViewHelper.BuildActivityVisual(activity), Padding = 6, StrokeThickness = 1 }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ReferenceEquals(activity, _selectedPickerActivity) ? ThemeKey.InfoBackground : ThemeKey.CardBackground);
            var captured = activity;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _selectedPickerActivity = captured;
                foreach (var child in _activityResultStack.Children)
                    if (child is Border other) other.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
                card.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground);
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

    private static Border Card(View view) => (new Border(){ Content = view, Padding = 12, StrokeThickness = 1 }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
}
