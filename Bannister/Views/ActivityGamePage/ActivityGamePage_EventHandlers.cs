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

            // Apply EXP multiple times if multiplier > 1 (just the EXP logging)
            for (int i = 0; i < effectiveMultiplier; i++)
            {
                await _exp.ApplyExpAsync(_auth.CurrentUsername, _game!.GameId, activityVM.Name, baseExp, activityVM.Id);
            }

            // Process completion ONCE per activity using shared logic
            // This handles: streak tracking, habit completion, display day streak,
            // times completed, NewHabit progress, and streak bonus
            var (bonusExp, bonusDetails) = await ProcessActivityCompletionCoreAsync(activityVM.Activity);
            
            totalExp += expForThisActivity + bonusExp;

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
            details.Add($"{activityVM.Name}{multiplierInfo}: {sign}{expForThisActivity}");
            
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
        
        string action = await DisplayActionSheet(
            "Update Streak",
            "Cancel",
            null,
            $"Habit Streak: {activity.HabitStreak}",
            $"Display Day Streak: {activity.DisplayDayStreak}"
        );

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action.StartsWith("Habit Streak"))
        {
            string result = await DisplayPromptAsync(
                "Habit Streak",
                $"For 7-day graduation.\nCurrent: {activity.HabitStreak}",
                initialValue: activity.HabitStreak.ToString(),
                keyboard: Keyboard.Numeric);

            if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int newStreak) && newStreak >= 0)
            {
                activity.HabitStreak = newStreak;
                await _activities.UpdateActivityAsync(activity);
                activityVM.UpdateActivity(activity);
                await DisplayAlert("Updated", $"Habit streak: {newStreak}", "OK");
            }
        }
        else if (action.StartsWith("Display Day Streak"))
        {
            string result = await DisplayPromptAsync(
                "Display Day Streak",
                $"Scheduled days bonus.\nCurrent: {activity.DisplayDayStreak}",
                initialValue: activity.DisplayDayStreak.ToString(),
                keyboard: Keyboard.Numeric);

            if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int newStreak) && newStreak >= 0)
            {
                activity.DisplayDayStreak = newStreak;
                if (newStreak > 0)
                {
                    activity.LastDisplayDayUsed = DateTime.UtcNow.Date;
                }
                await _activities.UpdateActivityAsync(activity);
                activityVM.DisplayDayStreak = newStreak;
                await DisplayAlert("Updated", $"Display day streak: {newStreak}", "OK");
            }
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
        if (_allActivities == null) return;
        
        foreach (var activity in _allActivities)
        {
            activity.IsSelected = true;
        }
    }

    private async void OnOptionsClicked(object? sender, EventArgs e)
    {
        var result = await DisplayActionSheet(
            "⚙️ Options",
            "Cancel",
            null,
            "📊 View Activity Log",
            "🔄 Refresh",
            "✏️ Manual EXP Adjustment",
            "📁 Change Category (All on Page)",
            "💡 Set All as Possible (All on Page)",
            "📤 Export Data");

        if (result == null || result == "Cancel") return;

        if (result == "📊 View Activity Log")
        {
            OnViewLogClicked(sender, e);
        }
        else if (result == "🔄 Refresh")
        {
            await RefreshActivitiesAsync();
        }
        else if (result == "✏️ Manual EXP Adjustment")
        {
            await ManualExpAdjustmentAsync();
        }
        else if (result == "📁 Change Category (All on Page)")
        {
            await BulkChangeCategoryAsync();
        }
        else if (result == "💡 Set All as Possible (All on Page)")
        {
            await BulkSetAsPossibleAsync();
        }
        else if (result == "📤 Export Data")
        {
            await DisplayAlert("Export", "Export functionality coming soon.", "OK");
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
