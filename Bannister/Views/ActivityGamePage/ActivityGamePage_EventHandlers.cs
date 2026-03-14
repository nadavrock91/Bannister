using Bannister.Helpers;
using Bannister.Models;
using Bannister.Services;
using Bannister.ViewModels;

namespace Bannister.Views;

/// <summary>
/// Partial class containing event handlers
/// </summary>
public partial class ActivityGamePage
{
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

        // Calculate total for alert
        int totalExp = 0;
        var details = new List<string>();

        foreach (var activityVM in selected)
        {
            int exp = activityVM.ExpGain * activityVM.EffectiveMultiplier;
            totalExp += exp;
            string sign = exp >= 0 ? "+" : "";
            
            // Show which multiplier was used
            string multiplierInfo = activityVM.TemporaryMultiplier > 1 
                ? $" (x{activityVM.TemporaryMultiplier} times)" 
                : activityVM.Multiplier > 1 
                    ? $" (x{activityVM.Multiplier})" 
                    : "";
            
            details.Add($"{activityVM.Name}{multiplierInfo}: {sign}{exp}");
        }

        // Log each activity individually (repeated per effective multiplier)
        foreach (var activityVM in selected)
        {
            int baseExp = activityVM.ExpGain;
            int effectiveMultiplier = activityVM.EffectiveMultiplier;

            for (int i = 0; i < effectiveMultiplier; i++)
            {
                await _exp.ApplyExpAsync(_auth.CurrentUsername, _game!.GameId, activityVM.Name, baseExp, activityVM.Id);
            }

            // Record streak usage if this activity is streak-tracked
            if (activityVM.Activity.IsStreakTracked)
            {
                await _streaks.RecordActivityUsageAsync(_auth.CurrentUsername, _game!.GameId, activityVM.Id, activityVM.Name);
            }
            
            // Record habit completion if this activity has habit tracking
            if (activityVM.Activity.HabitType != "None")
            {
                await _activities.RecordHabitCompletionAsync(activityVM.Activity);
            }
            
            // Record display day streak and check for bonus
            await _activities.RecordDisplayDayStreakAsync(activityVM.Activity);
            activityVM.DisplayDayStreak = activityVM.Activity.DisplayDayStreak;
            
            // Increment times completed
            activityVM.Activity.TimesCompleted++;
            await _activities.UpdateActivityAsync(activityVM.Activity);
            activityVM.TimesCompleted = activityVM.Activity.TimesCompleted;
            
            // Update NewHabit progress if this activity is linked to a NewHabit
            await RecordNewHabitProgressAsync(activityVM.Id);
            
            // Check for streak milestone bonus (only for positive EXP activities)
            // Negative activities should not get reduced punishment for streaks
            if (activityVM.ExpGain > 0)
            {
                int streakBonus = ActivityService.CalculateStreakBonus(activityVM.Activity.DisplayDayStreak);
                if (streakBonus > 0)
                {
                    await _exp.ApplyExpAsync(_auth.CurrentUsername, _game!.GameId, $"{activityVM.Name} (Streak Bonus)", streakBonus, activityVM.Id);
                    totalExp += streakBonus;
                    details.Add($"🔥 {activityVM.Name} streak bonus ({activityVM.Activity.DisplayDayStreak} days): +{streakBonus}");
                }
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
                activityVM.TemporaryMultiplier = 1; // Reset temporary multiplier
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
            activityVM.TemporaryMultiplier = 1; // Reset temporary multiplier after calculation
        }

        string sign2 = totalExp >= 0 ? "+" : "";
        string levelUpMsg = leveledUp ? $"\n\n🎉 LEVEL UP! Now Level {levelAfter}!" : "";
        await DisplayAlert("EXP Applied", $"Total: {sign2}{totalExp} EXP{levelUpMsg}\n\n{string.Join("\n", details)}", "OK");
    }

    private async Task ShowContextMenu(ActivityGameViewModel activityVM)
    {
        string notesOption = activityVM.HasNotes 
            ? $"📝 Edit Notes" 
            : "📝 Add Notes";
            
        string action = await DisplayActionSheet(
            activityVM.Name,
            "Cancel",
            null,
            "Edit Activity",
            $"Set Multiplier (current: x{activityVM.Multiplier})",
            "Applied X Times (one-time)",
            "Update Habit Streak",
            $"Times Completed: {activityVM.Activity.TimesCompleted}",
            notesOption,
            "Duplicate as Negative",
            "Set Auto-Award",
            "⏸️ Disable Activity",
            "🗑️ Remove Activity"
        );

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action == "Edit Activity")
        {
            await EditActivity(activityVM);
        }
        else if (action.StartsWith("Set Multiplier"))
        {
            await SetMultiplier(activityVM);
        }
        else if (action.StartsWith("Applied X Times"))
        {
            await SetTemporaryMultiplier(activityVM);
        }
        else if (action == "Update Habit Streak")
        {
            await UpdateHabitStreak(activityVM);
        }
        else if (action.StartsWith("Times Completed"))
        {
            await EditTimesCompleted(activityVM);
        }
        else if (action.Contains("Notes"))
        {
            await EditNotes(activityVM);
        }
        else if (action == "Duplicate as Negative")
        {
            await DuplicateAsNegative(activityVM);
        }
        else if (action == "Set Auto-Award")
        {
            await SetAutoAward(activityVM);
        }
        else if (action == "⏸️ Disable Activity")
        {
            await DisableActivity(activityVM);
        }
        else if (action == "🗑️ Remove Activity")
        {
            await RemoveActivity(activityVM);
        }
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
        
        // Double confirm for destructive action
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
            // Don't reload - just update the UI element
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
                
                // Auto-select the activity for convenience
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
        // Navigate to activity creation page with prefilled negative values
        // Pass data via query parameters
        string negativeName = $"{activityVM.Name} (Negative)";
        string encodedName = Uri.EscapeDataString(negativeName);
        string encodedImage = Uri.EscapeDataString(activityVM.ImagePath ?? "");
        string encodedCategory = Uri.EscapeDataString("Negative"); // Always use "Negative" category
        
        // Build query string with optional dates
        var queryParams = $"addactivity?gameId={_game!.GameId}&prefillName={encodedName}&prefillLevel=-500&prefillImage={encodedImage}&prefillCategory={encodedCategory}&isNegative=true&noHabitTarget=true";
        
        // Add start date if original has one
        if (activityVM.Activity.StartDate.HasValue)
        {
            queryParams += $"&prefillStartDate={activityVM.Activity.StartDate.Value:o}";
        }
        
        // Add end date if original has one
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

        if (result == null) return; // Cancelled

        activity.Notes = result.Trim();
        await _activities.UpdateActivityAsync(activity);
        
        // Update the ViewModel to trigger property changes
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

    private async Task RecordNewHabitProgressAsync(int activityId)
    {
        try
        {
            var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
            if (dbService == null) return;

            var conn = await dbService.GetConnectionAsync();
            
            // Find any NewHabit linked to this activity
            var habit = await conn.Table<NewHabit>()
                .Where(h => h.Username == _auth.CurrentUsername 
                    && h.PositiveActivityId == activityId 
                    && h.Status == "active")
                .FirstOrDefaultAsync();

            if (habit == null) return;

            var today = DateTime.UtcNow.Date;

            // Already recorded today?
            if (habit.LastAppliedDate.HasValue && habit.LastAppliedDate.Value.Date == today)
            {
                System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Already recorded today for {habit.HabitName}");
                return;
            }

            // Check if streak continues (must be consecutive days)
            bool isConsecutive = habit.LastAppliedDate.HasValue 
                && (today - habit.LastAppliedDate.Value.Date).TotalDays == 1;

            if (isConsecutive || habit.ConsecutiveDays == 0)
            {
                habit.ConsecutiveDays++;
            }
            else
            {
                // Streak broken - reset
                habit.ConsecutiveDays = 1;
            }

            habit.LastAppliedDate = today;

            // Check for graduation (7 days)
            if (habit.ConsecutiveDays >= habit.DaysToGraduate)
            {
                habit.Status = "graduated";
                habit.CompletedAt = DateTime.UtcNow;
                
                // Update allowance
                var allowance = await conn.Table<HabitAllowance>()
                    .Where(a => a.Username == _auth.CurrentUsername)
                    .FirstOrDefaultAsync();
                
                if (allowance != null)
                {
                    allowance.CurrentAllowance++;
                    allowance.TotalGraduated++;
                    if (allowance.CurrentAllowance > allowance.HighestAllowance)
                    {
                        allowance.HighestAllowance = allowance.CurrentAllowance;
                    }
                    await conn.UpdateAsync(allowance);
                }

                await DisplayAlert("🎉 Habit Graduated!", 
                    $"'{habit.HabitName}' has graduated!\n\nYou now have +1 allowance slot.", "OK");
            }

            await conn.UpdateAsync(habit);
            System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Recorded day {habit.ConsecutiveDays} for {habit.HabitName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Error recording progress: {ex.Message}");
        }
    }
}
