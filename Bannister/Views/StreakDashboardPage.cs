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

        // Create the attempts container FIRST (before the button so we can reference it)
        var attemptsContainer = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start,
            Margin = new Thickness(24, 0, 0, 0),
            IsVisible = isExpanded // Set initial visibility
        };

        // Populate attempts container
        if (sortedAttempts.Count > 0)
        {
            foreach (var attempt in sortedAttempts)
            {
                attemptsContainer.Children.Add(BuildAttemptCard(attempt, sortedAttempts.Count));
            }
        }

        // Expand/Collapse button - toggles visibility without rebuilding
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
            // Toggle expanded state
            _expandedActivities[activity.Id] = !_expandedActivities[activity.Id];
            bool nowExpanded = _expandedActivities[activity.Id];
            
            // Update button text
            btnExpand.Text = nowExpanded ? $"▼ ({sortedAttempts.Count})" : $"▶ ({sortedAttempts.Count})";
            
            // Toggle visibility of attempts container (no rebuild needed!)
            attemptsContainer.IsVisible = nowExpanded;
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

        // Current streak info
        if (activeStreak != null)
        {
            var streakRow = new HorizontalStackLayout 
            { 
                Spacing = 8,
                Margin = new Thickness(40, 4, 0, 0)
            };

            streakRow.Children.Add(new Label
            {
                Text = "🔥",
                FontSize = 20,
                VerticalOptions = LayoutOptions.Center
            });

            streakRow.Children.Add(new Label
            {
                Text = activeStreak.DaysAchieved.ToString(),
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF9800"),
                VerticalOptions = LayoutOptions.Center
            });

            streakRow.Children.Add(new Label
            {
                Text = activeStreak.DaysAchieved == 1 ? "day" : "days",
                FontSize = 14,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.Center
            });

            // Edit button
            var btnEdit = new Button
            {
                Text = "✏️",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#666"),
                FontSize = 16,
                HeightRequest = 30,
                WidthRequest = 30,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            btnEdit.Clicked += async (s, e) => await EditActiveStreakAsync(activeStreak);
            streakRow.Children.Add(btnEdit);

            // History button
            var btnHistory = new Button
            {
                Text = "📊",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#666"),
                FontSize = 16,
                HeightRequest = 30,
                WidthRequest = 30,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            btnHistory.Clicked += async (s, e) => await ShowStreakHistoryAsync(activeStreak);
            streakRow.Children.Add(btnHistory);

            // End button
            var btnEnd = new Button
            {
                Text = "End",
                BackgroundColor = Color.FromArgb("#E53935"),
                TextColor = Colors.White,
                FontSize = 11,
                CornerRadius = 4,
                HeightRequest = 28,
                Padding = new Thickness(8, 0),
                VerticalOptions = LayoutOptions.Center
            };
            btnEnd.Clicked += async (s, e) => await EndStreakAsync(activeStreak);
            streakRow.Children.Add(btnEnd);

            headerStack.Children.Add(streakRow);
        }
        else if (sortedAttempts.Count > 0)
        {
            // Show best past streak
            var bestStreak = sortedAttempts.OrderByDescending(a => a.DaysAchieved).First();
            headerStack.Children.Add(new Label
            {
                Text = $"Best: {bestStreak.DaysAchieved} days (Attempt #{bestStreak.AttemptNumber})",
                FontSize = 12,
                TextColor = Color.FromArgb("#666"),
                Margin = new Thickness(40, 0, 0, 0)
            });
        }
        else
        {
            headerStack.Children.Add(new Label
            {
                Text = "No attempts yet",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(40, 0, 0, 0)
            });
        }

        // Add Past Streak button
        var btnAddPast = new Button
        {
            Text = "+ Add Past Streak",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 32,
            Padding = new Thickness(12, 0),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(40, 4, 0, 0)
        };
        btnAddPast.Clicked += async (s, e) => await ShowAddPastStreakMenuAsync(activity);
        headerStack.Children.Add(btnAddPast);

        // Auto-increment checkbox
        var autoIncrementRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(40, 4, 0, 0)
        };

        var chkAutoIncrement = new CheckBox
        {
            IsChecked = activity.IsStreakAutoIncrement,
            Color = Color.FromArgb("#4CAF50")
        };
        chkAutoIncrement.CheckedChanged += async (s, e) =>
        {
            activity.IsStreakAutoIncrement = e.Value;
            await _activities.UpdateActivityAsync(activity);
        };
        autoIncrementRow.Children.Add(chkAutoIncrement);

        autoIncrementRow.Children.Add(new Label
        {
            Text = "Auto-increment daily (no click needed)",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        if (activity.IsStreakAutoIncrement)
        {
            autoIncrementRow.Children.Add(new Label
            {
                Text = "⚡",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            });
        }

        headerStack.Children.Add(autoIncrementRow);

        headerFrame.Content = headerStack;
        container.Children.Add(headerFrame);

        // Add the pre-built attempts container (visibility already set based on isExpanded)
        if (sortedAttempts.Count > 0)
        {
            container.Children.Add(attemptsContainer);
        }

        return container;
    }

    private Frame BuildAttemptCard(StreakAttempt attempt, int totalAttempts)
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

        // Make all cards tappable for options
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await ShowAttemptOptionsAsync(attempt, totalAttempts);
        frame.GestureRecognizers.Add(tapGesture);

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
            // Show "tap for options" hint
            stack.Children.Add(new Label
            {
                Text = "tap for options",
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

    private async Task ShowAttemptOptionsAsync(StreakAttempt attempt, int totalAttempts)
    {
        var options = new List<string>();

        options.Add("✏️ Edit Days");
        options.Add("📊 View History");
        
        if (attempt.IsActive)
        {
            options.Add("🛑 End Streak");
        }
        else
        {
            // For ended streaks, offer to reactivate
            options.Add("🔄 Reactivate Streak");
        }

        // Only show delete if there's more than one attempt, or always allow delete
        options.Add("🗑️ Delete Attempt");

        var result = await DisplayActionSheet(
            $"Attempt #{attempt.AttemptNumber} ({attempt.DaysAchieved} days)",
            "Cancel",
            null,
            options.ToArray());

        if (result == "✏️ Edit Days")
        {
            if (attempt.IsActive)
            {
                await EditActiveStreakAsync(attempt);
            }
            else
            {
                await EditPastStreakAsync(attempt);
            }
        }
        else if (result == "📊 View History")
        {
            await ShowStreakHistoryAsync(attempt);
        }
        else if (result == "🛑 End Streak")
        {
            await EndStreakAsync(attempt);
        }
        else if (result == "🔄 Reactivate Streak")
        {
            await ReactivateStreakAsync(attempt);
        }
        else if (result == "🗑️ Delete Attempt")
        {
            await DeleteAttemptAsync(attempt, totalAttempts);
        }
    }

    private async Task DeleteAttemptAsync(StreakAttempt attempt, int totalAttempts)
    {
        string warningMessage;
        
        if (attempt.IsActive && totalAttempts > 1)
        {
            warningMessage = $"Delete Attempt #{attempt.AttemptNumber} ({attempt.DaysAchieved} days)?\n\n" +
                           "Since this is the active streak, the previous attempt will be reactivated.\n\n" +
                           "This cannot be undone.";
        }
        else if (attempt.IsActive)
        {
            warningMessage = $"Delete Attempt #{attempt.AttemptNumber} ({attempt.DaysAchieved} days)?\n\n" +
                           "This is the only attempt. A new streak will start when you next use this activity.\n\n" +
                           "This cannot be undone.";
        }
        else
        {
            warningMessage = $"Delete Attempt #{attempt.AttemptNumber} ({attempt.DaysAchieved} days)?\n\n" +
                           "This cannot be undone.";
        }

        bool confirm = await DisplayAlert(
            "Delete Attempt",
            warningMessage,
            "Delete",
            "Cancel");

        if (!confirm) return;

        await _streaks.DeleteStreakAttemptAsync(attempt.Id);

        // Keep the activity expanded and refresh
        _expandedActivities[attempt.ActivityId] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadActivitiesAsync();
        await RestoreScrollPositionAsync();
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

    private async Task ReactivateStreakAsync(StreakAttempt attempt)
    {
        // Check if there's already an active streak for this activity
        var allAttempts = await _streaks.GetStreakAttemptsAsync(
            attempt.Username, attempt.Game, attempt.ActivityId);
        var currentActive = allAttempts.FirstOrDefault(a => a.IsActive);

        string message;
        if (currentActive != null)
        {
            message = $"Reactivate Attempt #{attempt.AttemptNumber} ({attempt.DaysAchieved} days)?\n\n" +
                     $"⚠️ The current active streak (Attempt #{currentActive.AttemptNumber}, {currentActive.DaysAchieved} days) will be ended.\n\n" +
                     "Use this if you accidentally started a new streak and want to restore the previous one.";
        }
        else
        {
            message = $"Reactivate Attempt #{attempt.AttemptNumber} ({attempt.DaysAchieved} days)?\n\n" +
                     "This streak will become active again and continue counting from where it left off.";
        }

        bool confirm = await DisplayAlert(
            "Reactivate Streak",
            message,
            "Reactivate",
            "Cancel");

        if (!confirm) return;

        // End the current active streak if there is one
        if (currentActive != null)
        {
            await _streaks.EndStreakAsync(currentActive.Id);
        }

        // Reactivate the selected streak
        await _streaks.ReactivateStreakAsync(attempt.Id);

        _expandedActivities[attempt.ActivityId] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadActivitiesAsync();
        await RestoreScrollPositionAsync();

        await DisplayAlert(
            "Streak Reactivated",
            $"Attempt #{attempt.AttemptNumber} is now active with {attempt.DaysAchieved} days.",
            "OK");
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
        // Give the UI time to rebuild and layout
        await Task.Delay(100);
        
        // Restore scroll position
        await _mainScroll.ScrollToAsync(0, _savedScrollY, false);
        
        // Double-check after another brief delay (layout can shift)
        await Task.Delay(50);
        if (Math.Abs(_mainScroll.ScrollY - _savedScrollY) > 10)
        {
            await _mainScroll.ScrollToAsync(0, _savedScrollY, false);
        }
    }
}
