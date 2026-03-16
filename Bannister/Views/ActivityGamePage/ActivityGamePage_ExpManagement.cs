using Bannister.Helpers;

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

        string autoAwardKey = $"AutoAward_LastCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string lastCheckDate = Preferences.Get(autoAwardKey, "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        if (lastCheckDate == today)
            return;

        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);
        var autoAwardActivities = allActivities.Where(a => a.IsAutoAward).ToList();

        if (autoAwardActivities.Count == 0)
            return;

        var eligibleActivities = new List<Models.Activity>();
        var now = DateTime.Now;

        foreach (var activity in autoAwardActivities)
        {
            bool isEligible = false;

            if (activity.AutoAwardFrequency == "Daily")
            {
                if (!activity.LastAutoAwarded.HasValue || 
                    activity.LastAutoAwarded.Value.Date < now.Date)
                {
                    isEligible = true;
                }
            }
            else if (activity.AutoAwardFrequency == "Weekly")
            {
                var selectedDays = activity.AutoAwardDays.Split(',', StringSplitOptions.RemoveEmptyEntries);
                string todayName = now.DayOfWeek.ToString();

                if (selectedDays.Contains(todayName))
                {
                    if (!activity.LastAutoAwarded.HasValue || 
                        activity.LastAutoAwarded.Value.Date < now.Date)
                    {
                        isEligible = true;
                    }
                }
            }
            else if (activity.AutoAwardFrequency == "Monthly")
            {
                if (now.Day == 1)
                {
                    if (!activity.LastAutoAwarded.HasValue || 
                        activity.LastAutoAwarded.Value.Month < now.Month ||
                        activity.LastAutoAwarded.Value.Year < now.Year)
                    {
                        isEligible = true;
                    }
                }
            }

            if (isEligible)
            {
                eligibleActivities.Add(activity);
            }
        }

        if (eligibleActivities.Count > 0)
        {
            Preferences.Set(autoAwardKey, today);

            // Get current level for PercentOfLevel calculations
            var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);

            var confirmPage = new AutoAwardConfirmationPage(
                eligibleActivities,
                _exp,
                _activities,
                _auth.CurrentUsername,
                _game.GameId,
                currentLevel
            );
            await Navigation.PushModalAsync(confirmPage);
            
            // Wait for the modal to complete before continuing
            await confirmPage.WaitForCompletionAsync();

            await RefreshExpAsync();
            await RefreshActivitiesAsync();
        }
    }
}
