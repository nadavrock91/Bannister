using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Dashboard page showing all streak-tracked activities across all games
/// with collapsible attempt cards under each activity
/// </summary>
public class StreakDashboardPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly StreakService _streaks;
    private readonly GameService _games;

    private VerticalStackLayout _activitiesContainer;
    private ScrollView _mainScroll;
    private Dictionary<int, bool> _expandedActivities = new(); // Track which activities are expanded
    private double _savedScrollY = 0; // Save scroll position

    public StreakDashboardPage(AuthService auth, ActivityService activities, StreakService streaks, GameService games)
    {
        _auth = auth;
        _activities = activities;
        _streaks = streaks;
        _games = games;

        Title = "Streak Tracking";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Check and break expired streaks AND auto-increment across all games
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);
        foreach (var game in allGames)
        {
            await _streaks.CheckAndBreakExpiredStreaksAsync(_auth.CurrentUsername, game.GameId, _activities);
            await _streaks.AutoIncrementStreaksAsync(_auth.CurrentUsername, game.GameId, _activities);
        }
        
        await LoadActivitiesAsync();
    }

    private void BuildUI()
    {
        _mainScroll = new ScrollView();
        
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#FF9800"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 4 };

        headerStack.Children.Add(new Label
        {
            Text = "🔥 Streak Dashboard",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        headerStack.Children.Add(new Label
        {
            Text = "Track consecutive days of activity usage",
            FontSize = 13,
            TextColor = Color.FromArgb("#FFFFFFCC")
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Info
        mainStack.Children.Add(new Label
        {
            Text = "💡 Enable streak tracking in Edit Activity to see activities here",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        });

        // Activities list
        _activitiesContainer = new VerticalStackLayout { Spacing = 16 };
        mainStack.Children.Add(_activitiesContainer);

        _mainScroll.Content = mainStack;
        Content = _mainScroll;
    }

    private async Task LoadActivitiesAsync()
    {
        _activitiesContainer.Children.Clear();

        // Get all games
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);
        
        // Collect all streak-tracked activities across all games
        var allTrackedData = new List<(Activity activity, List<StreakAttempt> attempts, string gameName)>();

        foreach (var game in allGames)
        {
            var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, game.GameId);
            var trackedActivities = activities.Where(a => a.IsStreakTracked).ToList();

            foreach (var activity in trackedActivities)
            {
                var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, game.GameId, activity.Id);
                allTrackedData.Add((activity, attempts, game.DisplayName));
            }
        }

        if (allTrackedData.Count == 0)
        {
            _activitiesContainer.Children.Add(new Label
            {
                Text = "No activities are being tracked for streaks.\n\nGo to Edit Activity and enable 'Track as Streak' to start tracking!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        // Sort alphabetically by activity name
        allTrackedData = allTrackedData.OrderBy(x => x.activity.Name).ToList();

        foreach (var (activity, attempts, gameName) in allTrackedData)
        {
            _activitiesContainer.Children.Add(BuildActivitySection(activity, attempts, gameName));
        }
    }

    private View BuildActivitySection(Activity activity, List<StreakAttempt> attempts, string gameName)
    {
        var container = new VerticalStackLayout { Spacing = 8 };

        // Sort attempts: newest first (by attempt number descending)
        var sortedAttempts = attempts.OrderByDescending(a => a.AttemptNumber).ToList();
        var activeStreak = sortedAttempts.FirstOrDefault(a => a.IsActive);
        var hasActiveStreak = activeStreak != null;

        // Initialize expanded state if not set
        if (!_expandedActivities.ContainsKey(activity.Id))
        {
            _expandedActivities[activity.Id] = false;
        }

        bool isExpanded = _expandedActivities[activity.Id];

        // Activity header card
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = hasActiveStreak ? Color.FromArgb("#E8F5E9") : Colors.White,
            BorderColor = hasActiveStreak ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        // Title row with expand button
        var titleRow = new HorizontalStackLayout { Spacing = 12 };

        // Expand/Collapse button
        var btnExpand = new Button
        {
            Text = isExpanded ? $"▼ ({sortedAttempts.Count})" : $"▶ ({sortedAttempts.Count})",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#5B63EE"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 30,
            Padding = new Thickness(4, 0)
        };
        btnExpand.Clicked += (s, e) =>
        {
            _expandedActivities[activity.Id] = !_expandedActivities[activity.Id];
            _ = LoadActivitiesAsync(); // Refresh
        };
        titleRow.Children.Add(btnExpand);

        titleRow.Children.Add(new Label
        {
            Text = activity.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        });

        if (hasActiveStreak)
        {
            titleRow.Children.Add(new Label
            {
                Text = "🔥 Active",
                FontSize = 14,
                TextColor = Color.FromArgb("#FF9800"),
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            });
        }

        headerStack.Children.Add(titleRow);

        // Game name
        headerStack.Children.Add(new Label
        {
            Text = $"📁 {gameName}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(40, 0, 0, 0)
        });

        // Current streak summary
        if (hasActiveStreak)
        {
            var streakRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 4, 0, 0) };

            // Fire emoji
            streakRow.Children.Add(new Label
            {
                Text = "🔥",
                FontSize = 24,
                VerticalOptions = LayoutOptions.Center
            });

            streakRow.Children.Add(new Label
            {
                Text = activeStreak!.DaysAchieved.ToString(),
                FontSize = 28,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF9800"),
                VerticalOptions = LayoutOptions.Center
            });

            streakRow.Children.Add(new Label
            {
                Text = activeStreak.DaysAchieved == 1 ? "day" : "days",
                FontSize = 14,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Edit button (pencil)
            var btnEdit = new Button
            {
                Text = "✏️",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#5B63EE"),
                FontSize = 18,
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            btnEdit.Clicked += async (s, e) => await EditActiveStreakAsync(activeStreak!);
            streakRow.Children.Add(btnEdit);

            // History button (chart)
            var btnHistory = new Button
            {
                Text = "📊",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#5B63EE"),
                FontSize = 18,
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            btnHistory.Clicked += async (s, e) => await ShowStreakHistoryAsync(activeStreak!);
            streakRow.Children.Add(btnHistory);

            // End button
            var btnEnd = new Button
            {
                Text = "End",
                BackgroundColor = Color.FromArgb("#F44336"),
                TextColor = Colors.White,
                FontSize = 12,
                HeightRequest = 28,
                CornerRadius = 6,
                Padding = new Thickness(12, 0),
                VerticalOptions = LayoutOptions.Center
            };
            btnEnd.Clicked += async (s, e) => await EndStreakAsync(activeStreak!);
            streakRow.Children.Add(btnEnd);

            headerStack.Children.Add(streakRow);
        }
        else
        {
            headerStack.Children.Add(new Label
            {
                Text = sortedAttempts.Count == 0 ? "No attempts yet" : "No active streak",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(40, 0, 0, 0)
            });
        }

        // Buttons row with Add Past Streak
        var buttonsRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 8, 0, 0) };

        // Add Past Streak button
        var btnAddPast = new Button
        {
            Text = "+ Add Past Streak",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        btnAddPast.Clicked += async (s, e) => await ShowAddPastStreakMenuAsync(activity);
        buttonsRow.Children.Add(btnAddPast);

        headerStack.Children.Add(buttonsRow);

        // Auto-Increment toggle row (separate row for clarity)
        var autoRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 4, 0, 0) };

        var chkAutoIncrement = new CheckBox
        {
            IsChecked = activity.IsStreakAutoIncrement,
            Color = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Center
        };
        chkAutoIncrement.CheckedChanged += async (s, e) =>
        {
            activity.IsStreakAutoIncrement = e.Value;
            await _activities.UpdateActivityAsync(activity);
            
            // If enabling auto-increment and there's no active streak, create one
            if (e.Value && !hasActiveStreak)
            {
                await _streaks.GetOrCreateActiveStreakAsync(_auth.CurrentUsername, activity.Game, activity.Id, activity.Name);
                _savedScrollY = _mainScroll.ScrollY;
                await LoadActivitiesAsync();
                await RestoreScrollPositionAsync();
            }
        };
        autoRow.Children.Add(chkAutoIncrement);

        autoRow.Children.Add(new Label
        {
            Text = "Auto-increment daily (no click needed)",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        // Show indicator if auto-increment is enabled
        if (activity.IsStreakAutoIncrement)
        {
            autoRow.Children.Add(new Label
            {
                Text = "⚡",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
        }

        headerStack.Children.Add(autoRow);

        headerFrame.Content = headerStack;
        container.Children.Add(headerFrame);

        // Attempt cards (if expanded)
        if (isExpanded && sortedAttempts.Count > 0)
        {
            var attemptsContainer = new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
                AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start,
                Margin = new Thickness(24, 0, 0, 0)
            };

            foreach (var attempt in sortedAttempts)
            {
                attemptsContainer.Children.Add(BuildAttemptCard(attempt));
            }

            container.Children.Add(attemptsContainer);
        }

        return container;
    }

    private Frame BuildAttemptCard(StreakAttempt attempt)
    {
        var isActive = attempt.IsActive;
        var borderColor = isActive ? Color.FromArgb("#4CAF50") : Color.FromArgb("#BDBDBD");
        var bgColor = isActive ? Color.FromArgb("#C8E6C9") : Color.FromArgb("#FAFAFA");

        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = bgColor,
            BorderColor = borderColor,
            HasShadow = false,
            WidthRequest = 120,
            Margin = new Thickness(0, 4, 8, 4)
        };

        // Make non-active cards tappable to edit
        if (!isActive)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) => await EditPastStreakAsync(attempt);
            frame.GestureRecognizers.Add(tapGesture);
        }

        var stack = new VerticalStackLayout { Spacing = 4 };

        // Attempt number header
        stack.Children.Add(new Label
        {
            Text = $"Attempt #{attempt.AttemptNumber}",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#2E7D32") : Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Days achieved (big)
        stack.Children.Add(new Label
        {
            Text = attempt.DaysAchieved.ToString(),
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#FF9800") : Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // "days" label
        stack.Children.Add(new Label
        {
            Text = attempt.DaysAchieved == 1 ? "day" : "days",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Status
        if (isActive)
        {
            stack.Children.Add(new Label
            {
                Text = "🔥 Active",
                FontSize = 10,
                TextColor = Color.FromArgb("#FF9800"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        else
        {
            // Show "tap to edit" hint for past attempts
            stack.Children.Add(new Label
            {
                Text = "tap to edit",
                FontSize = 9,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        frame.Content = stack;
        return frame;
    }

    private async Task EditPastStreakAsync(StreakAttempt attempt)
    {
        string daysStr = await DisplayPromptAsync(
            $"Edit Attempt #{attempt.AttemptNumber}",
            $"Current: {attempt.DaysAchieved} days\n\nEnter new number of days:",
            initialValue: attempt.DaysAchieved.ToString(),
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 7");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int newDays) || newDays < 1)
        {
            return;
        }

        await _streaks.UpdateStreakDaysAsync(attempt.Id, newDays);
        
        // Keep this activity expanded and save scroll position
        _expandedActivities[attempt.ActivityId] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadActivitiesAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task ShowAddPastStreakMenuAsync(Activity activity)
    {
        string daysStr = await DisplayPromptAsync(
            "Add Past Streak",
            "How many days did this past streak last?",
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 7");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int days) || days < 1)
        {
            return;
        }

        // Get the next attempt number (handles bumping active streak if needed)
        int newAttemptNumber = await _streaks.GetNextPastAttemptNumberAsync(
            _auth.CurrentUsername, activity.Game, activity.Id);

        // Get attempts to check if there's an active one to bump
        var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, activity.Game, activity.Id);
        var activeStreak = attempts.FirstOrDefault(a => a.IsActive);

        // If there's an active streak at this number, bump it and all after it
        if (activeStreak != null && activeStreak.AttemptNumber == newAttemptNumber)
        {
            await _streaks.BumpAttemptNumbersFromAsync(
                _auth.CurrentUsername,
                activity.Game,
                activity.Id,
                newAttemptNumber);
        }

        await _streaks.AddManualStreakAsync(
            _auth.CurrentUsername,
            activity.Game,
            activity.Id,
            activity.Name,
            newAttemptNumber,
            days,
            null,
            null);

        // Auto-expand this activity and save scroll position
        _expandedActivities[activity.Id] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadActivitiesAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task EndStreakAsync(StreakAttempt activeStreak)
    {
        bool confirm = await DisplayAlert(
            "End Streak",
            $"End the current streak of {activeStreak.DaysAchieved} days for \"{activeStreak.ActivityName}\"?\n\nThis cannot be undone.",
            "End Streak",
            "Cancel");

        if (confirm)
        {
            await _streaks.EndStreakAsync(activeStreak.Id);
            
            _expandedActivities[activeStreak.ActivityId] = true;
            _savedScrollY = _mainScroll.ScrollY;
            await LoadActivitiesAsync();
            await RestoreScrollPositionAsync();
        }
    }

    private async Task EditActiveStreakAsync(StreakAttempt activeStreak)
    {
        string daysStr = await DisplayPromptAsync(
            "Edit Active Streak",
            $"Current streak: {activeStreak.DaysAchieved} days\n\nEnter the correct number of days:",
            initialValue: activeStreak.DaysAchieved.ToString(),
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 5");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int newDays) || newDays < 1)
        {
            return;
        }

        await _streaks.UpdateStreakDaysAsync(activeStreak.Id, newDays);
        
        // Keep this activity expanded and save scroll position
        _expandedActivities[activeStreak.ActivityId] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadActivitiesAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task ShowStreakHistoryAsync(StreakAttempt streak)
    {
        var historyPage = new StreakHistoryPage(_streaks, streak);
        await Navigation.PushAsync(historyPage);
    }

    private async Task RestoreScrollPositionAsync()
    {
        // Small delay to let the UI rebuild
        await Task.Delay(50);
        await _mainScroll.ScrollToAsync(0, _savedScrollY, false);
    }
}
