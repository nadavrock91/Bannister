using Bannister.Helpers;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Partial class containing EXP management and animation methods
/// </summary>
public partial class ActivityGamePage
{
    private async Task RefreshExpAsync()
    {
        if (_game == null) return;

        int totalExp = await _exp.GetTotalExpAsync(_auth.CurrentUsername, _game.GameId);

        // Handle negative EXP
        if (totalExp < 0)
        {
            await Dispatcher.DispatchAsync(() =>
            {
                if (lblCurrentLevel != null)
                {
                    lblCurrentLevel.Text = "Negative EXP";
                    lblCurrentLevel.TextColor = Color.FromArgb("#EE5B5B");
                }
                if (lblExpToNext != null)
                    lblExpToNext.Text = $"{totalExp} EXP (below zero)";
                if (lblExpTotal != null)
                    lblExpTotal.Text = $"Total EXP: {totalExp:N0}";
                if (expProgressBar != null)
                {
                    expProgressBar.Progress = 0;
                    expProgressBar.ProgressColor = Color.FromArgb("#EE5B5B");
                }
            });

            _currentLevel = 0;
            return;
        }

        // Normal positive EXP handling
        var (level, expInto, expNeeded) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);
        _currentLevel = level;

        await Dispatcher.DispatchAsync(() =>
        {
            if (lblCurrentLevel != null)
            {
                lblCurrentLevel.Text = $"Level {level}";
                lblCurrentLevel.TextColor = Color.FromArgb("#5B63EE");
            }
            if (lblExpToNext != null)
            {
                lblExpToNext.Text = $"{expInto} / {expNeeded} EXP";
            }
            if (lblExpTotal != null)
            {
                lblExpTotal.Text = $"Total EXP: {totalExp:N0}";
            }
            if (expProgressBar != null)
            {
                double progress = expNeeded > 0 ? (double)expInto / expNeeded : 0;
                expProgressBar.Progress = progress;
                expProgressBar.ProgressColor = Color.FromArgb("#4CAF50");
            }
        });

        if (level >= 100)
        {
            bool isSlain = await _dragons.IsDragonSlainAsync(_auth.CurrentUsername, _game.GameId);
            if (!isSlain)
            {
                await _dragons.SlayDragonAsync(_auth.CurrentUsername, _game.GameId);

                var dragon = await _dragons.GetDragonAsync(_auth.CurrentUsername, _game.GameId);
                if (dragon != null)
                {
                    await _attempts.MarkAttemptSuccessfulAsync(
                        _auth.CurrentUsername,
                        _game.GameId,
                        dragon.Title);
                }

                await DisplayAlert("🎉 Victory!",
                    $"You have slain the dragon!\n\nYou reached Level {level}!",
                    "Awesome!");

                await LoadDragonAsync();
            }
        }
    }

    private async Task AnimateExpBar(double fromProgress, double toProgress, bool leveledUp)
    {
        try
        {
            if (leveledUp)
            {
                await expProgressBar.ProgressTo(1.0, 500, Easing.CubicOut);
                await SoundHelper.PlayLevelUpSound();
                await Task.Delay(200);
                expProgressBar.Progress = 0;
                await expProgressBar.ProgressTo(toProgress, 300, Easing.CubicOut);
            }
            else
            {
                await expProgressBar.ProgressTo(toProgress, 500, Easing.CubicOut);
                await SoundHelper.PlayExpGainSound();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
        }
    }

    private async Task CheckAutoAwardActivitiesAsync()
    {
        if (_game == null) return;

        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);
        var autoAwardActivities = allActivities
            .Where(IsEligibleAutoAwardActivity)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (autoAwardActivities.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO-AWARD] No auto-award activities found");
            return;
        }

        var today = DateTime.Today;
        var pendingDayCounts = new Dictionary<int, int>();
        var pendingActivities = new List<Models.Activity>();

        foreach (var activity in autoAwardActivities)
        {
            if (activity.LastAutoAwarded.HasValue && activity.LastAutoAwarded.Value.Date >= today)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTO-AWARD] {activity.Name} already awarded today");
                continue;
            }

            var startDate = GetAutoAwardStartDate(activity, today);
            if (startDate > today)
                continue;

            pendingActivities.Add(activity);
            pendingDayCounts[activity.Id] = (today - startDate).Days + 1;
        }

        if (pendingActivities.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO-AWARD] No pending auto-awards");
            return;
        }

        var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);
        var confirmPage = new AutoAwardConfirmationPage(
            pendingActivities,
            _exp,
            _activities,
            _auth.CurrentUsername,
            _game.GameId,
            currentLevel,
            pendingDayCounts);

        await Navigation.PushModalAsync(confirmPage);
        bool confirmed = await confirmPage.WaitForCompletionAsync();
        if (!confirmed)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO-AWARD] User skipped pending auto-awards");
            return;
        }

        var (levelBefore, expIntoBefore, expNeededBefore) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);
        double progressBefore = expNeededBefore > 0 ? (double)expIntoBefore / expNeededBefore : 0;
        int totalAwards = 0;
        int totalExpAwarded = 0;
        var bonusDetails = new List<string>();
        var awardedActivityIds = new HashSet<int>();

        foreach (var activity in confirmPage.SelectedActivities)
        {
            var startDate = GetAutoAwardStartDate(activity, today);

            if (startDate > today)
                continue;

            foreach (var day in EachAutoAwardDay(startDate, today))
            {
                DateTime targetTime = day.Date == today.Date
                    ? DateTime.Now
                    : DateTime.SpecifyKind(day.Date.AddHours(12), DateTimeKind.Local);

                var awardResult = await ApplyAutoAwardActivityAsync(activity, targetTime);
                totalExpAwarded += awardResult.TotalExp;
                bonusDetails.AddRange(awardResult.BonusDetails);
                totalAwards++;
                awardedActivityIds.Add(activity.Id);
            }

            activity.LastAutoAwarded = today;
            await _activities.UpdateActivityAsync(activity);
        }

        if (totalAwards > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO-AWARD] Applied {totalAwards} retroactive auto-award(s)");
            string bonusMessage = bonusDetails.Count > 0
                ? $"\n\n{string.Join("\n", bonusDetails)}"
                : "";

            await DisplayAlert(
                "EXP Awarded!",
                $"Awarded {totalExpAwarded} total EXP from {awardedActivityIds.Count} auto-award activit{(awardedActivityIds.Count == 1 ? "y" : "ies")} across {totalAwards} day{(totalAwards == 1 ? "" : "s")}.{bonusMessage}",
                "OK");

            var (levelAfter, expIntoAfter, expNeededAfter) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);
            double progressAfter = expNeededAfter > 0 ? (double)expIntoAfter / expNeededAfter : 0;
            bool leveledUp = levelAfter > levelBefore;

            if (lblCurrentLevel != null)
                lblCurrentLevel.Text = $"Level {levelAfter}";
            if (lblExpToNext != null)
                lblExpToNext.Text = $"{expIntoAfter} / {expNeededAfter} EXP";
            if (lblExpTotal != null)
            {
                int totalExp = await _exp.GetTotalExpAsync(_auth.CurrentUsername, _game.GameId);
                lblExpTotal.Text = $"Total EXP: {totalExp:N0}";
            }
            if (expProgressBar != null)
                expProgressBar.ProgressColor = Color.FromArgb("#4CAF50");

            await AnimateExpBar(progressBefore, progressAfter, leveledUp);
            await RefreshExpAsync();
            await RefreshActivitiesAsync();
        }
    }

    private static DateTime GetAutoAwardStartDate(Models.Activity activity, DateTime today)
    {
        return activity.LastAutoAwarded.HasValue
            ? activity.LastAutoAwarded.Value.Date.AddDays(1)
            : today;
    }

    private static bool IsEligibleAutoAwardActivity(Models.Activity activity)
    {
        if (!activity.IsAutoAward || !activity.IsActive || activity.IsPossible)
            return false;

        if (activity.EndDate.HasValue && activity.EndDate.Value < DateTime.Now)
            return false;

        return !string.Equals(activity.Category, "Expired", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(activity.Category, "Stale", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(int TotalExp, List<string> BonusDetails)> ApplyAutoAwardActivityAsync(Models.Activity activity, DateTime loggedAt)
    {
        string gameId = activity.Game;
        var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, gameId);
        int expAmount = CalculateAutoAwardExpGain(activity, currentLevel) * Math.Max(1, activity.Multiplier);
        int totalExp = expAmount;
        var bonusDetails = new List<string>();

        await _exp.ApplyExpAsync(
            _auth.CurrentUsername,
            gameId,
            $"{activity.Name} (Auto Award)",
            expAmount,
            activity.Id,
            loggedAt);

        if (activity.IsStreakTracked)
        {
            await _streaks.RecordActivityUsageAsync(
                _auth.CurrentUsername,
                gameId,
                activity.Id,
                activity.Name,
                activity,
                loggedAt);
        }

        if (activity.HabitType != "None")
            await _activities.RecordHabitCompletionAsync(activity, loggedAt);

        await _activities.RecordDisplayDayStreakAsync(activity, loggedAt);

        activity.TimesCompleted++;
        await _activities.UpdateActivityAsync(activity);

        var newHabits = Application.Current?.Handler?.MauiContext?.Services.GetService<NewHabitService>();
        if (newHabits != null)
            await newHabits.RecordHabitDoneAsync(activity.Id, loggedAt);

        if (activity.ExpGain > 0)
        {
            int streakBonus = ActivityService.CalculateStreakBonus(activity.DisplayDayStreak);
            if (streakBonus > 0)
            {
                await _exp.ApplyExpAsync(
                    _auth.CurrentUsername,
                    gameId,
                    $"{activity.Name} (Auto Award Streak Bonus)",
                    streakBonus,
                    activity.Id,
                    loggedAt);
                totalExp += streakBonus;
                bonusDetails.Add($"🔥 {activity.Name} streak bonus ({activity.DisplayDayStreak} days): +{streakBonus}");
            }
        }

        return (totalExp, bonusDetails);
    }

    private static int CalculateAutoAwardExpGain(Models.Activity activity, int currentLevel)
    {
        if (activity.RewardType == "PercentOfLevel")
            return ExpEngine.ExpForPercentOfLevel(currentLevel, activity.PercentOfLevel, activity.PercentCutoffLevel);

        return activity.ExpGain;
    }

    private static IEnumerable<DateTime> EachAutoAwardDay(DateTime start, DateTime end)
    {
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            yield return day;
    }
}
