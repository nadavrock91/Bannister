using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "gameId")]
public class GameCatchUpPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly StreakService _streaks;
    private readonly NewHabitService _newHabits;

    private readonly VerticalStackLayout _cardsStack = new() { Spacing = 12 };
    private readonly Label _subtitle = new();
    private readonly Button _applyButton;
    private readonly Button _skipButton;
    private readonly List<ActivityCatchUpCard> _cards = new();
    private bool _loaded;
    private bool _isSubmitting;
    private string _gameId = "";
    private Game? _game;

    public string GameId
    {
        get => _gameId;
        set
        {
            _gameId = value ?? "";
            OnPropertyChanged();
        }
    }

    public GameCatchUpPage(
        AuthService auth,
        GameService games,
        ActivityService activities,
        ExpService exp,
        StreakService streaks,
        NewHabitService newHabits)
    {
        _auth = auth;
        _games = games;
        _activities = activities;
        _exp = exp;
        _streaks = streaks;
        _newHabits = newHabits;

        Title = "Catch Up";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _applyButton = new Button
        {
            Text = "Apply Catch-Up",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 48
        };
        _applyButton.Clicked += OnApplyClicked;

        _skipButton = new Button
        {
            Text = "Skip All",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#444444"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _skipButton.Clicked += OnSkipClicked;

        Content = BuildContent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded)
            return;

        _loaded = true;
        await LoadAsync();
    }

    private View BuildContent()
    {
        var header = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(20, 18, 20, 10),
            Children =
            {
                new Label
                {
                    Text = "Catch Up",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222222")
                },
                _subtitle
            }
        };

        _subtitle.FontSize = 15;
        _subtitle.TextColor = Color.FromArgb("#666666");
        _subtitle.LineBreakMode = LineBreakMode.WordWrap;

        var buttonGrid = new Grid
        {
            Padding = new Thickness(20, 12, 20, 18),
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        buttonGrid.Add(_applyButton, 0, 0);
        buttonGrid.Add(_skipButton, 1, 0);

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 6, 20, 12),
                Spacing = 12,
                Children = { _cardsStack }
            }
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(scroll, 1);
        Grid.SetRow(buttonGrid, 2);
        root.Children.Add(header);
        root.Children.Add(scroll);
        root.Children.Add(buttonGrid);
        return root;
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId))
        {
            await DisplayAlert("Error", "No game ID provided.", "OK");
            await NavigateToGameAsync();
            return;
        }

        _game = await _games.GetGameAsync(_auth.CurrentUsername, GameId);
        if (_game == null)
        {
            await DisplayAlert("Error", $"Game '{GameId}' not found.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (!_game.LastVisitedAt.HasValue)
        {
            await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, _game.GameId, DateTime.Now);
            await NavigateToGameAsync();
            return;
        }

        var catchUpStart = _game.LastVisitedAt.Value.Date.AddDays(1);
        var catchUpEnd = DateTime.Today;
        if (catchUpStart > catchUpEnd)
        {
            await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, _game.GameId, DateTime.Now);
            await NavigateToGameAsync();
            return;
        }

        int daysAway = (DateTime.Today - _game.LastVisitedAt.Value.Date).Days;
        _subtitle.Text = $"You've been away for {daysAway} days. Mark what you did each day.";

        var days = EachDay(catchUpStart, catchUpEnd).ToList();
        var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);
        var eligibleActivities = activities
            .Where(IsEligibleActivity)
            .OrderBy(a => a.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _cards.Clear();
        _cardsStack.Children.Clear();

        foreach (var activity in eligibleActivities)
        {
            var eligibleDays = new List<DateTime>();
            foreach (var day in days)
            {
                if (!activity.IsScheduledDisplayDay(day))
                    continue;

                var existing = await _exp.GetExpLogsForActivityOnDateAsync(
                    _auth.CurrentUsername,
                    activity.Game,
                    activity.Id,
                    day);

                if (existing.Count == 0)
                    eligibleDays.Add(day);
            }

            if (eligibleDays.Count == 0)
                continue;

            var card = new ActivityCatchUpCard(activity, eligibleDays, ShowCountPromptAsync);
            _cards.Add(card);
            _cardsStack.Children.Add(card.View);
        }

        if (_cards.Count == 0)
        {
            _cardsStack.Children.Add(new Frame
            {
                Padding = 18,
                CornerRadius = 8,
                HasShadow = false,
                BorderColor = Color.FromArgb("#DDDDDD"),
                BackgroundColor = Colors.White,
                Content = new Label
                {
                    Text = "No eligible catch-up days remain for this game.",
                    FontSize = 15,
                    TextColor = Color.FromArgb("#555555")
                }
            });
        }
    }

    public static async Task<bool> HasEligibleCatchUpActivitiesAsync(
        string username,
        Game game,
        ActivityService activities,
        ExpService exp)
    {
        if (!game.LastVisitedAt.HasValue)
            return false;

        var catchUpStart = game.LastVisitedAt.Value.Date.AddDays(1);
        var catchUpEnd = DateTime.Today;
        if (catchUpStart > catchUpEnd)
            return false;

        var days = EachDay(catchUpStart, catchUpEnd).ToList();
        var gameActivities = await activities.GetActivitiesAsync(username, game.GameId);

        foreach (var activity in gameActivities.Where(IsEligibleActivity))
        {
            foreach (var day in days)
            {
                if (!activity.IsScheduledDisplayDay(day))
                    continue;

                var existing = await exp.GetExpLogsForActivityOnDateAsync(
                    username,
                    activity.Game,
                    activity.Id,
                    day);

                if (existing.Count == 0)
                    return true;
            }
        }

        return false;
    }

    public static bool IsEligibleActivity(Activity activity)
    {
        if (!activity.IsActive || activity.IsPossible || activity.IsAutoAward)
            return false;

        if (activity.EndDate.HasValue && activity.EndDate.Value < DateTime.Now)
            return false;

        return !string.Equals(activity.Category, "Expired", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(activity.Category, "Stale", StringComparison.OrdinalIgnoreCase);
    }

    private async void OnApplyClicked(object? sender, EventArgs e)
    {
        if (_isSubmitting)
            return;

        _isSubmitting = true;
        SetButtonsEnabled(false);

        try
        {
            int totalApplications = 0;
            var selectedRows = _cards
                .SelectMany(card => card.Rows.Select(row => (card.Activity, Row: row)))
                .Where(x => x.Row.IsChecked && x.Row.Count >= 1)
                .OrderBy(x => x.Row.Date)
                .ThenBy(x => x.Activity.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in selectedRows)
            {
                var localNoon = DateTime.SpecifyKind(item.Row.Date.Date.AddHours(12), DateTimeKind.Local);
                for (int i = 0; i < item.Row.Count; i++)
                {
                    await ApplyActivityAsync(item.Activity, localNoon);
                    totalApplications++;
                }
            }

            if (_game != null)
                await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, _game.GameId, DateTime.Now);

            await DisplayAlert("Catch-up applied", $"Recorded {totalApplications} application(s).", "OK");
            await NavigateToGameAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Catch-up failed", ex.Message, "OK");
            SetButtonsEnabled(true);
            _isSubmitting = false;
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        if (_isSubmitting)
            return;

        _isSubmitting = true;
        SetButtonsEnabled(false);

        if (_game != null)
            await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, _game.GameId, DateTime.Now);

        await NavigateToGameAsync();
    }

    private async Task ApplyActivityAsync(Activity activity, DateTime localNoon)
    {
        string gameId = activity.Game;
        var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, gameId);
        int baseExp = CalculateExpGain(activity, currentLevel);
        int multiplier = Math.Max(1, activity.Multiplier);

        for (int i = 0; i < multiplier; i++)
        {
            string name = multiplier > 1
                ? $"{activity.Name} (Catch-Up {i + 1}/{multiplier})"
                : $"{activity.Name} (Catch-Up)";

            await _exp.ApplyExpAsync(
                _auth.CurrentUsername,
                gameId,
                name,
                baseExp,
                activity.Id,
                localNoon);
        }

        if (activity.IsStreakTracked)
        {
            await _streaks.RecordActivityUsageAsync(
                _auth.CurrentUsername,
                gameId,
                activity.Id,
                activity.Name,
                activity,
                localNoon);
        }

        if (activity.HabitType != "None")
            await _activities.RecordHabitCompletionAsync(activity, localNoon);

        await _activities.RecordDisplayDayStreakAsync(activity, localNoon);

        activity.TimesCompleted++;
        await _activities.UpdateActivityAsync(activity);

        await _newHabits.RecordHabitDoneAsync(activity.Id, localNoon);

        if (activity.ExpGain > 0)
        {
            int streakBonus = ActivityService.CalculateStreakBonus(activity.DisplayDayStreak);
            if (streakBonus > 0)
            {
                await _exp.ApplyExpAsync(
                    _auth.CurrentUsername,
                    gameId,
                    $"{activity.Name} (Catch-Up Streak Bonus)",
                    streakBonus,
                    activity.Id,
                    localNoon);
            }
        }
    }

    private static int CalculateExpGain(Activity activity, int currentLevel)
    {
        if (activity.RewardType == "PercentOfLevel")
            return ExpEngine.ExpForPercentOfLevel(currentLevel, activity.PercentOfLevel, activity.PercentCutoffLevel);

        return activity.ExpGain;
    }

    private async Task<int?> ShowCountPromptAsync(CatchUpDayRow row)
    {
        string? result = await DisplayPromptAsync(
            "Count",
            $"How many times on {row.Date:MMM d}?",
            "OK",
            "Cancel",
            initialValue: row.Count.ToString(),
            maxLength: 3,
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(result))
            return null;

        if (!int.TryParse(result.Trim(), out int count) || count < 1 || count > 999)
        {
            await DisplayAlert("Invalid", "Enter a whole number from 1 to 999.", "OK");
            return null;
        }

        return count;
    }

    private async Task NavigateToGameAsync()
    {
        string encoded = Uri.EscapeDataString(GameId);
        await Shell.Current.GoToAsync($"../activitygrid?gameId={encoded}");
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _applyButton.IsEnabled = enabled;
        _skipButton.IsEnabled = enabled;
    }

    private static IEnumerable<DateTime> EachDay(DateTime start, DateTime end)
    {
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            yield return day;
    }

    private sealed class ActivityCatchUpCard
    {
        public Activity Activity { get; }
        public List<CatchUpDayRow> Rows { get; }
        public View View { get; }

        public ActivityCatchUpCard(Activity activity, List<DateTime> days, Func<CatchUpDayRow, Task<int?>> showCountPrompt)
        {
            Activity = activity;
            Rows = days.Select(day => new CatchUpDayRow(day, showCountPrompt)).ToList();

            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(new Label
            {
                Text = activity.Name,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222222"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            stack.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(activity.Category) ? "Uncategorized" : activity.Category,
                FontSize = 13,
                TextColor = Color.FromArgb("#666666"),
                Margin = new Thickness(0, -6, 0, 0),
                LineBreakMode = LineBreakMode.WordWrap
            });

            var selectAllButton = new Button
            {
                Text = "Select All Days",
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                TextColor = Color.FromArgb("#2E7D32"),
                CornerRadius = 8,
                HeightRequest = 38,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Start
            };
            selectAllButton.Clicked += (_, _) =>
            {
                foreach (var row in Rows)
                {
                    if (row.IsChecked)
                        continue;

                    row.SetSelected(true, 1);
                }
            };
            stack.Children.Add(selectAllButton);

            foreach (var row in Rows)
                stack.Children.Add(row.View);

            View = new Frame
            {
                Padding = 14,
                CornerRadius = 8,
                HasShadow = false,
                BorderColor = Color.FromArgb("#DDDDDD"),
                BackgroundColor = Colors.White,
                Content = stack
            };
        }
    }

    private sealed class CatchUpDayRow
    {
        private readonly Func<CatchUpDayRow, Task<int?>> _showCountPrompt;
        private readonly CheckBox _checkbox;
        private readonly Label _countLabel;
        private readonly Grid _countGrid;

        public DateTime Date { get; }
        public bool IsChecked { get; private set; }
        public int Count { get; private set; } = 1;
        public View View { get; }

        public CatchUpDayRow(DateTime date, Func<CatchUpDayRow, Task<int?>> showCountPrompt)
        {
            Date = date;
            _showCountPrompt = showCountPrompt;

            _checkbox = new CheckBox
            {
                VerticalOptions = LayoutOptions.Center,
                Color = Color.FromArgb("#2E7D32")
            };

            var dateLabel = new Label
            {
                Text = date.ToString("ddd, MMM d"),
                FontSize = 15,
                TextColor = Color.FromArgb("#333333"),
                VerticalTextAlignment = TextAlignment.Center
            };

            var minus = BuildCountButton("-");
            minus.Clicked += (_, _) => SetCount(Math.Max(1, Count - 1));

            _countLabel = new Label
            {
                Text = Count.ToString(),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222222"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                WidthRequest = 42
            };
            _countLabel.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    int? count = await _showCountPrompt(this);
                    if (count.HasValue)
                        SetCount(count.Value);
                })
            });

            var plus = BuildCountButton("+");
            plus.Clicked += (_, _) => SetCount(Math.Min(999, Count + 1));

            _countGrid = new Grid
            {
                ColumnSpacing = 4,
                IsVisible = false,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            _countGrid.Add(minus, 0, 0);
            _countGrid.Add(_countLabel, 1, 0);
            _countGrid.Add(plus, 2, 0);

            _checkbox.CheckedChanged += (_, e) =>
            {
                IsChecked = e.Value;
                if (IsChecked && Count < 1)
                    SetCount(1);
                _countGrid.IsVisible = IsChecked;
            };

            var row = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            row.Add(_checkbox, 0, 0);
            row.Add(dateLabel, 1, 0);
            row.Add(_countGrid, 2, 0);

            View = row;
        }

        private static Button BuildCountButton(string text)
        {
            return new Button
            {
                Text = text,
                WidthRequest = 34,
                HeightRequest = 34,
                CornerRadius = 17,
                Padding = 0,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#333333")
            };
        }

        private void SetCount(int count)
        {
            Count = Math.Clamp(count, 1, 999);
            _countLabel.Text = Count.ToString();
        }

        public void SetSelected(bool checkedState, int count)
        {
            if (checkedState)
                SetCount(count);

            _checkbox.IsChecked = checkedState;
            IsChecked = checkedState;
            _countGrid.IsVisible = checkedState;
        }
    }
}
