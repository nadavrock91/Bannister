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

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
