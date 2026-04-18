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
            var editPage = new EditActivityPage(_auth, _activities, _game!.GameId, streakActivity);
            await Navigation.PushModalAsync(editPage);
            await RefreshActivitiesAsync();
        }
    }

    /// <summary>
    /// Convert a streak container back to a normal activity.
    /// </summary>
    private async Task ConvertToNormalActivity(Activity streakActivity)
    {
        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game!.GameId);
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

        var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, _game.GameId, streakActivity.Id);
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
            Padding = 12,
            Spacing = 4,
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

        var daysLabel = new Label
        {
            Text = attemptVM.DaysDisplay,
            FontSize = 72,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = attemptVM.IsActive ? Color.FromArgb("#FF6D00") : Color.FromArgb("#9E9E9E")
        };
        contentStack.Children.Add(daysLabel);

        var daysTextLabel = new Label
        {
            Text = attemptVM.DaysAchieved == 1 ? "day" : "days",
            FontSize = 16,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = attemptVM.IsActive ? Color.FromArgb("#FF9800") : Color.FromArgb("#BDBDBD"),
            Margin = new Thickness(0, -8, 0, 0)
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
                $"You've already recorded today.\n\nCurrent streak: {attempt.DaysAchieved} days",
                "OK");
            return;
        }

        // Calculate EXP
        int expAmount = attemptVM.ExpGain * activity.Multiplier;
        
        // Use SHARED completion logic - this handles all the common stuff:
        // - Apply EXP
        // - Record streak usage (increments DaysAchieved via _streaks.RecordActivityUsageAsync)
        // - Record habit completion
        // - Record display day streak  
        // - Increment times completed
        // - Update NewHabit progress
        // - Check for streak milestone bonus
        var (totalExp, bonusDetails) = await ProcessActivityCompletionAsync(
            activity, 
            expAmount, 
            $"{activity.Name} (Day {attempt.DaysAchieved + 1})");

        await RefreshExpAsync();
        await RefreshActivitiesAsync();
        await LoadChartDataAsync();

        var updatedAttempt = (await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, _game.GameId, activity.Id))
            .FirstOrDefault(a => a.IsActive);
        
        string bonusMessage = !string.IsNullOrEmpty(bonusDetails) ? $"\n{bonusDetails}" : "";
        
        if (updatedAttempt != null)
        {
            await DisplayAlert("Day Recorded! 🔥", 
                $"+{totalExp} EXP{bonusMessage}\n\n" +
                $"Streak: {updatedAttempt.DaysAchieved} days\n" +
                $"Display Day Streak: {activity.DisplayDayStreak} days",
                "Nice!");
        }
    }

    /// <summary>
    /// Handle click on Start New Attempt button.
    /// </summary>
    private async Task OnStartNewStreakAttemptClicked(Activity streakActivity)
    {
        var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, _game!.GameId, streakActivity.Id);
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
            _game.GameId, 
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
            _game!.GameId, 
            streakContainer.Id);
        
        var header = BuildStreakContainerHeader(streakContainer, attempts);
        mainStack.Children.Add(header);
        
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

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(attempt);

        await DisplayAlert("Updated", "Dates updated.", "OK");
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
            _game!.GameId, 
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

    /// <summary>
    /// Reactivate an ended streak attempt.
    /// </summary>
    private async Task ReactivateAttempt(StreakAttemptViewModel attemptVM)
    {
        var attempt = attemptVM.GetAttempt();
        var activity = attemptVM.GetActivity();

        // Check if there's already an active streak for this activity
        var allAttempts = await _streaks.GetStreakAttemptsAsync(
            _auth.CurrentUsername, _game!.GameId, activity.Id);
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
