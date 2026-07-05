using Bannister.Helpers;
using Bannister.Models;
using Bannister.Services;
using Bannister.ViewModels;

namespace Bannister.Views;

/// <summary>
/// Partial class containing event handlers.
/// Uses shared logic from ActivityGamePage_SharedLogic.cs for completion and context menu handling.
/// </summary>
public partial class ActivityGamePage
{
    /// <summary>
    /// Handle Calculate EXP button click.
    /// Uses ProcessActivityCompletionAsync for shared completion logic.
    /// </summary>
    private async void OnCalculateClicked(object? sender, EventArgs e)
    {
        var selected = _allActivities?.Where(a => a.IsSelected).ToList();

        if (selected == null || selected.Count == 0)
        {
            await DisplayAlert("No Selection", "Select activities first (click to highlight with green border), then calculate.", "OK");
            return;
        }

        // Get current state BEFORE applying EXP
        var (levelBefore, expIntoBefore, expNeededBefore) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game!.GameId);
        double progressBefore = expNeededBefore > 0 ? (double)expIntoBefore / expNeededBefore : 0;

        // Calculate totals and process each activity
        int totalExp = 0;
        var details = new List<string>();

        foreach (var activityVM in selected)
        {
            int baseExp = activityVM.ExpGain;
            int effectiveMultiplier = activityVM.EffectiveMultiplier;
            int expForThisActivity = baseExp * effectiveMultiplier;
            bool isFirstZeroCountCompletion = activityVM.Activity.IsZeroCount && activityVM.Activity.TimesCompleted == 0;

            if (isFirstZeroCountCompletion)
            {
                string expGameId = _isGroupingMode ? activityVM.Activity.Game : _game!.GameId;
                await _exp.ApplyExpAsync(_auth.CurrentUsername, expGameId, activityVM.Name, expForThisActivity * 10, activityVM.Id);
            }
            else
            {
                // Apply EXP multiple times if multiplier > 1 (just the EXP logging)
                for (int i = 0; i < effectiveMultiplier; i++)
                {
                    // In grouping mode, use the activity's own game for EXP
                    string expGameId = _isGroupingMode ? activityVM.Activity.Game : _game!.GameId;
                    await _exp.ApplyExpAsync(_auth.CurrentUsername, expGameId, activityVM.Name, baseExp, activityVM.Id);
                }
            }

            // Process completion ONCE per activity using shared logic
            // This handles: streak tracking, habit completion, display day streak,
            // times completed, NewHabit progress, and streak bonus
            var (bonusExp, bonusDetails) = await ProcessActivityCompletionCoreAsync(activityVM.Activity);
            
            totalExp += (isFirstZeroCountCompletion ? expForThisActivity * 10 : expForThisActivity) + bonusExp;

            // Update ViewModel with new values from activity
            activityVM.DisplayDayStreak = activityVM.Activity.DisplayDayStreak;
            activityVM.TimesCompleted = activityVM.Activity.TimesCompleted;

            // Build detail string
            string sign = expForThisActivity >= 0 ? "+" : "";
            string multiplierInfo = activityVM.TemporaryMultiplier > 1 
                ? $" (x{activityVM.TemporaryMultiplier} times)" 
                : activityVM.Multiplier > 1 
                    ? $" (x{activityVM.Multiplier})" 
                    : "";
            string zeroCountInfo = isFirstZeroCountCompletion ? " (Zero Count x10)" : "";
            int displayedExp = isFirstZeroCountCompletion ? expForThisActivity * 10 : expForThisActivity;
            string displayedSign = displayedExp >= 0 ? "+" : "";
            details.Add($"{activityVM.Name}{multiplierInfo}{zeroCountInfo}: {displayedSign}{displayedExp}");
            
            if (!string.IsNullOrEmpty(bonusDetails))
            {
                details.Add(bonusDetails);
            }
        }

        int totalExpValue = await _exp.GetTotalExpAsync(_auth.CurrentUsername, _game!.GameId);
        
        // Handle negative EXP case
        if (totalExpValue < 0)
        {
            await RefreshExpAsync();
            
            foreach (var activityVM in selected)
            {
                activityVM.IsSelected = false;
                activityVM.TemporaryMultiplier = 1;
            }

            string signPrefix = totalExp >= 0 ? "+" : "";
            await DisplayAlert("EXP Applied", $"Total: {signPrefix}{totalExp} EXP\n\n⚠️ You're in negative EXP territory!\nCurrent Total: {totalExpValue:N0}\n\n{string.Join("\n", details)}", "OK");
            await PromptForAutoAwardSuggestionsAsync();
            return;
        }

        // Get state AFTER applying EXP
        var (levelAfter, expIntoAfter, expNeededAfter) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game!.GameId);
        double progressAfter = expNeededAfter > 0 ? (double)expIntoAfter / expNeededAfter : 0;
        bool leveledUp = levelAfter > levelBefore;

        // Update labels immediately
        lblCurrentLevel.Text = $"Level {levelAfter}";
        lblCurrentLevel.TextColor = Color.FromArgb("#5B63EE");
        lblExpToNext.Text = $"{expIntoAfter} / {expNeededAfter} EXP";
        lblExpTotal.Text = $"Total EXP: {totalExpValue:N0}";
        if (expProgressBar != null)
            expProgressBar.ProgressColor = Color.FromArgb("#4CAF50");

        await AnimateExpBar(progressBefore, progressAfter, leveledUp);
        await RefreshExpAsync();

        // Clear selections and reset temporary multipliers
        foreach (var activityVM in selected)
        {
            activityVM.IsSelected = false;
            activityVM.TemporaryMultiplier = 1;
        }

        string sign2 = totalExp >= 0 ? "+" : "";
        string levelUpMsg = leveledUp ? $"\n\n🎉 LEVEL UP! Now Level {levelAfter}!" : "";
        await DisplayAlert("EXP Applied", $"Total: {sign2}{totalExp} EXP{levelUpMsg}\n\n{string.Join("\n", details)}", "OK");
        await PromptForAutoAwardSuggestionsAsync();
    }

    private async void OnNextGameClicked(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[NEXT_GAME] Button clicked");
        try
        {
            var username = _auth?.CurrentUsername ?? "";
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] username={username}");
            if (string.IsNullOrWhiteSpace(username))
            {
                await DisplayAlert("No user", "Cannot determine current user.", "OK");
                return;
            }

            var games = await _games.GetGamesAsync(username);
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] games count={games?.Count ?? 0}");
            if (games == null || games.Count == 0)
            {
                await DisplayAlert("No games", "No games available.", "OK");
                return;
            }

            var currentId = _game?.GameId ?? _gameId ?? "";
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] currentId={currentId}");
            var currentIndex = games.FindIndex(g =>
                string.Equals(g.GameId, currentId, StringComparison.OrdinalIgnoreCase));

            if (currentIndex < 0)
            {
                System.Diagnostics.Debug.WriteLine("[NEXT_GAME] current game not found in list; defaulting index to 0");
                currentIndex = 0;
            }

            var nextIndex = (currentIndex + 1) % games.Count;
            var nextGame = games[nextIndex];
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] nextGameId={nextGame.GameId} displayName={nextGame.DisplayName}");

            if (string.Equals(nextGame.GameId, currentId, StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Only one game", "There are no other games to navigate to.", "OK");
                return;
            }

            await _games.UpdateLastVisitedAtAsync(username, nextGame.GameId, DateTime.Now);
            System.Diagnostics.Debug.WriteLine("[NEXT_GAME] Updated last-visited");
            var encodedGameId = Uri.EscapeDataString(nextGame.GameId);
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] About to navigate, encodedGameId={encodedGameId}");
            await Shell.Current.GoToAsync($"activitygrid?gameId={encodedGameId}");
            System.Diagnostics.Debug.WriteLine("[NEXT_GAME] Navigation call completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[NEXT_GAME] Stack: {ex.StackTrace}");
            await DisplayAlert("Next Game error", $"{ex.GetType().Name}: {ex.Message}", "OK");
        }
    }

    private async void OnHomeClicked(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[HOME] Button clicked");
        try
        {
            try
            {
                await Shell.Current.GoToAsync("//home");
                System.Diagnostics.Debug.WriteLine("[HOME] Shell GoToAsync //home completed");
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HOME] Shell GoToAsync failed: {ex.Message}, trying PopToRootAsync");
            }

            await Navigation.PopToRootAsync();
            System.Diagnostics.Debug.WriteLine("[HOME] PopToRootAsync completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HOME] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            await DisplayAlert("Home error", $"{ex.GetType().Name}: {ex.Message}", "OK");
        }
    }

    private async Task PromptForAutoAwardSuggestionsAsync()
    {
        if (_db.IsReadOnly || _allActivities == null || _allActivities.Count == 0)
            return;

        var activeStreakAttemptActivityIds = await GetActiveStreakAttemptActivityIdsAsync();

        var candidates = _allActivities
            .Select(vm => vm.Activity)
            .Where(activity =>
            {
                int threshold = activity.AutoSuggestThreshold <= 0 ? 30 : activity.AutoSuggestThreshold;
                return activity.IsActive
                    && !activity.IsAutoAward
                    && !activeStreakAttemptActivityIds.Contains(activity.Id)
                    && activity.DisplayDayStreak >= threshold;
            })
            .OrderBy(activity => activity.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
            return;

        bool changed = false;

        foreach (var activity in candidates)
        {
            int threshold = activity.AutoSuggestThreshold <= 0 ? 30 : activity.AutoSuggestThreshold;
            string? action = await AutoAwardSuggestionPromptPage.ShowAsync(
                Navigation,
                activity.Name,
                activity.DisplayDayStreak,
                threshold);

            if (action == "Set to Auto")
            {
                activity.IsAutoAward = true;
                if (string.IsNullOrWhiteSpace(activity.AutoAwardFrequency) || activity.AutoAwardFrequency == "None")
                {
                    activity.AutoAwardFrequency = "Daily";
                }
                if (activity.AutoAwardFrequency != "Weekly")
                {
                    activity.AutoAwardDays = "";
                }
                activity.Category = "Auto";

                await _activities.UpdateActivityAsync(activity);
                changed = true;
            }
            else if (action == "Postpone")
            {
                int? postponedThreshold = await ChooseAutoSuggestThresholdAsync(activity);
                if (postponedThreshold.HasValue)
                {
                    int currentStreak = Math.Max(0, activity.DisplayDayStreak);
                    activity.AutoSuggestThreshold = currentStreak + postponedThreshold.Value;
                    await _activities.UpdateActivityAsync(activity);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            int previousCategoryIndex = _currentCategoryIndex;
            string? previousTempCategory = _tempNonNavigableCategory;

            await LoadCategoriesAsync();
            RestoreCategoryPageAfterPromptRefresh(previousCategoryIndex, previousTempCategory);
            await RefreshActivitiesAsync();
        }
    }

    private async Task<HashSet<int>> GetActiveStreakAttemptActivityIdsAsync()
    {
        var activityIds = new HashSet<int>();

        if (_allActivities == null || _allActivities.Count == 0)
            return activityIds;

        try
        {
            var activeStreaksByGame = new Dictionary<string, List<StreakAttempt>>(StringComparer.OrdinalIgnoreCase);

            foreach (var activityVM in _allActivities)
            {
                var activity = activityVM.Activity;
                if (!activity.IsStreakTracked)
                    continue;

                string streakGameId = GetActivityGameId(activity);
                if (!activeStreaksByGame.TryGetValue(streakGameId, out var activeStreaks))
                {
                    activeStreaks = await _streaks.GetActiveStreaksAsync(_auth.CurrentUsername, streakGameId);
                    activeStreaksByGame[streakGameId] = activeStreaks;
                }

                if (activeStreaks.Any(streak => streak.ActivityId == activity.Id))
                    activityIds.Add(activity.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR loading active streak attempts for auto-award suggestions: {ex.Message}");
        }

        return activityIds;
    }

    private async Task<int?> ChooseAutoSuggestThresholdAsync(Activity activity)
    {
        return await AutoAwardPostponePromptPage.ShowAsync(
            Navigation,
            activity.Name,
            Math.Max(activity.AutoSuggestThreshold, 30));
    }

    private void RestoreCategoryPageAfterPromptRefresh(int previousCategoryIndex, string? previousTempCategory)
    {
        categoryPicker.SelectedIndexChanged -= OnCategoryChanged;
        try
        {
            if (!string.IsNullOrWhiteSpace(previousTempCategory) &&
                _categories.Any(c => string.Equals(c, previousTempCategory, StringComparison.OrdinalIgnoreCase)))
            {
                _tempNonNavigableCategory = previousTempCategory;
                _currentCategoryIndex = _navigableCategories.Count > 0
                    ? Math.Clamp(previousCategoryIndex, 0, _navigableCategories.Count - 1)
                    : 0;
                UpdateCategoryDisplay();
                return;
            }

            _tempNonNavigableCategory = null;
            _currentCategoryIndex = _navigableCategories.Count > 0
                ? Math.Clamp(previousCategoryIndex, 0, _navigableCategories.Count - 1)
                : 0;
            UpdateCategoryDisplay();
        }
        finally
        {
            categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        }
    }

    /// <summary>
    /// Show context menu for a normal activity card.
    /// Delegates to ShowUnifiedContextMenu (shared logic).
    /// </summary>
    private async Task ShowContextMenu(ActivityGameViewModel activityVM)
    {
        await ShowUnifiedContextMenu(activityVM.Activity, isStreakAttempt: false, attemptVM: null);
    }

    private async Task DisableActivity(ActivityGameViewModel activityVM)
    {
        bool confirm = await DisplayAlert(
            "Disable Activity",
            $"Disable '{activityVM.Name}'?\n\nThis will hide the activity (set IsActive = false). You can restore it later from Manage Activities.",
            "Disable",
            "Cancel");
        
        if (!confirm) return;
        
        await _activities.BlankActivityAsync(activityVM.Activity.Id);
        await RefreshActivitiesAsync();
    }
    
    private async Task RemoveActivity(ActivityGameViewModel activityVM)
    {
        bool confirm = await DisplayAlert(
            "Remove Activity",
            $"⚠️ Remove '{activityVM.Name}' completely?\n\nThis will reset ALL data for this activity (name, EXP, image, streaks, etc.) to defaults. The row will remain in the database but be completely blank.\n\nThis cannot be undone!",
            "Remove",
            "Cancel");
        
        if (!confirm) return;
        
        bool doubleConfirm = await DisplayAlert(
            "Are you sure?",
            $"Really remove '{activityVM.Name}'? All data will be lost.",
            "Yes, Remove It",
            "No, Keep It");
        
        if (!doubleConfirm) return;
        
        await _activities.ResetActivityToDefaultsAsync(activityVM.Activity.Id);
        await RefreshActivitiesAsync();
    }

    private async Task EditActivity(ActivityGameViewModel activityVM)
    {
        var editPage = new EditActivityPage(_auth, _activities, _game!.GameId, activityVM.Activity);
        await Navigation.PushModalAsync(editPage);
        await RefreshActivitiesAsync();
    }

    private async Task SetMultiplier(ActivityGameViewModel activityVM)
    {
        var multiplierPage = new SetMultiplierPage(activityVM.Activity);
        await Navigation.PushModalAsync(multiplierPage);

        var result = await multiplierPage.WaitForResultAsync();
        if (result.HasValue)
        {
            activityVM.Activity.Multiplier = result.Value;
            await _activities.UpdateActivityAsync(activityVM.Activity);
            activityVM.UpdateActivity(activityVM.Activity);
        }
    }

    private async Task SetTemporaryMultiplier(ActivityGameViewModel activityVM)
    {
        string result = await DisplayPromptAsync(
            "Applied How Many Times?",
            $"Enter number of times '{activityVM.Name}' was done today.\nThis is a one-time multiplier (won't save to activity).",
            "OK",
            "Cancel",
            "1",
            maxLength: 3,
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int times))
        {
            if (times > 0 && times <= 999)
            {
                activityVM.TemporaryMultiplier = times;
                
                if (!activityVM.IsSelected)
                {
                    activityVM.IsSelected = true;
                }
                
                await DisplayAlert("Temporary Multiplier Set", 
                    $"'{activityVM.Name}' will be applied {times} times for the next calculation only.\n\nThe activity has been selected for you.", 
                    "OK");
            }
            else
            {
                await DisplayAlert("Invalid", "Please enter a number between 1 and 999", "OK");
            }
        }
    }

    private async Task DuplicateAsNegative(ActivityGameViewModel activityVM)
    {
        string negativeName = $"{activityVM.Name} (Negative)";
        string encodedName = Uri.EscapeDataString(negativeName);
        string encodedImage = Uri.EscapeDataString(activityVM.ImagePath ?? "");
        string encodedCategory = Uri.EscapeDataString("Negative");
        
        var queryParams = $"addactivity?gameId={_game!.GameId}&prefillName={encodedName}&prefillLevel=-500&prefillImage={encodedImage}&prefillCategory={encodedCategory}&isNegative=true&noHabitTarget=true";
        
        if (activityVM.Activity.StartDate.HasValue)
        {
            queryParams += $"&prefillStartDate={activityVM.Activity.StartDate.Value:o}";
        }
        
        if (activityVM.Activity.EndDate.HasValue)
        {
            queryParams += $"&prefillEndDate={activityVM.Activity.EndDate.Value:o}";
        }
        
        await Shell.Current.GoToAsync(queryParams);
    }

    private async Task SetAutoAward(ActivityGameViewModel activityVM)
    {
        var autoAwardPage = new SetAutoAwardPage(activityVM.Activity);
        await Navigation.PushModalAsync(autoAwardPage);

        var result = await autoAwardPage.WaitForResultAsync();
        if (result)
        {
            await _activities.UpdateActivityAsync(activityVM.Activity);
            activityVM.UpdateActivity(activityVM.Activity);
            await RefreshActivitiesAsync();
            
            await DisplayAlert("Success", 
                $"Auto-award configured for '{activityVM.Name}'", 
                "OK");
        }
    }

    private async void OnShowAllToggled(object? sender, EventArgs e)
    {
        _showAllActivities = !_showAllActivities;
        
        if (_showAllActivities)
        {
            btnShowAll.Text = "👁️ Showing ALL";
            btnShowAll.BackgroundColor = Color.FromArgb("#4CAF50");
            btnShowAll.TextColor = Colors.White;
        }
        else
        {
            btnShowAll.Text = "👁️ Show All Days";
            btnShowAll.BackgroundColor = Color.FromArgb("#E0E0E0");
            btnShowAll.TextColor = Color.FromArgb("#666");
        }
        
        _tempNonNavigableCategory = null;
        await UpdateNavigableCategoriesAsync();
        
        if (_currentCategoryIndex >= _navigableCategories.Count)
        {
            _currentCategoryIndex = 0;
        }
        
        UpdateCategoryDisplay();
        await RefreshActivitiesAsync();
    }

    private async Task UpdateHabitStreak(ActivityGameViewModel activityVM)
    {
        var activity = activityVM.Activity;
        
        // Ask for Habit Streak first
        string? habitResult = await DisplayPromptAsync(
            "Habit Streak",
            $"For 7-day graduation.\nCurrent: {activity.HabitStreak}",
            "Next",
            "Cancel",
            initialValue: activity.HabitStreak.ToString(),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrEmpty(habitResult)) return;

        // Then ask for Display Day Streak
        string? displayResult = await DisplayPromptAsync(
            "Display Day Streak",
            $"Scheduled days bonus.\nCurrent: {activity.DisplayDayStreak}",
            "Save Both",
            "Cancel",
            initialValue: activity.DisplayDayStreak.ToString(),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrEmpty(displayResult)) return;

        // Apply both
        bool changed = false;

        if (int.TryParse(habitResult, out int newHabitStreak) && newHabitStreak >= 0)
        {
            activity.HabitStreak = newHabitStreak;
            changed = true;
        }

        if (int.TryParse(displayResult, out int newDisplayStreak) && newDisplayStreak >= 0)
        {
            activity.DisplayDayStreak = newDisplayStreak;
            if (newDisplayStreak > 0)
            {
                activity.LastDisplayDayUsed = DateTime.UtcNow.Date;
            }
            else
            {
                activity.AutoSuggestThreshold = 30;
            }
            activityVM.DisplayDayStreak = newDisplayStreak;
            changed = true;
        }

        if (changed)
        {
            await _activities.UpdateActivityAsync(activity);
            activityVM.UpdateActivity(activity);
            await DisplayAlert("Updated",
                $"Habit streak: {activity.HabitStreak}\nDisplay day streak: {activity.DisplayDayStreak}",
                "OK");
        }

        await RefreshActivitiesAsync();
    }

    private async Task EditTimesCompleted(ActivityGameViewModel activityVM)
    {
        var activity = activityVM.Activity;
        
        string result = await DisplayPromptAsync(
            "Times Completed",
            $"Current: {activity.TimesCompleted}\n\nThis auto-increments when you gain EXP.",
            initialValue: activity.TimesCompleted.ToString(),
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int newCount) && newCount >= 0)
        {
            activity.TimesCompleted = newCount;
            await _activities.UpdateActivityAsync(activity);
            activityVM.TimesCompleted = newCount;
            await DisplayAlert("Updated", $"Times completed: {newCount}", "OK");
            await RefreshActivitiesAsync();
        }
    }

    private async Task EditNotes(ActivityGameViewModel activityVM)
    {
        var activity = activityVM.Activity;
        
        string? result = await DisplayPromptAsync(
            "📝 Notes",
            "Enter notes or clarifications for this activity:",
            initialValue: activity.Notes ?? "",
            maxLength: 500,
            placeholder: "e.g., Only applies after 4pm, Only on weekdays...");

        if (result == null) return;

        activity.Notes = result.Trim();
        await _activities.UpdateActivityAsync(activity);
        
        activityVM.UpdateActivity(activity);
        
        if (string.IsNullOrWhiteSpace(result))
        {
            await DisplayAlert("Notes Cleared", "Notes have been removed.", "OK");
        }
        else
        {
            await DisplayAlert("Notes Saved", "Notes have been updated.", "OK");
        }
        
        await RefreshActivitiesAsync();
    }

    /// <summary>
    /// Record that a habit's positive activity was completed.
    /// ONLY increments the streak counter - does NOT graduate or change allowance.
    /// Graduation happens only when entering the Habits page.
    /// </summary>
    private async Task RecordNewHabitProgressAsync(int activityId)
    {
        try
        {
            var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
            if (dbService == null) return;

            var conn = await dbService.GetConnectionAsync();
            
            var habit = await conn.Table<NewHabit>()
                .Where(h => h.Username == _auth.CurrentUsername 
                    && h.PositiveActivityId == activityId 
                    && h.Status == "active")
                .FirstOrDefaultAsync();

            if (habit == null) return;

            var today = DateTime.UtcNow.Date;

            if (habit.LastAppliedDate.HasValue && habit.LastAppliedDate.Value.Date == today)
            {
                System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Already recorded today for {habit.HabitName}");
                return;
            }

            bool isConsecutive = habit.LastAppliedDate.HasValue 
                && (today - habit.LastAppliedDate.Value.Date).TotalDays == 1;

            if (isConsecutive || habit.ConsecutiveDays == 0)
            {
                habit.ConsecutiveDays++;
            }
            else
            {
                habit.ConsecutiveDays = 1;
            }

            habit.LastAppliedDate = today;

            // NOTE: We do NOT graduate here anymore.
            // Graduation only happens in the Habits page with user confirmation.
            // This method ONLY updates the streak counter.

            await conn.UpdateAsync(habit);
            System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Recorded day {habit.ConsecutiveDays}/{habit.DaysToGraduate} for {habit.HabitName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Error recording progress: {ex.Message}");
        }
    }

    private void OnSelectAllClicked(object? sender, EventArgs e)
    {
        if (_currentlyVisibleActivities == null) return;
        
        foreach (var activity in _currentlyVisibleActivities)
        {
            activity.IsSelected = true;
        }
    }

    private async void OnOptionsClicked(object? sender, EventArgs e)
    {
        var popup = new OptionsPopupPage();
        await Navigation.PushModalAsync(popup);
        
        string? result = await popup.WaitForResultAsync();
        
        if (string.IsNullOrEmpty(result)) return;

        if (result == "View Activity Log")
        {
            OnViewLogClicked(sender, e);
        }
        else if (result == "Refresh")
        {
            await RefreshActivitiesAsync();
        }
        else if (result == "Manual EXP Adjustment")
        {
            await ManualExpAdjustmentAsync();
        }
        else if (result == "Change Category (All on Page)")
        {
            await BulkChangeCategoryAsync();
        }
        else if (result == "Set All as Possible (All on Page)")
        {
            await BulkSetAsPossibleAsync();
        }
        else if (result == "Export Data")
        {
            await DisplayAlert("Export", "Export functionality coming soon.", "OK");
        }
        else if (result == "Run SQL (Dev)")
        {
            await RunDevSqlAsync();
        }
    }

    private async Task RunDevSqlAsync()
    {
        var inputPage = new SqlInputPage();
        await Navigation.PushModalAsync(inputPage);
        
        string? sql = await inputPage.WaitForResultAsync();

        if (string.IsNullOrWhiteSpace(sql)) return;

        try
        {
            var conn = await _db.GetConnectionAsync();
            string trimmed = sql.Trim();

            if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                var queryResult = await conn.ExecuteScalarAsync<string>(trimmed);
                await DisplayAlert("Result", queryResult?.ToString() ?? "(null)", "OK");
            }
            else
            {
                int rows = await conn.ExecuteAsync(trimmed);
                await DisplayAlert("Success", $"Affected {rows} row(s).", "OK");
                await RefreshActivitiesAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("SQL Error", ex.Message, "OK");
        }
    }

    private async Task BulkSetAsPossibleAsync()
    {
        if (_game == null) return;

        var displayedActivities = GetCurrentlyDisplayedActivities();
        
        if (displayedActivities.Count == 0)
        {
            await DisplayAlert("No Activities", "No activities are currently displayed on this page.", "OK");
            return;
        }

        int notPossibleCount = displayedActivities.Count(a => !a.Activity.IsPossible);
        
        if (notPossibleCount == 0)
        {
            await DisplayAlert("Already Possible", "All activities on this page are already marked as Possible.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Set as Possible",
            $"Mark {notPossibleCount} activities as 'Possible'?\n\nPossible activities won't show in normal filters - only under 'Possible' filter.",
            "Yes",
            "No");

        if (!confirm) return;

        int updated = 0;
        foreach (var activityVM in displayedActivities)
        {
            if (!activityVM.Activity.IsPossible)
            {
                activityVM.Activity.IsPossible = true;
                await _activities.UpdateActivityAsync(activityVM.Activity);
                updated++;
            }
        }

        await DisplayAlert("Done", $"Marked {updated} activities as Possible.", "OK");
        await RefreshActivitiesAsync();
    }

    private async Task BulkChangeCategoryAsync()
    {
        if (_game == null) return;

        var displayedActivities = GetCurrentlyDisplayedActivities();
        
        if (displayedActivities.Count == 0)
        {
            await DisplayAlert("No Activities", "No activities are currently displayed on this page.", "OK");
            return;
        }

        var options = new List<string>();
        foreach (var cat in _categories.OrderBy(c => c))
        {
            options.Add(cat);
        }
        options.Add("➕ New Category");
        options.Add("Cancel");

        string? selectedCategory = await DisplayActionSheet(
            $"Move {displayedActivities.Count} activities to:",
            "Cancel",
            null,
            options.ToArray());

        if (selectedCategory == null || selectedCategory == "Cancel") return;

        string newCategory;
        if (selectedCategory == "➕ New Category")
        {
            string? newCatName = await DisplayPromptAsync(
                "New Category",
                "Enter new category name:");
            
            if (string.IsNullOrWhiteSpace(newCatName)) return;
            newCategory = newCatName.Trim();
        }
        else
        {
            newCategory = selectedCategory;
        }

        bool confirm = await DisplayAlert(
            "Confirm",
            $"Move {displayedActivities.Count} activities to '{newCategory}'?",
            "Yes",
            "No");

        if (!confirm) return;

        foreach (var activityVM in displayedActivities)
        {
            activityVM.Activity.Category = newCategory;
            await _activities.UpdateActivityAsync(activityVM.Activity);
        }

        await DisplayAlert("Done", $"Moved {displayedActivities.Count} activities to '{newCategory}'.", "OK");
        await LoadCategoriesAsync();
        await RefreshActivitiesAsync();
    }

    private List<ActivityGameViewModel> GetCurrentlyDisplayedActivities()
    {
        var displayed = new List<ActivityGameViewModel>();
        
        if (activitiesCollection?.Content is not VerticalStackLayout mainStack) 
            return displayed;

        foreach (var child in mainStack.Children)
        {
            if (child is Grid row)
            {
                foreach (var gridChild in row.Children)
                {
                    if (gridChild is Frame frame && frame.BindingContext is ActivityGameViewModel vm)
                    {
                        displayed.Add(vm);
                    }
                }
            }
            else if (child is Frame frame && frame.BindingContext is ActivityGameViewModel vm)
            {
                displayed.Add(vm);
            }
        }

        return displayed;
    }

    private async Task ManualExpAdjustmentAsync()
    {
        if (_game == null) return;

        string? amountStr = await DisplayPromptAsync(
            "Manual EXP Adjustment",
            "Enter EXP amount (negative to subtract):",
            placeholder: "e.g. 100 or -50",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(amountStr)) return;

        if (!int.TryParse(amountStr, out int amount))
        {
            await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
            return;
        }

        if (amount == 0)
        {
            await DisplayAlert("No Change", "Amount is 0, no change made.", "OK");
            return;
        }

        string? reason = await DisplayPromptAsync(
            "Reason (optional)",
            "Enter a reason for this adjustment:",
            placeholder: "e.g. Correction, Bonus, etc.");

        string description = string.IsNullOrWhiteSpace(reason) 
            ? "Manual EXP Adjustment" 
            : $"Manual: {reason.Trim()}";

        await _exp.ApplyExpAsync(_auth.CurrentUsername, _game.GameId, description, amount, 0);
        await RefreshExpAsync();
        
        string sign = amount > 0 ? "+" : "";
        await DisplayAlert("EXP Adjusted", $"{sign}{amount} EXP applied.\n\nReason: {description}", "OK");
    }
}

internal class AutoAwardSuggestionPromptPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private readonly string _activityName;
    private readonly int _streak;
    private readonly int _threshold;
    private bool _isClosing;

    private AutoAwardSuggestionPromptPage(string activityName, int streak, int threshold)
    {
        _activityName = activityName;
        _streak = streak;
        _threshold = threshold;
        BackgroundColor = Color.FromArgb("#80000000");
        BuildUI();
    }

    public static async Task<string?> ShowAsync(INavigation navigation, string activityName, int streak, int threshold)
    {
        var page = new AutoAwardSuggestionPromptPage(activityName, streak, threshold);
        await navigation.PushModalAsync(page);
        return await page._completion.Task;
    }

    private void BuildUI()
    {
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 16,
            Padding = 0,
            HasShadow = true,
            WidthRequest = 420,
            MaximumWidthRequest = 520,
            MaximumHeightRequest = 620,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(20)
        };

        var stack = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        var titleLabel = new Label
        {
            Text = "Auto EXP Reward?",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            Padding = new Thickness(22, 20, 22, 8)
        };
        Grid.SetRow(titleLabel, 0);
        stack.Children.Add(titleLabel);

        var topDivider = new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        };
        Grid.SetRow(topDivider, 1);
        stack.Children.Add(topDivider);

        var contentStack = new VerticalStackLayout
        {
            Spacing = 14,
            Padding = new Thickness(22, 18)
        };

        contentStack.Children.Add(new Label
        {
            Text = _activityName,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        contentStack.Children.Add(new Label
        {
            Text = $"This activity's Display Day Streak is {_streak}, which has reached the auto-suggestion threshold ({_threshold}). Turn on auto EXP reward?",
            FontSize = 15,
            TextColor = Color.FromArgb("#555555"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var contentScroll = new ScrollView { Content = contentStack };
        Grid.SetRow(contentScroll, 2);
        stack.Children.Add(contentScroll);

        var bottomDivider = new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        };
        Grid.SetRow(bottomDivider, 3);
        stack.Children.Add(bottomDivider);

        var buttonStack = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(22, 16, 22, 22)
        };

        buttonStack.Children.Add(BuildButton("Set to Auto", Color.FromArgb("#4CAF50"), Colors.White, "Set to Auto"));
        buttonStack.Children.Add(BuildButton("Postpone", Color.FromArgb("#FBC02D"), Color.FromArgb("#333333"), "Postpone"));
        buttonStack.Children.Add(BuildButton("Skip", Color.FromArgb("#EEEEEE"), Color.FromArgb("#555555"), null));

        Grid.SetRow(buttonStack, 4);
        stack.Children.Add(buttonStack);

        card.Content = stack;

        var root = new Grid();
        root.Children.Add(card);
        Content = root;
    }

    private Button BuildButton(string text, Color background, Color textColor, string? result)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = background,
            TextColor = textColor,
            CornerRadius = 8,
            HeightRequest = 46,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };
        button.Clicked += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"auto-award button clicked: {result ?? "Skip"}");
            _ = CloseAsync(result);
        };
        return button;
    }

    private async Task CloseAsync(string? result)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        System.Diagnostics.Debug.WriteLine($"auto-award close requested: {result ?? "null"}");

        try
        {
            System.Diagnostics.Debug.WriteLine("auto-award close before PopModalAsync");
            await Navigation.PopModalAsync(animated: false);
            System.Diagnostics.Debug.WriteLine("auto-award close after PopModalAsync");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Failed to close auto-award suggestion prompt: " + ex);
        }

        bool completed = _completion.TrySetResult(result);
        System.Diagnostics.Debug.WriteLine($"auto-award close TrySetResult: {completed}");
    }

    protected override void OnDisappearing()
    {
        System.Diagnostics.Debug.WriteLine("auto-award suggestion prompt disappearing");
        if (!_isClosing)
            _completion.TrySetResult(null);
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        System.Diagnostics.Debug.WriteLine("auto-award suggestion back button pressed");
        _ = CloseAsync(null);
        return true;
    }
}

internal class AutoAwardPostponePromptPage : ContentPage
{
    private readonly TaskCompletionSource<int?> _completion = new();
    private readonly string _activityName;
    private readonly int _initialThreshold;
    private Entry? _manualEntry;
    private bool _isClosing;

    private AutoAwardPostponePromptPage(string activityName, int initialThreshold)
    {
        _activityName = activityName;
        _initialThreshold = initialThreshold;
        BackgroundColor = Color.FromArgb("#80000000");
        BuildUI();
    }

    public static async Task<int?> ShowAsync(INavigation navigation, string activityName, int initialThreshold)
    {
        var page = new AutoAwardPostponePromptPage(activityName, initialThreshold);
        await navigation.PushModalAsync(page);
        return await page._completion.Task;
    }

    private void BuildUI()
    {
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 16,
            Padding = 0,
            HasShadow = true,
            WidthRequest = 420,
            MaximumWidthRequest = 520,
            MaximumHeightRequest = 680,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(20)
        };

        var stack = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        var titleLabel = new Label
        {
            Text = "Postpone Auto Suggestion",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            Padding = new Thickness(22, 20, 22, 8)
        };
        Grid.SetRow(titleLabel, 0);
        stack.Children.Add(titleLabel);

        var topDivider = new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        };
        Grid.SetRow(topDivider, 1);
        stack.Children.Add(topDivider);

        var contentStack = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(22, 18)
        };

        contentStack.Children.Add(new Label
        {
            Text = _activityName,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        contentStack.Children.Add(new Label
        {
            Text = "Postpone the suggestion. Choose how many more days from now before this activity is suggested again.",
            FontSize = 14,
            TextColor = Color.FromArgb("#555555"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var fixedGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        int[] thresholds = { 60, 90, 180, 365 };
        for (int i = 0; i < thresholds.Length; i++)
        {
            var button = BuildThresholdButton(thresholds[i]);
            Grid.SetColumn(button, i % 2);
            Grid.SetRow(button, i / 2);
            fixedGrid.Children.Add(button);
        }

        contentStack.Children.Add(fixedGrid);

        contentStack.Children.Add(new Label
        {
            Text = "Manual (days from now)",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            Margin = new Thickness(0, 8, 0, 0)
        });

        var manualRow = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        _manualEntry = new Entry
        {
            Text = _initialThreshold.ToString(),
            Keyboard = Keyboard.Numeric,
            Placeholder = "Threshold",
            BackgroundColor = Color.FromArgb("#F7F7F7")
        };
        Grid.SetColumn(_manualEntry, 0);
        manualRow.Children.Add(_manualEntry);

        var saveManual = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold
        };
        saveManual.Clicked += OnManualSaveClicked;
        Grid.SetColumn(saveManual, 1);
        manualRow.Children.Add(saveManual);

        contentStack.Children.Add(manualRow);

        var contentScroll = new ScrollView { Content = contentStack };
        Grid.SetRow(contentScroll, 2);
        stack.Children.Add(contentScroll);

        var bottomDivider = new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        };
        Grid.SetRow(bottomDivider, 3);
        stack.Children.Add(bottomDivider);

        var cancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            TextColor = Color.FromArgb("#555555"),
            CornerRadius = 8,
            HeightRequest = 46,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(22, 16, 22, 22)
        };
        cancelButton.Clicked += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("auto-award postpone cancel clicked");
            _ = CloseAsync(null);
        };
        Grid.SetRow(cancelButton, 4);
        stack.Children.Add(cancelButton);

        card.Content = stack;

        var root = new Grid();
        root.Children.Add(card);
        Content = root;
    }

    private Button BuildThresholdButton(int threshold)
    {
        var button = new Button
        {
            Text = threshold.ToString(),
            BackgroundColor = Color.FromArgb("#FBC02D"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };
        button.Clicked += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"auto-award postpone threshold clicked: {threshold}");
            _ = CloseAsync(threshold);
        };
        return button;
    }

    private async void OnManualSaveClicked(object? sender, EventArgs e)
    {
        if (_manualEntry == null ||
            !int.TryParse((_manualEntry.Text ?? "").Trim(), out int threshold) ||
            threshold < 1)
        {
            await DisplayAlert("Invalid", "Enter a whole number greater than 0.", "OK");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"auto-award postpone manual save clicked: {threshold}");
        await CloseAsync(threshold);
    }

    private async Task CloseAsync(int? result)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        System.Diagnostics.Debug.WriteLine($"auto-award postpone close requested: {result?.ToString() ?? "null"}");

        try
        {
            System.Diagnostics.Debug.WriteLine("auto-award postpone close before PopModalAsync");
            await Navigation.PopModalAsync(animated: false);
            System.Diagnostics.Debug.WriteLine("auto-award postpone close after PopModalAsync");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Failed to close auto-award postpone prompt: " + ex);
        }

        bool completed = _completion.TrySetResult(result);
        System.Diagnostics.Debug.WriteLine($"auto-award postpone close TrySetResult: {completed}");
    }

    protected override void OnDisappearing()
    {
        System.Diagnostics.Debug.WriteLine("auto-award postpone prompt disappearing");
        if (!_isClosing)
            _completion.TrySetResult(null);
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        System.Diagnostics.Debug.WriteLine("auto-award postpone back button pressed");
        _ = CloseAsync(null);
        return true;
    }
}
