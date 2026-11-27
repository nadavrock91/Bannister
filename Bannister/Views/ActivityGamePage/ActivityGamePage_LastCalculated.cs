using Bannister.Models;

using Bannister.Services;

namespace Bannister.Views;

public partial class ActivityGamePage
{
    private string? _targetNotice;

    private async Task SaveLastCalculatedAsync(Activity activity)
    {
        string gameId = _isGroupingMode ? activity.Game : _game?.GameId ?? activity.Game;
        await _lastCalculated.SaveAsync(
            _auth.CurrentUsername,
            gameId,
            activity.Category ?? "Misc",
            activity.Id);
        await UpdateLastCalculatedButtonAsync();
    }

    private async Task UpdateLastCalculatedButtonAsync()
    {
        var location = await _lastCalculated.GetAsync(_auth.CurrentUsername);
        if (location == null) { btnLastCalculated.IsVisible = false; return; }
        var game = await _games.GetGameAsync(_auth.CurrentUsername, location.GameId);
        btnLastCalculated.Text = $"↩ Last: {game?.DisplayName ?? location.GameId} / {location.Category}";
        btnLastCalculated.IsVisible = true;
    }

    private async Task GoToLastCalculatedAsync()
    {
        var location = await _lastCalculated.GetAsync(_auth.CurrentUsername);
        if (location == null) { btnLastCalculated.IsVisible = false; return; }
        var game = await _games.GetGameAsync(_auth.CurrentUsername, location.GameId);
        if (game == null)
        {
            await DisplayAlert("Last calculated", "The saved game no longer exists.", "OK");
            return;
        }

        if (_game == null || !string.Equals(_game.GameId, location.GameId, StringComparison.OrdinalIgnoreCase))
        {
            string route = $"activitygrid?gameId={Uri.EscapeDataString(location.GameId)}" +
                $"&targetCategory={Uri.EscapeDataString(location.Category)}&targetActivityId={location.ActivityId}";
            await Shell.Current.GoToAsync(route);
            return;
        }

        _targetCategory = location.Category;
        _targetActivityId = location.ActivityId;
        _targetMessageShown = false;
        await PrepareTargetLocationAsync();
        await RefreshActivitiesAsync();
    }

    private async Task PrepareTargetLocationAsync()
    {
        if (_game == null || (string.IsNullOrWhiteSpace(_targetCategory) && _targetActivityId <= 0)) return;
        var all = await GetCurrentActivitiesAsync();
        var activity = _targetActivityId > 0 ? all.FirstOrDefault(x => x.Id == _targetActivityId) : null;
        string category = _targetCategory;

        if (activity == null && _targetActivityId > 0)
            _targetNotice = "The saved activity no longer exists. Showing its last category instead.";
        else if (activity != null && !string.Equals(activity.Category, category, StringComparison.OrdinalIgnoreCase))
        {
            category = activity.Category ?? category;
            _targetNotice = "The activity moved to another category. Showing its current location.";
        }

        int categoryIndex = _categories.FindIndex(x => string.Equals(x, category, StringComparison.OrdinalIgnoreCase));
        if (categoryIndex < 0)
        {
            _targetNotice = "The saved category no longer exists. Showing the game instead.";
            _targetCategory = "";
            return;
        }

        _targetCategory = _categories[categoryIndex];
        _showAllActivities = true;
        if (btnShowAll != null)
        {
            btnShowAll.Text = "👁️ Showing All Days";
            btnShowAll.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground);
            btnShowAll.ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        }
        int navigableIndex = _navigableCategories.FindIndex(x =>
            string.Equals(x, _targetCategory, StringComparison.OrdinalIgnoreCase));
        if (navigableIndex >= 0)
        {
            _tempNonNavigableCategory = null;
            _currentCategoryIndex = navigableIndex;
        }
        else
        {
            _tempNonNavigableCategory = _targetCategory;
            _currentCategoryIndex = -1;
        }

        categoryPicker.SelectedIndexChanged -= OnCategoryChanged;
        categoryPicker.SelectedIndex = categoryIndex;
        categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        UpdateCategoryDisplay();
    }

    private async Task RevealTargetActivityAsync()
    {
        if (_targetMessageShown || _targetActivityId <= 0) return;
        _targetMessageShown = true;
        await Task.Delay(100);
        if (_activityCards.TryGetValue(_targetActivityId, out var card))
        {
            await activitiesCollection.ScrollToAsync(card, ScrollToPosition.Center, true);
            Color oldBorder = card.BorderColor;
            card.ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningAccentText);
            await Task.Delay(1600);
            card.BorderColor = oldBorder;
        }
        else if (string.IsNullOrWhiteSpace(_targetNotice))
        {
            _targetNotice = "The saved activity is not currently visible. Showing its category.";
        }

        if (!string.IsNullOrWhiteSpace(_targetNotice))
            await DisplayAlert("Last calculated", _targetNotice, "OK");
        _targetNotice = null;
        _targetActivityId = 0;
        _targetCategory = "";
    }
}
