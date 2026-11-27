using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Full-screen page for selecting an activity to turn into a habit.
/// Shows habit target info (days since set, postpone count) to help prioritize.
/// </summary>
public class ActivitySelectionPage : Bannister.Views.ModeThemedPage
{
    private readonly ActivityService _activities;
    private readonly GameService _games;
    private readonly string _username;
    private readonly bool _negativeOnly;
    private readonly bool _includeAllStatuses;
    private readonly TaskCompletionSource<Activity?> _tcs;

    private VerticalStackLayout _activityList;
    private Picker _gamePicker;
    private Picker _categoryPicker;
    private SearchBar _searchBar;
    private List<Game> _allGames = new();
    private List<Activity> _currentActivities = new();
    private string? _selectedGameId = null;
    private string _searchText = "";
    private bool _isInitializing;
    private bool _updatingCategoryPicker;
    private const int MaxRenderedActivities = 100;

    public ActivitySelectionPage(ActivityService activities, GameService games, string username, string title, bool negativeOnly = false, bool includeAllStatuses = false)
    {
        _activities = activities;
        _games = games;
        _username = username;
        _negativeOnly = negativeOnly;
        _includeAllStatuses = includeAllStatuses;
        _tcs = new TaskCompletionSource<Activity?>();

        Title = title;


        BuildUI();
        _ = LoadGamesAsync();
    }

    public Task<Activity?> GetSelectedActivityAsync() => _tcs.Task;

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }, // Top bar
                new RowDefinition { Height = GridLength.Auto }, // Search bar
                new RowDefinition { Height = GridLength.Star }  // Scrollable content
            }
        };

        // Top bar with Cancel button
        var topBar = (new Grid
        {
            Padding = new Thickness(8, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground);

        var btnCancel = (new Button
        {
            Text = "← Cancel",
            BackgroundColor = Colors.Transparent,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(8, 4)
        }).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnCancel.Clicked += OnCancelClicked;
        topBar.Add(btnCancel, 0, 0);

        var titleLabel = (new Label
        {
            Text = _negativeOnly ? "Select Penalty Activity" : "Select Activity for Habit",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true);
        topBar.Add(titleLabel, 1, 0);

        mainGrid.Add(topBar, 0, 0);

        // Search bar
        _searchBar = (new SearchBar
        {
            Placeholder = "Search activities..."
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(SearchBar.CancelButtonColorProperty, ThemeKey.HighlightIndigoText);
        _searchBar.TextChanged += OnSearchTextChanged;
        mainGrid.Add(_searchBar, 0, 1);

        // Scrollable content
        var scrollView = new ScrollView
        {
            VerticalOptions = LayoutOptions.Fill
        };
        
        var contentStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        // Header info
        var headerFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, _negativeOnly ? ThemeKey.ErrorButtonBackground : ThemeKey.SuccessButtonBackground);
        headerFrame.Content = (new Label
        {
            Text = _negativeOnly 
                ? "Choose what happens if you miss a day"
                : "Activities with ⚠️ have been waiting longest",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true);
        contentStack.Children.Add(headerFrame);

        // Filters
        var filterGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };

        _gamePicker = (new Picker
        {
            Title = "All Games"
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _gamePicker.SelectedIndexChanged += OnGameFilterChanged;
        Grid.SetColumn(_gamePicker, 0);
        filterGrid.Children.Add(_gamePicker);

        _categoryPicker = (new Picker
        {
            Title = "All Categories",
            IsEnabled = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _categoryPicker.SelectedIndexChanged += OnCategoryFilterChanged;
        Grid.SetColumn(_categoryPicker, 1);
        filterGrid.Children.Add(_categoryPicker);

        contentStack.Children.Add(filterGrid);

        // Activity list
        _activityList = new VerticalStackLayout { Spacing = 8 };
        contentStack.Children.Add(_activityList);

        scrollView.Content = contentStack;
        mainGrid.Add(scrollView, 0, 2);

        Content = mainGrid;
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.Trim() ?? "";
        await LoadActivitiesAsync();
    }

    private bool ShouldLoadActivities() =>
        _selectedGameId != null || _searchText.Length >= 2;

    private void ShowSelectionHint()
    {
        _activityList.Children.Clear();
        _activityList.Children.Add((new Label
        {
            Text = "Search by activity name or pick a game to show activities.",
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 20)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
    }

    private void DisplayFilteredActivities()
    {
        _activityList.Children.Clear();

        var filtered = _currentActivities;

        // Apply search filter - case insensitive, must contain exact search text
        if (!string.IsNullOrEmpty(_searchText))
        {
            filtered = filtered.Where(a => 
                a.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                a.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                a.Game.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        if (filtered.Count == 0)
        {
            _activityList.Children.Add((new Label
            {
                Text = string.IsNullOrEmpty(_searchText) 
                    ? "No activities found" 
                    : $"No activities matching '{_searchText}'",
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        // Sort: prioritize activities with habit targets that have been waiting longest
        var sorted = filtered
            .OrderByDescending(a => a.HabitTargetDate.HasValue)
            .ThenByDescending(a => a.HabitTargetFirstSet.HasValue)
            .ThenByDescending(a => a.HabitTargetPostponeCount)
            .ThenByDescending(a => a.DaysSinceHabitTargetSet)
            .ThenBy(a => a.HabitTargetDate ?? DateTime.MaxValue)
            .ThenBy(a => a.Game)
            .ThenBy(a => a.Name)
            .ToList();

        var shown = sorted.Take(MaxRenderedActivities).ToList();

        if (sorted.Count > MaxRenderedActivities)
        {
            _activityList.Children.Add((new Label
            {
                Text = $"Showing first {MaxRenderedActivities} of {sorted.Count}. Search or filter more to narrow results.",
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        }

        foreach (var activity in shown)
        {
            _activityList.Children.Add(CreateActivityCard(activity));
        }
    }

    private async Task LoadGamesAsync()
    {
        try
        {
            await _activities.MigrateHabitTargetFirstSetAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECTION] Migration error: {ex.Message}");
        }
        
        _allGames = await _games.GetGamesAsync(_username);
        
        var gameOptions = new List<string> { "All Games" };
        gameOptions.AddRange(_allGames.Select(g => g.DisplayName));
        _isInitializing = true;
        _gamePicker.ItemsSource = gameOptions;
        _gamePicker.SelectedIndex = 0;
        _isInitializing = false;
        ShowSelectionHint();
    }

    private async void OnGameFilterChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;

        if (_gamePicker.SelectedIndex <= 0)
        {
            _selectedGameId = null;
            _updatingCategoryPicker = true;
            _categoryPicker.IsEnabled = false;
            _categoryPicker.ItemsSource = null;
            _updatingCategoryPicker = false;
        }
        else
        {
            var selectedGame = _allGames[_gamePicker.SelectedIndex - 1];
            _selectedGameId = selectedGame.GameId;
            
            var activities = await _activities.GetActivitiesAsync(_username, _selectedGameId);
            var categories = activities.Select(a => a.Category).Distinct().OrderBy(c => c).ToList();
            
            var categoryOptions = new List<string> { "All Categories" };
            categoryOptions.AddRange(categories);
            _updatingCategoryPicker = true;
            _categoryPicker.ItemsSource = categoryOptions;
            _categoryPicker.SelectedIndex = 0;
            _categoryPicker.IsEnabled = true;
            _updatingCategoryPicker = false;
        }

        await LoadActivitiesAsync();
    }

    private async void OnCategoryFilterChanged(object? sender, EventArgs e)
    {
        if (_updatingCategoryPicker) return;
        await LoadActivitiesAsync();
    }

    private async Task LoadActivitiesAsync()
    {
        _activityList.Children.Clear();
        if (!ShouldLoadActivities())
        {
            _currentActivities.Clear();
            ShowSelectionHint();
            return;
        }

        _activityList.Children.Add((new ActivityIndicator { IsRunning = true }).ThemeColor(ActivityIndicator.ColorProperty, ThemeKey.HighlightIndigoText));

        try
        {
            List<Activity> allActivities = new();

            if (_selectedGameId == null)
            {
                foreach (var game in _allGames)
                {
                    var gameActivities = await _activities.GetActivitiesAsync(_username, game.GameId);
                    allActivities.AddRange(gameActivities);
                }
            }
            else
            {
                allActivities = await _activities.GetActivitiesAsync(_username, _selectedGameId);
                
                if (_categoryPicker.SelectedIndex > 0 && _categoryPicker.SelectedItem is string category)
                {
                    allActivities = allActivities.Where(a => a.Category == category).ToList();
                }
            }

            // Filter for positive or negative
            if (_negativeOnly)
            {
                _currentActivities = allActivities.Where(a => a.ExpGain < 0 || a.Category == "Negative").ToList();
            }
            else if (_includeAllStatuses)
            {
                // Include all positive activities regardless of status (Active, Possible, Stale, Expired)
                _currentActivities = allActivities.Where(a => a.ExpGain > 0 && a.Category != "Negative").ToList();
            }
            else
            {
                // Include streak-tracked activities regardless of active/exp status
                // This allows activities being tracked for streaks to be added as habits
                _currentActivities = allActivities.Where(a => 
                    (a.ExpGain > 0 && a.Category != "Negative" && a.IsActive && !a.IsPossible) ||
                    (a.IsStreakTracked && a.Category != "Negative")
                ).ToList();
            }

            _activityList.Children.Clear();
            DisplayFilteredActivities();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECTION] Error loading activities: {ex.Message}");
            _activityList.Children.Clear();
            _activityList.Children.Add((new Label
            {
                Text = $"Error loading activities: {ex.Message}",
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText1, classifyPageText: true));
        }
    }

    private Frame CreateActivityCard(Activity activity)
    {
        var hasTargetDate = activity.HabitTargetDate.HasValue;
        var hasTargetInfo = activity.HabitTargetFirstSet.HasValue;
        var isOverdue = activity.HabitTargetDate.HasValue && activity.HabitTargetDate.Value.Date < DateTime.Now.Date;
        
        ThemeKey bgColor;
        if (isOverdue)
            bgColor = ThemeKey.ErrorBackground;
        else if (hasTargetInfo && activity.HabitTargetPostponeCount >= 2)
            bgColor = ThemeKey.WarningBackground;
        else if (hasTargetInfo && activity.DaysSinceHabitTargetSet >= 30)
            bgColor = ThemeKey.HighlightYellow;
        else if (hasTargetDate)
            bgColor = ThemeKey.SuccessBackground;
        else
            bgColor = ThemeKey.CardBackground;

        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, bgColor);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 4
        };

        // Row 0: Activity name + EXP
        var nameStack = new HorizontalStackLayout { Spacing = 6 };
        
        // Show streak indicator for streak-tracked activities
        if (activity.IsStreakTracked)
        {
            nameStack.Children.Add(new Label
            {
                Text = "🔥",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            });
        }
        else if (hasTargetInfo && (activity.DaysSinceHabitTargetSet >= 14 || activity.HabitTargetPostponeCount >= 1))
        {
            nameStack.Children.Add(new Label
            {
                Text = "⚠️",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            });
        }
        else if (hasTargetDate)
        {
            nameStack.Children.Add(new Label
            {
                Text = "🎯",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            });
        }
        
        if (activity.IsImageOnly &&
            !string.IsNullOrWhiteSpace(activity.ImagePath))
        {
            string resolvedPath =
                Path.IsPathRooted(activity.ImagePath)
                ? (File.Exists(activity.ImagePath)
                    ? activity.ImagePath
                    : Path.Combine(
                        FileSystem.AppDataDirectory,
                        "ActivityImages",
                        Path.GetFileName(activity.ImagePath)))
                : Path.Combine(
                    FileSystem.AppDataDirectory,
                    "ActivityImages",
                    activity.ImagePath);

            if (File.Exists(resolvedPath))
                nameStack.Children.Add(new Image
                {
                    Source = ImageSource.FromFile(resolvedPath),
                    HeightRequest = 50,
                    WidthRequest = 50,
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Start
                });
            else
                nameStack.Children.Add((new Label
                {
                    Text = "",
                    FontSize = 24
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
        }
        else
        {
            nameStack.Children.Add((new Label
            {
                Text = activity.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        }
        grid.Add(nameStack, 0, 0);

        // Show EXP or streak info
        string expText = activity.ExpGain != 0 
            ? $"{activity.ExpGain:+#;-#;0} EXP" 
            : (activity.IsStreakTracked ? $"🔥{activity.DisplayDayStreak}" : "0 EXP");
        
        grid.Add((new Label
        {
            Text = expText,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End
        }).ThemeColor(Label.TextColorProperty, activity.ExpGain >= 0 ? ThemeKey.SuccessButtonBackground : ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true), 1, 0);

        // Row 1: Game name
        grid.Add((new Label
        {
            Text = $"📁 {activity.Game}",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true), 0, 1);

        // Row 2: Habit target info
        if (hasTargetDate || hasTargetInfo)
        {
            var infoStack = new HorizontalStackLayout { Spacing = 8 };
            
            if (hasTargetInfo)
            {
                var daysSince = activity.DaysSinceHabitTargetSet;
                var daysColor = daysSince >= 30 ? ThemeKey.ErrorText : daysSince >= 14 ? ThemeKey.WarningText : ThemeKey.CardText2;
                infoStack.Children.Add((new Label
                {
                    Text = $"🕐 {daysSince}d waiting",
                    FontSize = 11,
                    FontAttributes = daysSince >= 14 ? FontAttributes.Bold : FontAttributes.None
                }).ThemeColor(Label.TextColorProperty, daysColor, ThemeKey.PageText3, classifyPageText: true));
            }

            if (activity.HabitTargetPostponeCount > 0)
            {
                var postponeColor = activity.HabitTargetPostponeCount >= 3 ? ThemeKey.ErrorText : activity.HabitTargetPostponeCount >= 2 ? ThemeKey.WarningText : ThemeKey.CardText2;
                infoStack.Children.Add((new Label
                {
                    Text = $"🔄 {activity.HabitTargetPostponeCount}x postponed",
                    FontSize = 11,
                    FontAttributes = activity.HabitTargetPostponeCount >= 2 ? FontAttributes.Bold : FontAttributes.None
                }).ThemeColor(Label.TextColorProperty, postponeColor, ThemeKey.PageText3, classifyPageText: true));
            }

            if (hasTargetDate && !hasTargetInfo)
            {
                infoStack.Children.Add((new Label
                {
                    Text = $"📅 Target: {activity.HabitTargetDate!.Value:MMM dd}",
                    FontSize = 11
                }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText3, classifyPageText: true));
            }

            Grid.SetColumnSpan(infoStack, 2);
            grid.Add(infoStack, 0, 2);
        }

        frame.Content = grid;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => {
            _tcs.TrySetResult(activity);
            Navigation.PopModalAsync();
        };
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
