using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace Bannister.Views;

public partial class ManageActivitiesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly string _gameId;
    
    private List<ActivityManageViewModel> _allActivities = new();
    private List<ActivityManageViewModel> _filteredActivities = new();
    private List<string> _categories = new();

    public ManageActivitiesPage(AuthService auth, ActivityService activities, string gameId)
    {
        InitializeComponent();
        _auth = auth;
        _activities = activities;
        _gameId = gameId;
        
        sortPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadActivitiesAsync();
    }

    private async Task LoadActivitiesAsync()
    {
        var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _gameId);
        
        _allActivities = activities
            .Select(a => new ActivityManageViewModel(a))
            .ToList();

        // Load categories for filter
        _categories = activities
            .Select(a => a.Category ?? "Misc")
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        
        _categories.Insert(0, "All Categories");
        categoryFilter.ItemsSource = _categories;
        categoryFilter.SelectedIndex = 0;

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        // Start with all activities
        _filteredActivities = new List<ActivityManageViewModel>(_allActivities);

        // Apply search filter
        string searchText = searchBar.Text?.Trim().ToLower() ?? "";
        if (!string.IsNullOrEmpty(searchText))
        {
            _filteredActivities = _filteredActivities
                .Where(a => a.Name.ToLower().Contains(searchText) ||
                           (a.Category?.ToLower().Contains(searchText) ?? false))
                .ToList();
        }

        // Apply category filter
        string selectedCategory = categoryFilter.SelectedItem?.ToString() ?? "All Categories";
        if (selectedCategory != "All Categories")
        {
            _filteredActivities = _filteredActivities
                .Where(a => a.Category == selectedCategory)
                .ToList();
        }

        // Apply sorting
        string sortOption = sortPicker.SelectedItem?.ToString() ?? "Name (A-Z)";
        _filteredActivities = sortOption switch
        {
            "Name (A-Z)" => _filteredActivities.OrderBy(a => a.Name).ToList(),
            "Name (Z-A)" => _filteredActivities.OrderByDescending(a => a.Name).ToList(),
            "EXP (High to Low)" => _filteredActivities.OrderByDescending(a => a.ExpGain).ToList(),
            "EXP (Low to High)" => _filteredActivities.OrderBy(a => a.ExpGain).ToList(),
            "Category" => _filteredActivities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToList(),
            _ => _filteredActivities.OrderBy(a => a.Name).ToList()
        };

        activitiesList.ItemsSource = _filteredActivities;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFiltersAndSort();
    }

    private void OnSearchPressed(object sender, EventArgs e)
    {
        ApplyFiltersAndSort();
    }

    private void OnCategoryFilterChanged(object sender, EventArgs e)
    {
        ApplyFiltersAndSort();
    }

    private void OnSortChanged(object sender, EventArgs e)
    {
        ApplyFiltersAndSort();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ActivityManageViewModel vm)
        {
            var editPage = new EditActivityPage(_auth, _activities, _gameId, vm.Activity);
            await Navigation.PushModalAsync(editPage);
            await LoadActivitiesAsync();
        }
    }

    private async void OnDuplicateClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ActivityManageViewModel vm)
        {
            bool confirm = await DisplayAlert(
                "Duplicate Activity",
                $"Create a copy of '{vm.Name}'?",
                "Yes",
                "No");

            if (!confirm) return;

            try
            {
                await _activities.CreateActivityAsync(
                    _auth.CurrentUsername,
                    _gameId,
                    $"{vm.Name} (Copy)",
                    vm.ExpGain,
                    vm.MeaningfulUntilLevel,
                    vm.Category,
                    vm.ImagePath
                );

                await DisplayAlert("Success", "Activity duplicated!", "OK");
                await LoadActivitiesAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not duplicate: {ex.Message}", "OK");
            }
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ActivityManageViewModel vm)
        {
            bool confirm = await DisplayAlert(
                "Delete Activity",
                $"Are you sure you want to delete '{vm.Name}'?\n\nThis cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirm) return;

            try
            {
                await _activities.BlankActivityAsync(vm.Activity.Id);
                await DisplayAlert("Success", "Activity deleted!", "OK");
                await LoadActivitiesAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not delete: {ex.Message}", "OK");
            }
        }
    }

    private async void OnBulkEditClicked(object sender, EventArgs e)
    {
        var selected = activitiesList.SelectedItems?.Cast<ActivityManageViewModel>().ToList();
        if (selected == null || selected.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select activities first.", "OK");
            return;
        }

        string action = await DisplayActionSheet(
            $"Bulk Edit ({selected.Count} activities)",
            "Cancel",
            null,
            "Change Category",
            "Change Multiplier",
            "Set Cutoff Level",
            "Mark as Inactive");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        try
        {
            if (action == "Change Category")
            {
                string newCategory = await DisplayPromptAsync(
                    "Change Category",
                    "Enter new category:",
                    placeholder: "e.g., Protein");

                if (string.IsNullOrWhiteSpace(newCategory)) return;

                foreach (var vm in selected)
                {
                    vm.Activity.Category = newCategory;
                    await _activities.UpdateActivityAsync(vm.Activity);
                }

                await DisplayAlert("Success", $"Updated {selected.Count} activities!", "OK");
            }
            else if (action == "Change Multiplier")
            {
                string input = await DisplayPromptAsync(
                    "Change Multiplier",
                    "Enter new multiplier (1-10):",
                    keyboard: Keyboard.Numeric);

                if (!int.TryParse(input, out int multiplier) || multiplier < 1 || multiplier > 10)
                {
                    await DisplayAlert("Invalid", "Please enter a number between 1 and 10", "OK");
                    return;
                }

                foreach (var vm in selected)
                {
                    vm.Activity.Multiplier = multiplier;
                    await _activities.UpdateActivityAsync(vm.Activity);
                }

                await DisplayAlert("Success", $"Updated {selected.Count} activities!", "OK");
            }
            else if (action == "Set Cutoff Level")
            {
                string input = await DisplayPromptAsync(
                    "Set Cutoff Level",
                    "Enter cutoff level (1-100):",
                    keyboard: Keyboard.Numeric);

                if (!int.TryParse(input, out int cutoff) || cutoff < 1 || cutoff > 100)
                {
                    await DisplayAlert("Invalid", "Please enter a number between 1 and 100", "OK");
                    return;
                }

                foreach (var vm in selected)
                {
                    vm.Activity.MeaningfulUntilLevel = cutoff;
                    vm.Activity.ExpGain = cutoff * 2;
                    await _activities.UpdateActivityAsync(vm.Activity);
                }

                await DisplayAlert("Success", $"Updated {selected.Count} activities!", "OK");
            }
            else if (action == "Mark as Inactive")
            {
                bool confirm = await DisplayAlert(
                    "Mark as Inactive",
                    $"Hide {selected.Count} activities from the game grid?",
                    "Yes",
                    "No");

                if (!confirm) return;

                foreach (var vm in selected)
                {
                    vm.Activity.IsActive = false;
                    await _activities.UpdateActivityAsync(vm.Activity);
                }

                await DisplayAlert("Success", $"Marked {selected.Count} activities as inactive!", "OK");
            }

            await LoadActivitiesAsync();
            activitiesList.SelectedItems?.Clear();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Bulk edit failed: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteSelectedClicked(object sender, EventArgs e)
    {
        var selected = activitiesList.SelectedItems?.Cast<ActivityManageViewModel>().ToList();
        if (selected == null || selected.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select activities to delete.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Delete Activities",
            $"Are you sure you want to delete {selected.Count} activities?\n\nThis cannot be undone.",
            "Delete All",
            "Cancel");

        if (!confirm) return;

        try
        {
            foreach (var vm in selected)
            {
                await _activities.BlankActivityAsync(vm.Activity.Id);
            }

            await DisplayAlert("Success", $"Deleted {selected.Count} activities!", "OK");
            await LoadActivitiesAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not delete activities: {ex.Message}", "OK");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Name,Category,ExpGain,MeaningfulUntilLevel,Multiplier,ImagePath");

            foreach (var activity in _allActivities)
            {
                csv.AppendLine($"\"{activity.Name}\",\"{activity.Category}\",{activity.ExpGain},{activity.MeaningfulUntilLevel},{activity.Multiplier},\"{activity.ImagePath}\"");
            }

            string filename = $"activities_{_gameId}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string filepath = Path.Combine(FileSystem.AppDataDirectory, filename);

            await File.WriteAllTextAsync(filepath, csv.ToString());

            await DisplayAlert("Exported", 
                $"Activities exported to:\n{filepath}\n\nYou can open this location from File Explorer.", 
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}

public class ActivityManageViewModel
{
    public Activity Activity { get; }

    public ActivityManageViewModel(Activity activity)
    {
        Activity = activity;
    }

    public int Id => Activity.Id;
    public string Name => Activity.Name;
    public string Category => Activity.Category ?? "Misc";
    public int ExpGain => Activity.ExpGain;
    public int MeaningfulUntilLevel => Activity.MeaningfulUntilLevel;
    public int Multiplier => Activity.Multiplier;
    public bool ShowMultiplier => Activity.Multiplier > 1;
    public string ImagePath => Activity.ImagePath ?? "";
}
