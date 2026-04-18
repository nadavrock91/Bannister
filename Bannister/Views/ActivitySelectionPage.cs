using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Full-screen page for selecting an activity to turn into a habit.
/// Shows habit target info (days since set, postpone count) to help prioritize.
/// </summary>
public class ActivitySelectionPage : ContentPage
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

    public ActivitySelectionPage(ActivityService activities, GameService games, string username, string title, bool negativeOnly = false, bool includeAllStatuses = false)
    {
        _activities = activities;
        _games = games;
        _username = username;
        _negativeOnly = negativeOnly;
        _includeAllStatuses = includeAllStatuses;
        _tcs = new TaskCompletionSource<Activity?>();

        Title = title;
        BackgroundColor = Color.FromArgb("#F5F5F5");

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
        var topBar = new Grid
        {
            BackgroundColor = Color.FromArgb("#5B63EE"),
            Padding = new Thickness(8, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        var btnCancel = new Button
        {
            Text = "← Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(8, 4)
        };
        btnCancel.Clicked += OnCancelClicked;
        topBar.Add(btnCancel, 0, 0);

        var titleLabel = new Label
        {
            Text = _negativeOnly ? "Select Penalty Activity" : "Select Activity for Habit",
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        topBar.Add(titleLabel, 1, 0);

        mainGrid.Add(topBar, 0, 0);

        // Search bar
        _searchBar = new SearchBar
        {
            Placeholder = "Search activities...",
            BackgroundColor = Colors.White,
            CancelButtonColor = Color.FromArgb("#5B63EE")
        };
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
        var headerFrame = new Frame
        {
            BackgroundColor = _negativeOnly ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50"),
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true
        };
        headerFrame.Content = new Label
        {
            Text = _negativeOnly 
                ? "Choose what happens if you miss a day"
                : "Activities with ⚠️ have been waiting longest",
            FontSize = 14,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };
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

        _gamePicker = new Picker
        {
            Title = "All Games",
            BackgroundColor = Colors.White
        };
        _gamePicker.SelectedIndexChanged += OnGameFilterChanged;
        Grid.SetColumn(_gamePicker, 0);
        filterGrid.Children.Add(_gamePicker);

        _categoryPicker = new Picker
        {
            Title = "All Categories",
            BackgroundColor = Colors.White,
            IsEnabled = false
        };
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

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.Trim() ?? "";
        DisplayFilteredActivities();
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
            _activityList.Children.Add(new Label
            {
                Text = string.IsNullOrEmpty(_searchText) 
                    ? "No activities found" 
                    : $"No activities matching '{_searchText}'",
                TextColor = Color.FromArgb("#666"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
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

        foreach (var activity in sorted)
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
        _gamePicker.ItemsSource = gameOptions;
        _gamePicker.SelectedIndex = 0;

        await LoadActivitiesAsync();
    }

    private async void OnGameFilterChanged(object? sender, EventArgs e)
    {
        if (_gamePicker.SelectedIndex <= 0)
        {
            _selectedGameId = null;
            _categoryPicker.IsEnabled = false;
            _categoryPicker.ItemsSource = null;
        }
        else
        {
            var selectedGame = _allGames[_gamePicker.SelectedIndex - 1];
            _selectedGameId = selectedGame.GameId;
            
            var activities = await _activities.GetActivitiesAsync(_username, _selectedGameId);
            var categories = activities.Select(a => a.Category).Distinct().OrderBy(c => c).ToList();
            
            var categoryOptions = new List<string> { "All Categories" };
            categoryOptions.AddRange(categories);
            _categoryPicker.ItemsSource = categoryOptions;
            _categoryPicker.SelectedIndex = 0;
            _categoryPicker.IsEnabled = true;
        }

        await LoadActivitiesAsync();
    }

    private async void OnCategoryFilterChanged(object? sender, EventArgs e)
    {
        await LoadActivitiesAsync();
    }

    private async Task LoadActivitiesAsync()
    {
        _activityList.Children.Clear();
        _activityList.Children.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#5B63EE") });

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
                _currentActivities = allActivities.Where(a => a.ExpGain > 0 && a.Category != "Negative" && a.IsActive && !a.IsPossible).ToList();
            }

            _activityList.Children.Clear();
            DisplayFilteredActivities();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECTION] Error loading activities: {ex.Message}");
            _activityList.Children.Clear();
            _activityList.Children.Add(new Label
            {
                Text = $"Error loading activities: {ex.Message}",
                TextColor = Colors.Red,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
        }
    }

    private Frame CreateActivityCard(Activity activity)
    {
        var hasTargetDate = activity.HabitTargetDate.HasValue;
        var hasTargetInfo = activity.HabitTargetFirstSet.HasValue;
        var isOverdue = activity.HabitTargetDate.HasValue && activity.HabitTargetDate.Value.Date < DateTime.Now.Date;
        
        Color bgColor;
        if (isOverdue)
            bgColor = Color.FromArgb("#FFEBEE");
        else if (hasTargetInfo && activity.HabitTargetPostponeCount >= 2)
            bgColor = Color.FromArgb("#FFF3E0");
        else if (hasTargetInfo && activity.DaysSinceHabitTargetSet >= 30)
            bgColor = Color.FromArgb("#FFFDE7");
        else if (hasTargetDate)
            bgColor = Color.FromArgb("#E8F5E9");
        else
            bgColor = Colors.White;

        var frame = new Frame
        {
            BackgroundColor = bgColor,
            Padding = 12,
            CornerRadius = 10,
            HasShadow = true
        };

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
        
        if (hasTargetInfo && (activity.DaysSinceHabitTargetSet >= 14 || activity.HabitTargetPostponeCount >= 1))
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
        
        nameStack.Children.Add(new Label
        {
            Text = activity.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        });
        grid.Add(nameStack, 0, 0);

        grid.Add(new Label
        {
            Text = $"{activity.ExpGain:+#;-#;0} EXP",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = activity.ExpGain >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336"),
            HorizontalTextAlignment = TextAlignment.End
        }, 1, 0);

        // Row 1: Game name
        grid.Add(new Label
        {
            Text = $"📁 {activity.Game}",
            FontSize = 11,
            TextColor = Color.FromArgb("#666")
        }, 0, 1);

        // Row 2: Habit target info
        if (hasTargetDate || hasTargetInfo)
        {
            var infoStack = new HorizontalStackLayout { Spacing = 8 };
            
            if (hasTargetInfo)
            {
                var daysSince = activity.DaysSinceHabitTargetSet;
                var daysColor = daysSince >= 30 ? Color.FromArgb("#D32F2F") :
                               daysSince >= 14 ? Color.FromArgb("#F57C00") :
                               Color.FromArgb("#666");
                infoStack.Children.Add(new Label
                {
                    Text = $"🕐 {daysSince}d waiting",
                    FontSize = 11,
                    TextColor = daysColor,
                    FontAttributes = daysSince >= 14 ? FontAttributes.Bold : FontAttributes.None
                });
            }

            if (activity.HabitTargetPostponeCount > 0)
            {
                var postponeColor = activity.HabitTargetPostponeCount >= 3 ? Color.FromArgb("#D32F2F") :
                                   activity.HabitTargetPostponeCount >= 2 ? Color.FromArgb("#F57C00") :
                                   Color.FromArgb("#666");
                infoStack.Children.Add(new Label
                {
                    Text = $"🔄 {activity.HabitTargetPostponeCount}x postponed",
                    FontSize = 11,
                    TextColor = postponeColor,
                    FontAttributes = activity.HabitTargetPostponeCount >= 2 ? FontAttributes.Bold : FontAttributes.None
                });
            }

            if (hasTargetDate && !hasTargetInfo)
            {
                infoStack.Children.Add(new Label
                {
                    Text = $"📅 Target: {activity.HabitTargetDate!.Value:MMM dd}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#388E3C")
                });
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
