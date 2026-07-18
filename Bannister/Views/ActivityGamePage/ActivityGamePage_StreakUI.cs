using Bannister.Models;
using Bannister.Services;
using Bannister.ViewModels;

namespace Bannister.Views;

/// <summary>
/// Partial class for streak container UI - displays streak attempts as cards with big day counters.
/// Uses shared logic from ActivityGamePage_SharedLogic.cs for completion and context menu handling.
/// </summary>
public partial class ActivityGamePage
{
    private string GetActivityGameId(Activity activity)
    {
        return _isGroupingMode ? activity.Game : _game!.GameId;
    }

    /// <summary>
    /// Build the streak container header with activity name and Start New Attempt button.
    /// </summary>
    private Frame BuildStreakContainerHeader(Activity streakActivity, List<StreakAttempt> attempts)
    {
        var headerFrame = new Frame
        {
            Padding = new Thickness(12, 8),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            BorderColor = Color.FromArgb("#FF9800"),
            HasShadow = false,
            Margin = new Thickness(0, 8, 0, 4)
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        var iconLabel = new Label
        {
            Text = "🔥",
            FontSize = 24,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconLabel, 0);
        headerGrid.Children.Add(iconLabel);

        var infoStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 2
        };
        
        var titleLabel = new Label
        {
            Text = streakActivity.Name.ToUpper(),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100")
        };
        infoStack.Children.Add(titleLabel);
        
        var activeAttempt = attempts.FirstOrDefault(a => a.IsActive);
        string attemptInfo = activeAttempt != null 
            ? $"Attempt #{activeAttempt.AttemptNumber} active • {attempts.Count} total"
            : $"{attempts.Count} total attempts";
        
        var infoLabel = new Label
        {
            Text = attemptInfo,
            FontSize = 11,
            TextColor = Color.FromArgb("#BF360C")
        };
        infoStack.Children.Add(infoLabel);
        
        Grid.SetColumn(infoStack, 1);
        headerGrid.Children.Add(infoStack);

        // Settings/Convert button
        var settingsButton = new Button
        {
            Text = "⚙️",
            BackgroundColor = Color.FromArgb("#FFE0B2"),
            TextColor = Color.FromArgb("#E65100"),
            FontSize = 16,
            CornerRadius = 8,
            WidthRequest = 40,
            HeightRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        settingsButton.Clicked += async (s, e) => await ShowStreakContainerMenu(streakActivity);
        Grid.SetColumn(settingsButton, 2);
        headerGrid.Children.Add(settingsButton);

        var startButton = new Button
        {
            Text = activeAttempt != null ? "🔄 Restart" : "▶️ Start Attempt",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 8,
            HeightRequest = 36,
            Padding = new Thickness(12, 0),
            VerticalOptions = LayoutOptions.Center
        };
        startButton.Clicked += async (s, e) => await OnStartNewStreakAttemptClicked(streakActivity);
        Grid.SetColumn(startButton, 3);
        headerGrid.Children.Add(startButton);

        headerFrame.Content = headerGrid;
        return headerFrame;
    }

    /// <summary>
    /// Show menu for streak container settings.
    /// </summary>
    private async Task ShowStreakContainerMenu(Activity streakActivity)
    {
        string action = await DisplayActionSheet(
            streakActivity.Name,
            "Cancel",
            null,
            "🔄 Convert to Normal Activity",
            "📊 View Streak History",
            "✏️ Edit Activity");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action.Contains("Convert to Normal"))
        {
            await ConvertToNormalActivity(streakActivity);
        }
        else if (action.Contains("View Streak History"))
        {
            await Shell.Current.GoToAsync($"streakHistory?activityId={streakActivity.Id}");
        }
        else if (action.Contains("Edit Activity"))
        {
            var editPage = new EditActivityPage(_auth, _activities, GetActivityGameId(streakActivity), streakActivity);
            await Navigation.PushModalAsync(editPage);
            await RefreshActivitiesAsync();
        }
    }

    /// <summary>
    /// Convert a streak container back to a normal activity.
    /// </summary>
    private async Task ConvertToNormalActivity(Activity streakActivity)
    {
        string streakGameId = GetActivityGameId(streakActivity);
        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, streakGameId);
        var categories = allActivities
            .Where(a => !a.IsStreakContainer && a.Id != streakActivity.Id)
            .Select(a => a.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (!string.IsNullOrEmpty(streakActivity.OriginalCategory) && 
            !categories.Contains(streakActivity.OriginalCategory))
        {
            categories.Insert(0, streakActivity.OriginalCategory);
        }

        if (!categories.Contains("Misc"))
        {
            categories.Add("Misc");
        }

        string defaultOption = !string.IsNullOrEmpty(streakActivity.OriginalCategory) 
            ? $"📁 {streakActivity.OriginalCategory} (original)" 
            : null;

        var options = new List<string>();
        if (defaultOption != null)
        {
            options.Add(defaultOption);
        }
        options.AddRange(categories.Where(c => c != streakActivity.OriginalCategory).Select(c => $"📁 {c}"));
        options.Add("✏️ Enter new category...");

        string choice = await DisplayActionSheet(
            "Move to which category?",
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        string targetCategory;

        if (choice.Contains("Enter new category"))
        {
            string newCategory = await DisplayPromptAsync(
                "New Category",
                "Enter category name:",
                "OK",
                "Cancel",
                placeholder: "Category name");

            if (string.IsNullOrWhiteSpace(newCategory)) return;
            targetCategory = newCategory.Trim();
        }
        else
        {
            targetCategory = choice
                .Replace("📁 ", "")
                .Replace(" (original)", "")
                .Trim();
        }

        var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, streakGameId, streakActivity.Id);
        bool confirm = await DisplayAlert(
            "Convert to Normal Activity?",
            $"This will:\n" +
            $"• Move '{streakActivity.Name}' to category '{targetCategory}'\n" +
            $"• Stop tracking as streak attempts\n" +
            $"• Keep existing {attempts.Count} attempt(s) in history\n\n" +
            $"Continue?",
            "Convert",
            "Cancel");

        if (!confirm) return;

        streakActivity.Category = targetCategory;
        streakActivity.IsStreakContainer = false;
        streakActivity.IsStreakTracked = false;

        await _activities.UpdateActivityAsync(streakActivity);

        await DisplayAlert("Converted!", 
            $"'{streakActivity.Name}' is now a normal activity in '{targetCategory}'.", 
            "OK");

        await LoadCategoriesAsync();
        
        if (_categories.Contains(targetCategory))
        {
            _currentCategoryIndex = _categories.IndexOf(targetCategory);
        }
        
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Build a card for a streak attempt with big day counter.
    /// Includes 3-dot menu button that uses ShowUnifiedContextMenu (shared with normal activities).
    /// </summary>
    private Frame BuildStreakAttemptCard(StreakAttemptViewModel attemptVM)
    {
        var outerFrame = new Frame
        {
            WidthRequest = 180,
            HeightRequest = 200,
            Padding = 0,
            CornerRadius = 12,
            HasShadow = true,
            BackgroundColor = Colors.White,
            BorderColor = attemptVM.IsActive ? Color.FromArgb("#FF9800") : Color.FromArgb("#E0E0E0")
        };

        var grid = new Grid();

        var backgroundBox = new BoxView
        {
            Color = attemptVM.IsActive 
                ? Color.FromArgb("#FFF8E1")
                : Color.FromArgb("#F5F5F5")
        };
        grid.Children.Add(backgroundBox);

        var contentStack = new VerticalStackLayout
        {
            Padding = new Thickness(10, 12, 10, 8),
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        var attemptLabel = new Label
        {
            Text = attemptVM.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = attemptVM.IsActive ? Color.FromArgb("#E65100") : Color.FromArgb("#757575")
        };
        contentStack.Children.Add(attemptLabel);

        const double defaultDaysFontSize = 34;
        string daysText = attemptVM.DaysWithTargetDisplay;
        var daysLabel = new Label
        {
            Text = daysText,
            FontSize = GetStreakAttemptDaysFontSize(daysText, defaultDaysFontSize),
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            MaxLines = 1,
            TextColor = attemptVM.IsActive ? Color.FromArgb("#FF6D00") : Color.FromArgb("#9E9E9E")
        };
        contentStack.Children.Add(daysLabel);

        var daysTextLabel = new Label
        {
            Text = "days in a row",
            FontSize = 16,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = attemptVM.IsActive ? Color.FromArgb("#FF9800") : Color.FromArgb("#BDBDBD"),
            Margin = new Thickness(0, -2, 0, 0)
        };
        contentStack.Children.Add(daysTextLabel);

        var statusLabel = new Label
        {
            Text = attemptVM.Status,
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = attemptVM.IsActive ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336")
        };
        contentStack.Children.Add(statusLabel);

        if (!string.IsNullOrEmpty(attemptVM.DateRange))
        {
            var dateLabel = new Label
            {
                Text = attemptVM.DateRange,
                FontSize = 10,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#9E9E9E")
            };
            contentStack.Children.Add(dateLabel);
        }

        grid.Children.Add(contentStack);

        // EXP badge
        var expFrame = new Frame
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#4CAF50"),
            BorderColor = Colors.Transparent,
            Margin = new Thickness(4)
        };
        var expLabel = new Label
        {
            Text = attemptVM.ExpGainDisplay,
            TextColor = Colors.White,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold
        };
        expFrame.Content = expLabel;
        grid.Children.Add(expFrame);

        if (attemptVM.ShowMultiplier)
        {
            var multFrame = new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.End,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#FF5722"),
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 4, 40, 4)
            };
            var multLabel = new Label
            {
                Text = $"×{attemptVM.Multiplier}",
                TextColor = Colors.White,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            };
            multFrame.Content = multLabel;
            grid.Children.Add(multFrame);
        }

        // 3-DOT MENU BUTTON - uses ShowUnifiedContextMenu (shared logic)
        var menuButton = new Button
        {
            Text = "⋮",
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            BackgroundColor = Color.FromArgb("#80000000"),
            TextColor = Colors.White,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 30,
            HeightRequest = 30,
            CornerRadius = 15,
            Padding = 0,
            Margin = new Thickness(4)
        };
        menuButton.Clicked += async (s, e) => 
            await ShowUnifiedContextMenu(attemptVM.GetActivity(), isStreakAttempt: true, attemptVM: attemptVM);
        grid.Children.Add(menuButton);

        // Single tap on card for active attempts records usage
        if (attemptVM.IsActive)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) => await OnStreakAttemptCardClicked(attemptVM);
            outerFrame.GestureRecognizers.Add(tapGesture);
        }
        else
        {
            // Inactive cards - tap shows context menu (same as original behavior)
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) => 
                await ShowUnifiedContextMenu(attemptVM.GetActivity(), isStreakAttempt: true, attemptVM: attemptVM);
            outerFrame.GestureRecognizers.Add(tapGesture);
        }

        outerFrame.Content = grid;
        return outerFrame;
    }

    private static double GetStreakAttemptDaysFontSize(string text, double defaultFontSize)
    {
        int compactLength = text.Count(c => !char.IsWhiteSpace(c));
        return compactLength switch
        {
            <= 7 => defaultFontSize,
            <= 9 => defaultFontSize * 0.75,
            _ => defaultFontSize * 0.6
        };
    }

    /// <summary>
    /// Handle click on active streak attempt card - records today and awards EXP.
    /// Uses ProcessActivityCompletionAsync for shared completion logic.
    /// </summary>
    private async Task OnStreakAttemptCardClicked(StreakAttemptViewModel attemptVM)
    {
        if (!attemptVM.IsActive) return;

        var attempt = attemptVM.GetAttempt();
        var activity = attemptVM.GetActivity();

        var today = DateTime.UtcNow.Date;
        if (attempt.LastUsedDate.HasValue && attempt.LastUsedDate.Value.Date == today)
        {
            await DisplayAlert("Already Recorded",
                $"You've already recorded today.\n\nCurrent streak: {attemptVM.DisplayDaysAchieved} days",
                "OK");
            return;
        }

        // Check for gap before proceeding
        string gameId = GetActivityGameId(activity);
        int gapDays = await _streaks.GetActiveStreakGapDaysAsync(_auth.CurrentUsername, gameId, activity.Id);
        bool streakBroke = false;

        if (gapDays > 1 && activity.ShowStreakAsDaysSinceStarted != true)
        {
            // Gap detected — ask user if streak broke
            streakBroke = await DisplayAlert(
                "Streak Gap Detected",
                $"It's been {gapDays} days since your last use of '{activity.Name}'.\n\n" +
                $"Current streak: {attempt.DaysAchieved} days.\n\n" +
                "Did the streak break?",
                "Yes, It Broke",
                "No, Keep Going");

            if (streakBroke)
            {
                // End the current streak — ProcessActivityCompletionAsync will create a new one
                await _streaks.EndStreakAsync(attempt.Id);
            }
        }

        // Calculate EXP — always single application amount
        int expAmount = attemptVM.ExpGain * activity.Multiplier;

        // Compute day label for EXP log
        string dayLabel;
        if (streakBroke)
        {
            dayLabel = $"{activity.Name} (Day 1)";
        }
        else if (activity.ShowStreakAsDaysSinceStarted == true && attempt.StartedAt.HasValue)
        {
            int calendarDays = (today - attempt.StartedAt.Value.ToLocalTime().Date).Days;
            dayLabel = $"{activity.Name} (Day {calendarDays})";
        }
        else
        {
            dayLabel = $"{activity.Name} (Day {attempt.DaysAchieved + 1})";
        }

        var (totalExp, bonusDetails) = await ProcessActivityCompletionAsync(
            activity,
            expAmount,
            dayLabel);

        await RefreshExpAsync();
        await RefreshActivitiesAsync();
        await LoadChartDataAsync();

        var updatedAttempt = (await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, gameId, activity.Id))
            .FirstOrDefault(a => a.IsActive);

        string bonusMessage = !string.IsNullOrEmpty(bonusDetails) ? $"\n{bonusDetails}" : "";

        if (updatedAttempt != null)
        {
            int displayDays = GetAttemptDisplayDays(activity, updatedAttempt);
            string streakNote = streakBroke ? " (new attempt)" : "";
            await DisplayAlert($"Day Recorded! {streakNote}",
                $"+{totalExp} EXP{bonusMessage}\n\n" +
                $"Streak: {displayDays} / {GetStreakTargetDays(activity)} days\n" +
                $"Display Day Streak: {activity.DisplayDayStreak} days",
                "Nice!");

            await PromptForNextStreakTargetIfNeededAsync(activity, updatedAttempt);
        }
    }

    private int GetStreakTargetDays(Activity activity)
    {
        return activity.StreakTargetDays > 0 ? activity.StreakTargetDays : 365;
    }

    private int GetAttemptDisplayDays(Activity activity, StreakAttempt attempt)
    {
        if (activity.ShowStreakAsDaysSinceStarted && attempt.StartedAt.HasValue)
        {
            return Math.Max(0, (DateTime.UtcNow.Date - attempt.StartedAt.Value.ToLocalTime().Date).Days);
        }
        return attempt.DaysAchieved;
    }

    private async Task PromptForNextStreakTargetIfNeededAsync(Activity activity, StreakAttempt attempt)
    {
        int targetDays = GetStreakTargetDays(activity);
        if (attempt.DaysAchieved < targetDays)
        {
            return;
        }

        if (await _streaks.HasTargetCompletionAsync(attempt.Id, targetDays))
        {
            return;
        }

        string? result = await DisplayPromptAsync(
            "Streak Target Reached",
            $"You reached {targetDays} days in a row.\n\nSet the next target:",
            "Save",
            "Cancel",
            initialValue: Math.Max(targetDays + 1, targetDays * 2).ToString(),
            keyboard: Keyboard.Numeric);

        await _streaks.LogTargetCompletionAsync(_auth.CurrentUsername, GetActivityGameId(activity), activity, attempt, targetDays);

        if (string.IsNullOrWhiteSpace(result))
        {
            return;
        }

        if (!int.TryParse(result, out int newTargetDays) || newTargetDays <= targetDays)
        {
            await DisplayAlert("Invalid Target", $"Enter a number greater than {targetDays}.", "OK");
            return;
        }

        activity.StreakTargetDays = newTargetDays;
        await _activities.UpdateActivityAsync(activity);
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Handle click on Start New Attempt button.
    /// </summary>
    private async Task OnStartNewStreakAttemptClicked(Activity streakActivity)
    {
        string streakGameId = GetActivityGameId(streakActivity);
        var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, streakGameId, streakActivity.Id);
        var activeAttempt = attempts.FirstOrDefault(a => a.IsActive);

        if (activeAttempt != null)
        {
            bool confirm = await DisplayAlert(
                "Restart Streak?",
                $"You have an active streak at {activeAttempt.DaysAchieved} days.\n\nContinue?",
                "Yes, Restart",
                "Cancel");

            if (!confirm) return;
        }

        await _streaks.StartNewStreakAsync(
            _auth.CurrentUsername, 
            streakGameId, 
            streakActivity.Id, 
            streakActivity.Name);

        await RefreshActivitiesAsync();

        await DisplayAlert("New Attempt Started! 🔥", 
            "Click the attempt card each day to record your progress.",
            "Let's Go!");
    }

    /// <summary>
    /// Build the full view for a streak container category.
    /// </summary>
    private async Task BuildStreakContainerViewAsync(VerticalStackLayout mainStack, Activity streakContainer)
    {
        var attempts = await _streaks.GetStreakAttemptsAsync(
            _auth.CurrentUsername, 
            GetActivityGameId(streakContainer), 
            streakContainer.Id);
        
        var header = BuildStreakContainerHeader(streakContainer, attempts);
        mainStack.Children.Add(header);

        var goalsHeader = await BuildStreakGoalsHeaderAsync(streakContainer, attempts);
        mainStack.Children.Add(goalsHeader);
        
        if (attempts.Count == 0)
        {
            var noAttemptsFrame = new Frame
            {
                Padding = 20,
                CornerRadius = 12,
                BackgroundColor = Color.FromArgb("#FFF8E1"),
                BorderColor = Color.FromArgb("#FFE082"),
                HasShadow = false,
                Margin = new Thickness(0, 16)
            };
            
            var noAttemptsStack = new VerticalStackLayout
            {
                Spacing = 12,
                HorizontalOptions = LayoutOptions.Center
            };
            
            noAttemptsStack.Children.Add(new Label
            {
                Text = "🔥",
                FontSize = 48,
                HorizontalOptions = LayoutOptions.Center
            });
            
            noAttemptsStack.Children.Add(new Label
            {
                Text = "No streak attempts yet",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#E65100")
            });
            
            noAttemptsStack.Children.Add(new Label
            {
                Text = "Click \"Start Attempt\" to begin tracking.",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#BF360C")
            });
            
            noAttemptsFrame.Content = noAttemptsStack;
            mainStack.Children.Add(noAttemptsFrame);
            return;
        }

        var attemptVMs = attempts
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.AttemptNumber)
            .Select(a => new StreakAttemptViewModel(a, streakContainer) { CurrentLevel = _currentLevel })
            .ToList();
        
        Grid? currentRow = null;
        int columnIndex = 0;
        
        foreach (var attemptVM in attemptVMs)
        {
            if (currentRow == null || columnIndex >= 2)
            {
                currentRow = new Grid
                {
                    ColumnSpacing = 12,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    Margin = new Thickness(0, 8, 0, 8)
                };
                mainStack.Children.Add(currentRow);
                columnIndex = 0;
            }
            
            var card = BuildStreakAttemptCard(attemptVM);
            Grid.SetColumn(card, columnIndex);
            currentRow.Children.Add(card);
            
            columnIndex++;
        }
    }

    private async Task<View> BuildStreakGoalsHeaderAsync(Activity streakContainer, List<StreakAttempt> attempts)
    {
        int currentStreakDays = GetCurrentStreakGoalDays(streakContainer, attempts);

        List<StreakGoal> goals;
        if (_db.IsReadOnly)
        {
            goals = await _streakGoals.GetGoalsForActivityAsync(streakContainer.Id);
        }
        else
        {
            goals = await _streakGoals.MarkAchievedIfReachedAsync(streakContainer.Id, currentStreakDays);
        }

        var stack = new VerticalStackLayout
        {
            Spacing = 10
        };

        if (goals.Count == 0)
        {
            stack.Children.Add(new Label
            {
                Text = "No streak goals yet.",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5D4037")
            });

            var setFirstButton = new Button
            {
                Text = "Set First Goal",
                BackgroundColor = Color.FromArgb("#FFB300"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 42
            };
            setFirstButton.Clicked += async (_, _) => await PromptAddStreakGoalAsync(streakContainer, null);
            stack.Children.Add(setFirstButton);

            return WrapStreakGoalsFrame(stack);
        }

        var firstGoal = goals.First();
        var latestGoal = goals.Last();
        int daysSinceStart = Math.Max(0, (DateTime.Today - firstGoal.SetDate.ToLocalTime().Date).Days);

        stack.Children.Add(new Label
        {
            Text = $"Started: {firstGoal.SetDate.ToLocalTime():MMM d, yyyy}",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3E2723")
        });
        stack.Children.Add(new Label
        {
            Text = $"Days since: {daysSinceStart}",
            FontSize = 13,
            TextColor = Color.FromArgb("#795548")
        });

        for (int i = 0; i < goals.Count; i++)
        {
            stack.Children.Add(BuildStreakGoalCard(i + 1, goals[i], currentStreakDays));
        }

        if (latestGoal.AchievedDate.HasValue)
        {
            var addGoalButton = new Button
            {
                Text = "+ Add New Goal",
                BackgroundColor = Color.FromArgb("#6D4C41"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 42
            };
            addGoalButton.Clicked += async (_, _) => await PromptAddStreakGoalAsync(streakContainer, latestGoal);
            stack.Children.Add(addGoalButton);
        }

        return WrapStreakGoalsFrame(stack);
    }

    private static int GetCurrentStreakGoalDays(Activity streakContainer, List<StreakAttempt> attempts)
    {
        var activeAttempt = attempts
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.AttemptNumber)
            .FirstOrDefault();

        return activeAttempt?.DaysAchieved ?? streakContainer.DisplayDayStreak;
    }

    private View BuildStreakGoalCard(int goalNumber, StreakGoal goal, int currentStreakDays)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 4
        };

        stack.Children.Add(new Label
        {
            Text = $"Goal {goalNumber}: {goal.TargetDays} days",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222")
        });
        stack.Children.Add(new Label
        {
            Text = $"Set: {goal.SetDate.ToLocalTime():MMM d, yyyy}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666666")
        });

        if (goal.AchievedDate.HasValue)
        {
            int daysTaken = Math.Max(0, (goal.AchievedDate.Value.Date - goal.SetDate.ToLocalTime().Date).Days);
            stack.Children.Add(new Label
            {
                Text = $"Achieved: {goal.AchievedDate.Value:MMM d, yyyy} (took {daysTaken} days)",
                FontSize = 12,
                TextColor = Color.FromArgb("#2E7D32")
            });
        }
        else
        {
            int daysToGo = goal.TargetDays - currentStreakDays;
            stack.Children.Add(new Label
            {
                Text = daysToGo <= 0 ? "Goal reached!" : $"In progress - {daysToGo} days to go",
                FontSize = 12,
                TextColor = Color.FromArgb("#795548")
            });
        }

        return new Frame
        {
            Padding = 10,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#FFE082"),
            BackgroundColor = Color.FromArgb("#FFFDF5"),
            Content = stack
        };
    }

    private static Frame WrapStreakGoalsFrame(View content)
    {
        return new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            HasShadow = false,
            BorderColor = Color.FromArgb("#FFCC80"),
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            Margin = new Thickness(0, 12, 0, 8),
            Content = content
        };
    }

    private async Task PromptAddStreakGoalAsync(Activity streakContainer, StreakGoal? previousGoal)
    {
        if (_db.IsReadOnly)
        {
            await DisplayAlert("Read Only", "Streak goals can only be changed on the master device.", "OK");
            return;
        }

        string? result = await DisplayPromptAsync(
            previousGoal == null ? "Set First Goal" : "Add New Goal",
            previousGoal == null
                ? "Enter target streak days."
                : $"Enter target streak days greater than {previousGoal.TargetDays}.",
            "Save",
            "Cancel",
            keyboard: Keyboard.Numeric);

        if (result == null)
        {
            return;
        }

        if (!int.TryParse(result.Trim(), out int targetDays) || targetDays <= 0)
        {
            await DisplayAlert("Invalid Goal", "Enter a positive number of days.", "OK");
            return;
        }

        if (previousGoal != null && targetDays <= previousGoal.TargetDays)
        {
            await DisplayAlert("Invalid Goal", $"Enter a number greater than {previousGoal.TargetDays}.", "OK");
            return;
        }

        await _streakGoals.AddGoalAsync(streakContainer.Id, targetDays);
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Check if a category is a streak container.
    /// </summary>
    private bool IsStreakContainerCategory(string category)
    {
        return _allActivities.Any(vm => 
            vm.Activity.IsStreakContainer && 
            vm.Activity.Name.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the streak container activity for a category.
    /// </summary>
    private Activity? GetStreakContainerForCategory(string category)
    {
        return _allActivities
            .FirstOrDefault(vm => 
                vm.Activity.IsStreakContainer && 
                vm.Activity.Name.Equals(category, StringComparison.OrdinalIgnoreCase))
            ?.Activity;
    }

    #region Streak Attempt-Specific Edit Methods

    /// <summary>
    /// Edit the days count for an attempt.
    /// </summary>
    private async Task EditAttemptDays(StreakAttemptViewModel attemptVM)
    {
        var attempt = attemptVM.GetAttempt();
        
        string result = await DisplayPromptAsync(
            "Edit Days Count",
            $"Current: {attempt.DaysAchieved} days\n\nEnter new day count:",
            "Save",
            "Cancel",
            initialValue: attempt.DaysAchieved.ToString(),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrEmpty(result)) return;

        if (!int.TryParse(result, out int newDays) || newDays < 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid number (0 or greater).", "OK");
            return;
        }

        attempt.DaysAchieved = newDays;
        
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(attempt);

        await DisplayAlert("Updated", $"Attempt {attempt.AttemptNumber} now shows {newDays} days.", "OK");
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Edit the date range for an attempt.
    /// </summary>
    private async Task EditAttemptDates(StreakAttemptViewModel attemptVM)
    {
        var attempt = attemptVM.GetAttempt();
        
        string currentStart = attempt.StartedAt?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
        
        string result = await DisplayPromptAsync(
            "Edit Start Date",
            $"Current start: {currentStart}\n\nEnter new start date (YYYY-MM-DD):",
            "Save",
            "Cancel",
            initialValue: currentStart);

        if (string.IsNullOrEmpty(result)) return;

        if (!DateTime.TryParse(result, out DateTime newStartDate))
        {
            await DisplayAlert("Invalid", "Please enter a valid date (YYYY-MM-DD).", "OK");
            return;
        }

        attempt.StartedAt = newStartDate;
        
        if (!attempt.IsActive)
        {
            string currentEnd = attempt.EndedAt?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
            
            string endResult = await DisplayPromptAsync(
                "Edit End Date",
                $"Current end: {currentEnd}\n\nEnter new end date (YYYY-MM-DD):",
                "Save",
                "Cancel",
                initialValue: currentEnd);

            if (!string.IsNullOrEmpty(endResult) && DateTime.TryParse(endResult, out DateTime newEndDate))
            {
                attempt.EndedAt = newEndDate;
            }
        }

        // For days-since-started activities, recalculate DaysAchieved from new StartedAt
        var activity = attemptVM.GetActivity();
        if (activity.ShowStreakAsDaysSinceStarted && attempt.StartedAt.HasValue)
        {
            int daysBefore = attempt.DaysAchieved;
            attempt.DaysAchieved = Math.Max(0, (DateTime.UtcNow.Date - attempt.StartedAt.Value.ToLocalTime().Date).Days);
            System.Diagnostics.Debug.WriteLine($"[STREAK] Days-since recalculated: {daysBefore} -> {attempt.DaysAchieved} (StartedAt={attempt.StartedAt.Value:yyyy-MM-dd})");
        }

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(attempt);

        int displayDays = GetAttemptDisplayDays(activity, attempt);
        await DisplayAlert("Updated", $"Dates updated.\nCurrent value: {displayDays} days.", "OK");
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Delete an attempt completely.
    /// </summary>
    private async Task DeleteAttempt(StreakAttemptViewModel attemptVM)
    {
        var attempt = attemptVM.GetAttempt();
        var activity = attemptVM.GetActivity();

        bool confirm = await DisplayAlert(
            "Delete Attempt?",
            $"Delete Attempt {attempt.AttemptNumber}?\n\n" +
            $"Days: {attempt.DaysAchieved}\n" +
            $"Status: {attempt.Status}\n" +
            $"Date: {attemptVM.DateRange}\n\n" +
            $"This cannot be undone!",
            "Delete",
            "Cancel");

        if (!confirm) return;

        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(attempt);

        var remainingAttempts = await _streaks.GetStreakAttemptsAsync(
            _auth.CurrentUsername, 
            GetActivityGameId(activity), 
            activity.Id);
        
        int newNumber = 1;
        foreach (var remaining in remainingAttempts.OrderBy(a => a.StartedAt))
        {
            if (remaining.AttemptNumber != newNumber)
            {
                remaining.AttemptNumber = newNumber;
                await conn.UpdateAsync(remaining);
            }
            newNumber++;
        }

        await DisplayAlert("Deleted", "Attempt deleted.", "OK");
        await RefreshActivitiesAsync();
    }

    private async Task<bool> HasPreviousEndedAttemptAsync(StreakAttemptViewModel attemptVM)
    {
        var attempt = attemptVM.GetAttempt();
        if (!attempt.IsActive)
            return false;

        var activity = attemptVM.GetActivity();
        var previousAttempt = await GetPreviousEndedAttemptAsync(activity, attempt);
        return previousAttempt != null;
    }

    private async Task<StreakAttempt?> GetPreviousEndedAttemptAsync(Activity activity, StreakAttempt currentAttempt)
    {
        var allAttempts = await _streaks.GetStreakAttemptsAsync(
            _auth.CurrentUsername,
            GetActivityGameId(activity),
            activity.Id);

        return allAttempts
            .Where(a => !a.IsActive
                && a.EndedAt.HasValue
                && a.AttemptNumber < currentAttempt.AttemptNumber)
            .OrderByDescending(a => a.AttemptNumber)
            .FirstOrDefault();
    }

    private async Task DeleteAttemptAndRestartPrevious(StreakAttemptViewModel attemptVM)
    {
        if (_db.IsReadOnly)
        {
            await DisplayAlert("Read Only", "This device cannot modify streak attempts.", "OK");
            return;
        }

        var attempt = attemptVM.GetAttempt();
        var activity = attemptVM.GetActivity();
        var previousAttempt = await GetPreviousEndedAttemptAsync(activity, attempt);

        if (previousAttempt == null)
        {
            await DisplayAlert("No Previous Attempt", "There is no ended previous attempt to restart.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Delete and Restart?",
            "This will delete the current attempt and reactivate the previous attempt as your active streak. Continue?",
            "Delete and Restart",
            "Cancel");

        if (!confirm)
            return;

        await _streaks.DeleteStreakAttemptAsync(attempt.Id);
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Reactivate an ended streak attempt.
    /// </summary>
    private async Task ReactivateAttempt(StreakAttemptViewModel attemptVM)
    {
        var attempt = attemptVM.GetAttempt();
        var activity = attemptVM.GetActivity();

        // Check if there's already an active streak for this activity
        var allAttempts = await _streaks.GetStreakAttemptsAsync(
            _auth.CurrentUsername, GetActivityGameId(activity), activity.Id);
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

        await RefreshActivitiesAsync();

        await DisplayAlert(
            "Streak Reactivated",
            $"Attempt #{attempt.AttemptNumber} is now active with {attempt.DaysAchieved} days.",
            "OK");
    }

    #endregion
}
