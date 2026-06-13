using Bannister.Models;
using Bannister.Services;
using Bannister.ViewModels;

namespace Bannister.Views;

/// <summary>
/// Partial class containing shared activity completion logic.
/// Both normal activities (via Calculate EXP) and streak cards (via direct click) use these methods.
/// 
/// UPDATE THIS FILE when adding new functionality that should apply to all activity completions.
/// </summary>
public partial class ActivityGamePage
{/// <summary>
 /// Full activity completion including EXP application.
 /// Used by streak cards which do everything in a single click.
 /// 
 /// This is the SINGLE SOURCE OF TRUTH for activity completion logic.
 /// </summary>
 /// <param name="activity">The activity being completed</param>
 /// <param name="expAmount">Base EXP amount (already multiplied if needed)</param>
 /// <param name="logDescription">Description for the EXP log entry</param>
 /// <returns>Total EXP awarded including any bonuses, and bonus details string</returns>
    private async Task<(int totalExp, string bonusDetails)> ProcessActivityCompletionAsync(
        Activity activity,
        int expAmount,
        string logDescription)
    {
        string activityGameId = GetActivityGameId(activity);

        // 1. Apply base EXP
        await _exp.ApplyExpAsync(
            _auth.CurrentUsername,
            activityGameId,
            logDescription,
            expAmount,
            activity.Id);

        // 2. Process all the other side effects
        var (bonusExp, bonusDetails) = await ProcessActivityCompletionCoreAsync(activity);

        return (expAmount + bonusExp, bonusDetails);
    }

    /// <summary>
    /// Core completion logic WITHOUT applying base EXP.
    /// Used by Calculate EXP which handles EXP application separately (for multiplier support).
    /// 
    /// Handles: streak tracking, habit completion, display day streak,
    /// times completed, NewHabit progress, and streak bonus.
    /// 
    /// UPDATE THIS METHOD when adding new completion side effects.
    /// </summary>
    /// <param name="activity">The activity being completed</param>
    /// <returns>Bonus EXP (from streak milestones) and bonus details string</returns>
    private async Task<(int bonusExp, string bonusDetails)> ProcessActivityCompletionCoreAsync(Activity activity)
    {
        int bonusExp = 0;
        var bonusDetails = new List<string>();

        // 1. Record streak usage if this activity is streak-tracked
        if (activity.IsStreakTracked)
        {
            await _streaks.RecordActivityUsageAsync(
                _auth.CurrentUsername,
                GetActivityGameId(activity),
                activity.Id,
                activity.Name,
                activity);
        }

        // 2. Record habit completion if this activity has habit tracking
        if (activity.HabitType != "None")
        {
            await _activities.RecordHabitCompletionAsync(activity);
        }

        // 3. Record display day streak
        await _activities.RecordDisplayDayStreakAsync(activity);

        // 4. Increment times completed
        activity.TimesCompleted++;
        await _activities.UpdateActivityAsync(activity);

        // 5. Update NewHabit progress if this activity is linked to a NewHabit
        await RecordNewHabitProgressAsync(activity.Id);

        // 6. Check for streak milestone bonus (only for positive EXP activities)
        if (activity.ExpGain > 0)
        {
            int streakBonus = ActivityService.CalculateStreakBonus(activity.DisplayDayStreak);
            if (streakBonus > 0)
            {
                await _exp.ApplyExpAsync(
                    _auth.CurrentUsername,
                    GetActivityGameId(activity),
                    $"{activity.Name} (Streak Bonus)",
                    streakBonus,
                    activity.Id);
                bonusExp = streakBonus;
                bonusDetails.Add($"🔥 {activity.Name} streak bonus ({activity.DisplayDayStreak} days): +{streakBonus}");
            }
        }

        return (bonusExp, string.Join("\n", bonusDetails));
    }

    /// <summary>
    /// Shows the unified context menu for any activity (normal or streak container).
    /// This is the SINGLE SOURCE OF TRUTH for context menu options.
    /// </summary>
    /// <param name="activity">The activity to show menu for</param>
    /// <param name="isStreakAttempt">True if this is being called from a streak attempt card</param>
    /// <param name="attemptVM">Optional: the streak attempt VM if applicable</param>
    private async Task ShowUnifiedContextMenu(Activity activity, bool isStreakAttempt = false, StreakAttemptViewModel? attemptVM = null)
    {
        string notesOption = !string.IsNullOrEmpty(activity.Notes)
            ? "📝 Edit Notes"
            : "📝 Add Notes";

        var options = new List<string>
        {
            "✏️ Edit Activity",
            $"Set Multiplier (current: x{activity.Multiplier})",
            "Applied X Times (one-time)",
            "Update Streak Values",
            $"Times Completed: {activity.TimesCompleted}",
            notesOption,
            "Duplicate as Negative",
            "Set Manual Priority",
            "Set Auto-Award"
        };

        // Add streak attempt-specific options
        if (isStreakAttempt && attemptVM != null)
        {
            if (attemptVM.IsActive)
            {
                options.Add("✏️ Edit Attempt Days");
                options.Add("📅 Edit Start Date");
            }
            else
            {
                options.Add("✏️ Edit Attempt Days");
                options.Add("📅 Edit Date Range");
                options.Add("🔄 Reactivate Streak");
            }
            options.Add("🗑️ Delete This Attempt");
        }

        options.Add("📦 Move to Another Game");
        options.Add("📂 Assign to Grouping");
        options.Add("⏸️ Disable Activity");
        options.Add("🗑️ Remove Activity");

        string title = isStreakAttempt && attemptVM != null
            ? $"{activity.Name} - {attemptVM.Name}"
            : activity.Name;

        string action = await DisplayActionSheet(
            title,
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        // Handle actions - find matching ActivityGameViewModel if needed for some operations
        var activityVM = _allActivities?.FirstOrDefault(vm => vm.Activity.Id == activity.Id);

        if (action == "✏️ Edit Activity")
        {
            var editPage = new EditActivityPage(_auth, _activities, _game!.GameId, activity);
            await Navigation.PushModalAsync(editPage);
            await RefreshActivitiesAsync();
        }
        else if (action.StartsWith("Set Multiplier"))
        {
            var multiplierPage = new SetMultiplierPage(activity);
            await Navigation.PushModalAsync(multiplierPage);

            var result = await multiplierPage.WaitForResultAsync();
            if (result.HasValue)
            {
                activity.Multiplier = result.Value;
                await _activities.UpdateActivityAsync(activity);
                activityVM?.UpdateActivity(activity);
                await RefreshActivitiesAsync();
            }
        }
        else if (action.StartsWith("Applied X Times"))
        {
            await HandleAppliedXTimes(activity, activityVM, isStreakAttempt, attemptVM);
        }
        else if (action == "Update Streak Values")
        {
            await HandleUpdateStreakValues(activity, activityVM, attemptVM);
        }
        else if (action.StartsWith("Times Completed"))
        {
            await HandleEditTimesCompleted(activity, activityVM);
        }
        else if (action.Contains("Notes"))
        {
            await HandleEditNotes(activity, activityVM);
        }
        else if (action == "Duplicate as Negative")
        {
            await HandleDuplicateAsNegative(activity);
        }
        else if (action == "Set Manual Priority")
        {
            await HandleSetManualPriority(activity, activityVM);
        }
        else if (action == "Set Auto-Award")
        {
            var autoAwardPage = new SetAutoAwardPage(activity);
            await Navigation.PushModalAsync(autoAwardPage);

            var result = await autoAwardPage.WaitForResultAsync();
            if (result)
            {
                await _activities.UpdateActivityAsync(activity);
                activityVM?.UpdateActivity(activity);
                await RefreshActivitiesAsync();
                await DisplayAlert("Success", $"Auto-award configured for '{activity.Name}'", "OK");
            }
        }
        else if (action == "✏️ Edit Attempt Days" && attemptVM != null)
        {
            await EditAttemptDays(attemptVM);
        }
        else if ((action == "📅 Edit Start Date" || action == "📅 Edit Date Range") && attemptVM != null)
        {
            await EditAttemptDates(attemptVM);
        }
        else if (action == "🗑️ Delete This Attempt" && attemptVM != null)
        {
            await DeleteAttempt(attemptVM);
        }
        else if (action == "🔄 Reactivate Streak" && attemptVM != null)
        {
            await ReactivateAttempt(attemptVM);
        }
        else if (action == "📦 Move to Another Game")
        {
            await HandleMoveToAnotherGame(activity);
        }
        else if (action == "📂 Assign to Grouping")
        {
            await HandleAssignToGrouping(activity);
        }
        else if (action == "⏸️ Disable Activity")
        {
            bool confirm = await DisplayAlert(
                "Disable Activity",
                $"Disable '{activity.Name}'?\n\nThis will hide the activity (set IsActive = false). You can restore it later from Manage Activities.",
                "Disable",
                "Cancel");

            if (confirm)
            {
                await _activities.BlankActivityAsync(activity.Id);
                await LoadCategoriesAsync();
                await RefreshActivitiesAsync();
            }
        }
        else if (action == "🗑️ Remove Activity")
        {
            bool confirm = await DisplayAlert(
                "Remove Activity",
                $"⚠️ Remove '{activity.Name}' completely?\n\nThis will reset ALL data for this activity. This cannot be undone!",
                "Remove",
                "Cancel");

            if (confirm)
            {
                bool doubleConfirm = await DisplayAlert(
                    "Are you sure?",
                    $"Really remove '{activity.Name}'? All data will be lost.",
                    "Yes, Remove It",
                    "No, Keep It");

                if (doubleConfirm)
                {
                    await _activities.ResetActivityToDefaultsAsync(activity.Id);
                    await LoadCategoriesAsync();
                    await RefreshActivitiesAsync();
                }
            }
        }
    }

    #region Unified Context Menu Helper Methods

    private async Task HandleAppliedXTimes(Activity activity, ActivityGameViewModel? activityVM, bool isStreakAttempt, StreakAttemptViewModel? attemptVM)
    {
        string result = await DisplayPromptAsync(
            "Applied How Many Times?",
            isStreakAttempt
                ? $"Enter number of times '{activity.Name}' was done.\nEach will be processed separately."
                : $"Enter number of times '{activity.Name}' was done today.\nThis is a one-time multiplier (won't save to activity).",
            "OK",
            "Cancel",
            "1",
            maxLength: 3,
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int times))
        {
            if (times > 0 && times <= 999)
            {
                if (isStreakAttempt && attemptVM != null)
                {
                    // For streak containers, apply multiple times immediately
                    int totalExp = 0;
                    for (int i = 0; i < times; i++)
                    {
                        int expAmount = attemptVM.ExpGain * activity.Multiplier;
                        var (earnedExp, _) = await ProcessActivityCompletionAsync(
                            activity,
                            expAmount,
                            $"{activity.Name} (Batch {i + 1}/{times})");
                        totalExp += earnedExp;
                    }

                    await RefreshExpAsync();
                    await RefreshActivitiesAsync();

                    await DisplayAlert("Applied!",
                        $"Recorded {times} times.\n+{totalExp} EXP total.",
                        "OK");
                }
                else if (activityVM != null)
                {
                    // For normal activities, set temporary multiplier
                    activityVM.TemporaryMultiplier = times;

                    if (!activityVM.IsSelected)
                    {
                        activityVM.IsSelected = true;
                    }

                    await DisplayAlert("Temporary Multiplier Set",
                        $"'{activity.Name}' will be applied {times} times for the next calculation only.\n\nThe activity has been selected for you.",
                        "OK");
                }
            }
            else
            {
                await DisplayAlert("Invalid", "Please enter a number between 1 and 999", "OK");
            }
        }
    }

    private async Task HandleUpdateStreakValues(Activity activity, ActivityGameViewModel? activityVM, StreakAttemptViewModel? attemptVM)
    {
        var options = new List<string>
        {
            $"Habit + Display Day Streaks ({activity.HabitStreak}, {activity.DisplayDayStreak})"
        };

        if (attemptVM != null)
        {
            options.Insert(0, $"Attempt Days: {attemptVM.DaysAchieved}");
        }

        string action = await DisplayActionSheet(
            "Update Streak Values",
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action.StartsWith("Attempt Days") && attemptVM != null)
        {
            await EditAttemptDays(attemptVM);
        }
        else if (action.StartsWith("Habit + Display Day Streaks"))
        {
            string? habitResult = await DisplayPromptAsync(
                "Habit Streak",
                $"For 7-day graduation.\nCurrent: {activity.HabitStreak}",
                "Next",
                "Cancel",
                initialValue: activity.HabitStreak.ToString(),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrEmpty(habitResult)) return;

            string? displayResult = await DisplayPromptAsync(
                "Display Day Streak",
                $"Scheduled days bonus.\nCurrent: {activity.DisplayDayStreak}",
                "Save Both",
                "Cancel",
                initialValue: activity.DisplayDayStreak.ToString(),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrEmpty(displayResult)) return;

            if (!int.TryParse(habitResult, out int newHabitStreak) || newHabitStreak < 0 ||
                !int.TryParse(displayResult, out int newDisplayStreak) || newDisplayStreak < 0)
            {
                await DisplayAlert("Invalid", "Please enter whole numbers of 0 or higher.", "OK");
                return;
            }

            activity.HabitStreak = newHabitStreak;
            activity.DisplayDayStreak = newDisplayStreak;
            if (newDisplayStreak > 0)
            {
                activity.LastDisplayDayUsed = DateTime.UtcNow.Date;
            }
            else
            {
                activity.AutoSuggestThreshold = 30;
            }

            await _activities.UpdateActivityAsync(activity);
            activityVM?.UpdateActivity(activity);

            if (activityVM != null)
            {
                activityVM.DisplayDayStreak = newDisplayStreak;
            }

            await DisplayAlert("Updated",
                $"Habit streak: {activity.HabitStreak}\nDisplay day streak: {activity.DisplayDayStreak}",
                "OK");
            await RefreshActivitiesAsync();
        }
    }

    private async Task HandleEditTimesCompleted(Activity activity, ActivityGameViewModel? activityVM)
    {
        string result = await DisplayPromptAsync(
            "Times Completed",
            $"Current: {activity.TimesCompleted}\n\nThis auto-increments when you gain EXP.",
            initialValue: activity.TimesCompleted.ToString(),
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int newCount) && newCount >= 0)
        {
            activity.TimesCompleted = newCount;
            await _activities.UpdateActivityAsync(activity);
            if (activityVM != null)
            {
                activityVM.TimesCompleted = newCount;
            }
            await DisplayAlert("Updated", $"Times completed: {newCount}", "OK");
            await RefreshActivitiesAsync();
        }
    }

    private async Task HandleEditNotes(Activity activity, ActivityGameViewModel? activityVM)
    {
        string result = await DisplayPromptAsync(
            "Notes",
            "Enter notes for this activity:",
            initialValue: activity.Notes ?? "",
            maxLength: 500);

        if (result != null) // Allow empty to clear notes
        {
            activity.Notes = result;
            await _activities.UpdateActivityAsync(activity);
            activityVM?.UpdateActivity(activity);
            await DisplayAlert("Saved", "Notes updated.", "OK");
            await RefreshActivitiesAsync();
        }
    }

    private async Task HandleDuplicateAsNegative(Activity activity)
    {
        string negativeName = $"{activity.Name} (Negative)";
        string encodedName = Uri.EscapeDataString(negativeName);
        string encodedImage = Uri.EscapeDataString(activity.ImagePath ?? "");
        string encodedCategory = Uri.EscapeDataString("Negative");

        var queryParams = $"addactivity?gameId={_game!.GameId}&prefillName={encodedName}&prefillLevel=-500&prefillImage={encodedImage}&prefillCategory={encodedCategory}&isNegative=true&noHabitTarget=true";

        if (activity.StartDate.HasValue)
        {
            queryParams += $"&prefillStartDate={activity.StartDate.Value:o}";
        }

        if (activity.EndDate.HasValue)
        {
            queryParams += $"&prefillEndDate={activity.EndDate.Value:o}";
        }

        await Shell.Current.GoToAsync(queryParams);
    }

    private async Task HandleSetManualPriority(Activity activity, ActivityGameViewModel? activityVM)
    {
        string result = await DisplayPromptAsync(
            "Set Manual Priority",
            "Lower numbers show first. Leave blank to clear.",
            accept: "Save",
            cancel: "Cancel",
            initialValue: activity.ManualPriority?.ToString() ?? "",
            keyboard: Keyboard.Numeric);

        if (result == null)
        {
            return;
        }

        int? newPriority = null;
        if (!string.IsNullOrWhiteSpace(result))
        {
            if (!int.TryParse(result.Trim(), out int parsedPriority) || parsedPriority <= 0)
            {
                await DisplayAlert("Invalid Priority", "Priority must be a positive number or blank.", "OK");
                return;
            }

            newPriority = parsedPriority;
        }

        activity.ManualPriority = newPriority;
        await _activities.UpdateActivityAsync(activity);
        activityVM?.UpdateActivity(activity);
        await RefreshActivitiesAsync();
    }

    private async Task HandleMoveToAnotherGame(Activity activity)
    {
        // Get all games for this user
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);

        // Filter out the current game
        var otherGames = allGames.Where(g => g.GameId != _game!.GameId).ToList();

        if (otherGames.Count == 0)
        {
            await DisplayAlert("No Other Games", "You don't have any other games to move this activity to.", "OK");
            return;
        }

        // Show game selection
        var gameOptions = otherGames.Select(g => $"🎮 {g.DisplayName}").ToArray();
        string selectedGame = await DisplayActionSheet(
            "Move to which game?",
            "Cancel",
            null,
            gameOptions);

        if (string.IsNullOrEmpty(selectedGame) || selectedGame == "Cancel") return;

        // Find the selected game
        string selectedGameName = selectedGame.Replace("🎮 ", "");
        var targetGame = otherGames.FirstOrDefault(g => g.DisplayName == selectedGameName);
        if (targetGame == null) return;

        // Get categories from the target game
        var targetActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, targetGame.GameId);
        var targetCategories = targetActivities
            .Where(a => a.IsActive)
            .Select(a => a.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        // Build category options
        var categoryOptions = new List<string>();

        // Add option to keep current category
        categoryOptions.Add($"📁 {activity.Category} (keep current)");

        // Add existing categories from target game (excluding current)
        foreach (var cat in targetCategories.Where(c => c != activity.Category))
        {
            categoryOptions.Add($"📁 {cat}");
        }

        // Add option for new category
        categoryOptions.Add("✏️ Enter new category...");

        string selectedCategory = await DisplayActionSheet(
            $"Category in '{targetGame.DisplayName}'?",
            "Cancel",
            null,
            categoryOptions.ToArray());

        if (string.IsNullOrEmpty(selectedCategory) || selectedCategory == "Cancel") return;

        string targetCategory;

        if (selectedCategory.Contains("Enter new category"))
        {
            string newCategory = await DisplayPromptAsync(
                "New Category",
                $"Enter category name for '{targetGame.DisplayName}':",
                "OK",
                "Cancel",
                placeholder: "Category name");

            if (string.IsNullOrWhiteSpace(newCategory)) return;
            targetCategory = newCategory.Trim();
        }
        else if (selectedCategory.Contains("(keep current)"))
        {
            targetCategory = activity.Category;
        }
        else
        {
            targetCategory = selectedCategory.Replace("📁 ", "").Trim();
        }

        // Confirm the move
        bool confirm = await DisplayAlert(
            "Move Activity?",
            $"Move '{activity.Name}' to:\n\n" +
            $"Game: {targetGame.DisplayName}\n" +
            $"Category: {targetCategory}\n\n" +
            "The activity will be removed from this game.",
            "Move",
            "Cancel");

        if (!confirm) return;

        // Save current category before moving
        string currentCategoryName = null;
        if (_navigableCategories.Count > 0 && _currentCategoryIndex >= 0 && _currentCategoryIndex < _navigableCategories.Count)
        {
            currentCategoryName = _navigableCategories[_currentCategoryIndex];
        }

        // Perform the move - update the activity's game and category
        activity.Game = targetGame.GameId;
        activity.Category = targetCategory;
        await _activities.UpdateActivityAsync(activity);

        await DisplayAlert("Moved!",
            $"'{activity.Name}' has been moved to '{targetGame.DisplayName}' in category '{targetCategory}'.",
            "OK");

        // Refresh the current view
        await LoadCategoriesAsync();

        // Restore the category index if the category still exists
        if (!string.IsNullOrEmpty(currentCategoryName))
        {
            int restoredIndex = _navigableCategories.IndexOf(currentCategoryName);
            if (restoredIndex >= 0)
            {
                _currentCategoryIndex = restoredIndex;
                UpdateCategoryDisplay();
            }
            // If category no longer exists (was the only activity), stay at index 0
        }

        await RefreshActivitiesAsync();
    }

    private async Task HandleAssignToGrouping(Activity activity)
    {
        if (_groupingService == null) return;

        var groupings = await _groupingService.GetGroupingsAsync(_auth.CurrentUsername);

        int? selectedGroupingId = await ShowGroupingSelectionModalAsync(activity, groupings);
        if (selectedGroupingId == null) return;

        if (selectedGroupingId == -1)
        {
            string? name = await DisplayPromptAsync(
                "New Grouping",
                "Enter a name for the grouping:",
                "Create",
                "Cancel",
                placeholder: "e.g., Morning Routine",
                maxLength: 100);

            if (!string.IsNullOrWhiteSpace(name))
            {
                var newGrouping = await _groupingService.CreateGroupingAsync(_auth.CurrentUsername, name);
                await _groupingService.AddActivityToGroupingAsync(newGrouping.Id, activity.Id);
                await DisplayAlert("Added", $"'{activity.Name}' added to '{name}'.", "OK");
            }
            return;
        }

        var selectedGrouping = groupings.FirstOrDefault(g => g.Id == selectedGroupingId.Value);
        if (selectedGrouping == null) return;

        bool currentlyIn = await _groupingService.IsActivityInGroupingAsync(selectedGrouping.Id, activity.Id);

        if (currentlyIn)
        {
            await _groupingService.RemoveActivityFromGroupingAsync(selectedGrouping.Id, activity.Id);
            await DisplayAlert("Removed", $"'{activity.Name}' removed from '{selectedGrouping.Name}'.", "OK");
        }
        else
        {
            await _groupingService.AddActivityToGroupingAsync(selectedGrouping.Id, activity.Id);
            await DisplayAlert("Added", $"'{activity.Name}' added to '{selectedGrouping.Name}'.", "OK");
        }
    }

    private async Task<int?> ShowGroupingSelectionModalAsync(Activity activity, List<ActivityGrouping> groupings)
    {
        var completion = new TaskCompletionSource<int?>();
        ContentPage? modalPage = null;

        async Task CompleteAsync(int? result)
        {
            if (completion.Task.IsCompleted) return;

            completion.SetResult(result);
            if (modalPage != null)
            {
                await Navigation.PopModalAsync();
            }
        }

        var listStack = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(0, 4, 0, 0)
        };

        foreach (var grouping in groupings)
        {
            bool isIn = await _groupingService!.IsActivityInGroupingAsync(grouping.Id, activity.Id);
            listStack.Children.Add(BuildGroupingSelectionRow(
                isIn ? "✓" : "+",
                grouping.Name,
                isIn ? "Tap to remove from this grouping" : "Tap to add to this grouping",
                isIn ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#F5F5F5"),
                isIn ? Color.FromArgb("#2E7D32") : Color.FromArgb("#5E35B1"),
                () => CompleteAsync(grouping.Id)));
        }

        listStack.Children.Add(BuildGroupingSelectionRow(
            "+",
            "Create New Grouping",
            "Add this activity to a new custom group",
            Color.FromArgb("#FFF8E1"),
            Color.FromArgb("#F9A825"),
            () => CompleteAsync(-1)));

        var doneButton = new Button
        {
            Text = "Done",
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            TextColor = Colors.Black,
            CornerRadius = 8,
            HeightRequest = 40,
            Margin = new Thickness(0, 8, 0, 0)
        };
        doneButton.Clicked += async (_, _) => await CompleteAsync(null);

        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Groupings for",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#666666")
                },
                new Label
                {
                    Text = activity.Name,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#E0E0E0"),
                    Margin = new Thickness(0, 4)
                },
                new ScrollView
                {
                    MaximumHeightRequest = 360,
                    Content = listStack
                },
                doneButton
            }
        };

        modalPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = new Grid
            {
                Padding = new Thickness(24),
                Children =
                {
                    new Frame
                    {
                        Padding = 18,
                        CornerRadius = 8,
                        HasShadow = true,
                        BackgroundColor = Colors.White,
                        BorderColor = Color.FromArgb("#DDDDDD"),
                        MaximumWidthRequest = 620,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Content = content
                    }
                }
            }
        };

        await Navigation.PushModalAsync(modalPage);
        return await completion.Task;
    }

    private static Frame BuildGroupingSelectionRow(
        string icon,
        string title,
        string subtitle,
        Color backgroundColor,
        Color iconColor,
        Func<Task> onTap)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 32 },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };

        var iconLabel = new Label
        {
            Text = icon,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = iconColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(iconLabel, 0);
        row.Children.Add(iconLabel);

        var textStack = new VerticalStackLayout { Spacing = 2 };
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 14,
            TextColor = Colors.Black,
            LineBreakMode = LineBreakMode.WordWrap
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 11,
            TextColor = Color.FromArgb("#777777"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        Grid.SetColumn(textStack, 1);
        row.Children.Add(textStack);

        var frame = new Frame
        {
            Padding = new Thickness(10, 8),
            CornerRadius = 8,
            HasShadow = false,
            BackgroundColor = backgroundColor,
            BorderColor = Color.FromArgb("#E0E0E0"),
            Content = row
        };
        frame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await onTap())
        });

        return frame;
    }

    #endregion
}
