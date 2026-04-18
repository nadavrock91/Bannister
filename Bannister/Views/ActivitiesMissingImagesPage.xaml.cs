using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

public partial class ActivitiesMissingImagesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly string _gameId;
    private List<Activity> _missingImageActivities = new();

    public ActivitiesMissingImagesPage(AuthService auth, ActivityService activities, string gameId)
    {
        InitializeComponent();
        _auth = auth;
        _activities = activities;
        _gameId = gameId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadActivitiesAsync();
    }

    private async Task LoadActivitiesAsync()
    {
        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _gameId);

        // Filter activities without images
        _missingImageActivities = allActivities
            .Where(a => string.IsNullOrEmpty(a.ImagePath))
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Name)
            .ToList();

        activitiesList.ItemsSource = _missingImageActivities;
    }

    private async void OnEditSelected(object sender, EventArgs e)
    {
        var selected = activitiesList.SelectedItems?.Cast<Activity>().ToList();
        if (selected == null || selected.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select activities to edit.", "OK");
            return;
        }

        if (selected.Count > 1)
        {
            await DisplayAlert("Multiple Selection", "Please select only one activity to edit.", "OK");
            return;
        }

        var activity = selected[0];

        // ? FIXED: Actually open EditActivityPage
        var editPage = new EditActivityPage(_auth, _activities, _gameId, activity);
        await Navigation.PushModalAsync(editPage);

        // Refresh list after editing
        await LoadActivitiesAsync();
    }

    private async void OnDeleteSelected(object sender, EventArgs e)
    {
        var selected = activitiesList.SelectedItems?.Cast<Activity>().ToList();
        if (selected == null || selected.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select activities to delete.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Confirm Delete",
            $"Delete {selected.Count} activity(ies)?",
            "Delete",
            "Cancel"
        );

        if (!confirm) return;

        try
        {
            foreach (var activity in selected)
            {
                await _activities.DeleteActivityAsync(activity.Id);
            }

            await DisplayAlert("Success", $"Deleted {selected.Count} activity(ies).", "OK");
            await LoadActivitiesAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not delete activities: {ex.Message}", "OK");
        }
    }

    private async void OnClose(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}