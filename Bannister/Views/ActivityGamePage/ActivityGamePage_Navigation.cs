using ConversationPractice.Services;
using ConversationPractice.Views;

namespace Bannister.Views;

/// <summary>
/// Partial class containing navigation and filter change event handlers
/// </summary>
public partial class ActivityGamePage
{
    // Filter and Category Events
    private void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (categoryPicker.SelectedIndex >= 0)
        {
            _currentCategoryIndex = categoryPicker.SelectedIndex;
            UpdateCategoryDisplay();
            _ = RefreshActivitiesAsync();
        }
    }

    private void OnPrevCategoryClicked(object? sender, EventArgs e)
    {
        if (_currentCategoryIndex > 0)
        {
            _currentCategoryIndex--;
            UpdateCategoryDisplay();
            _ = RefreshActivitiesAsync();
        }
    }

    private void OnNextCategoryClicked(object? sender, EventArgs e)
    {
        if (_currentCategoryIndex < _categories.Count - 1)
        {
            _currentCategoryIndex++;
            UpdateCategoryDisplay();
            _ = RefreshActivitiesAsync();
        }
    }

    private void OnMetaFilterChanged(object? sender, EventArgs e)
    {
        if (metaFilterPicker.SelectedIndex >= 0)
        {
            _currentMetaFilter = metaFilterPicker.SelectedItem?.ToString() ?? "All Activities";
            _ = RefreshActivitiesAsync();
        }
    }

    private void OnSortOrderChanged(object? sender, EventArgs e)
    {
        _ = RefreshActivitiesAsync();
    }

    // Selection Events
    private void OnClearSelectionClicked(object? sender, EventArgs e)
    {
        if (_allActivities != null)
        {
            foreach (var activity in _allActivities)
            {
                activity.IsSelected = false;
            }
        }
    }

    // Navigation Events
    private async void OnAddActivityClicked(object? sender, EventArgs e)
    {
        if (_game == null) return;
        await Shell.Current.GoToAsync($"addactivity?gameId={_game.GameId}");
    }

    private async void OnManageClicked(object? sender, EventArgs e)
    {
        if (_game == null) return;

        var managePage = new ManageActivitiesPage(_auth, _activities, _game.GameId);
        await Navigation.PushModalAsync(managePage);
        await RefreshActivitiesAsync();
    }

    private async void OnDefineDragonClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"setupgoal?gameId={_game!.GameId}");
    }

    private async void OnViewLogClicked(object? sender, EventArgs e)
    {
        if (_game == null) return;
        await Shell.Current.GoToAsync($"explog?gameId={_game.GameId}");
    }

    private async void OnConversationPracticeClicked(object? sender, EventArgs e)
    {
        var conversationService = Handler?.MauiContext?.Services
            .GetService<ConversationService>();
        
        if (conversationService != null)
        {
            await Navigation.PushAsync(
                new ConversationListPage(conversationService, _auth.CurrentUsername)
            );
        }
        else
        {
            await DisplayAlert("Error", "Conversation service not available", "OK");
        }
    }

    private async void OnOptionsClicked(object? sender, EventArgs e)
    {
        var options = new[]
        {
            "📊 Manual EXP Adjustment",
            "🔄 Reset Daily Checks",
            "📋 Export Data"
        };

        var result = await DisplayActionSheet("⚙️ Options", "Cancel", null, options);

        if (result == "📊 Manual EXP Adjustment")
        {
            await ShowManualExpAdjustmentAsync();
        }
        else if (result == "🔄 Reset Daily Checks")
        {
            await ResetDailyChecksAsync();
        }
        else if (result == "📋 Export Data")
        {
            await DisplayAlert("Export", "Export functionality coming soon!", "OK");
        }
    }

    private async Task ShowManualExpAdjustmentAsync()
    {
        if (_game == null) return;

        // First, ask if adding or removing EXP
        var action = await DisplayActionSheet(
            "Manual EXP Adjustment",
            "Cancel",
            null,
            "➕ Add EXP",
            "➖ Remove EXP");

        if (action == null || action == "Cancel") return;

        bool isAdding = action == "➕ Add EXP";

        // Get amount
        string amountStr = await DisplayPromptAsync(
            isAdding ? "Add EXP" : "Remove EXP",
            "Enter amount:",
            keyboard: Keyboard.Numeric,
            initialValue: "10");

        if (string.IsNullOrWhiteSpace(amountStr)) return;

        if (!int.TryParse(amountStr, out int amount) || amount <= 0)
        {
            await DisplayAlert("Invalid Amount", "Please enter a positive number.", "OK");
            return;
        }

        // Get reason
        string reason = await DisplayPromptAsync(
            "Reason",
            "Enter reason for adjustment:",
            initialValue: isAdding ? "Manual bonus" : "Manual correction");

        if (string.IsNullOrWhiteSpace(reason)) return;

        // Apply the adjustment
        int expChange = isAdding ? amount : -amount;
        string source = $"Manual: {reason}";

        await _exp.ApplyExpAsync(_auth.CurrentUsername, _game.GameId, source, expChange);

        await RefreshExpAsync();
        await LoadChartDataAsync();

        string emoji = isAdding ? "✅" : "⚠️";
        await DisplayAlert(
            $"{emoji} EXP Adjusted",
            $"{(isAdding ? "Added" : "Removed")} {amount} EXP\n\nReason: {reason}",
            "OK");
    }

    private async Task ResetDailyChecksAsync()
    {
        if (_game == null) return;

        bool confirm = await DisplayAlert(
            "Reset Daily Checks",
            "This will reset the following daily checks so they run again:\n\n" +
            "• Broken streak check\n" +
            "• Auto-award check\n" +
            "• Habit target check\n\n" +
            "Use this if something went wrong and you need to re-run the checks.\n\n" +
            "Continue?",
            "Reset",
            "Cancel");

        if (!confirm) return;

        // Clear the preference keys for this game
        string brokenStreakKey = $"BrokenStreakCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string autoAwardKey = $"AutoAward_LastCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string habitTargetKey = $"HabitTargetCheck_{_auth.CurrentUsername}_{_game.GameId}";

        Preferences.Remove(brokenStreakKey);
        Preferences.Remove(autoAwardKey);
        Preferences.Remove(habitTargetKey);

        await DisplayAlert(
            "Checks Reset",
            "Daily checks have been reset. They will run again when you reload the game.",
            "OK");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
