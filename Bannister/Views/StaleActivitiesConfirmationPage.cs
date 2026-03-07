using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bannister.Views;

public class StaleActivitiesConfirmationPage : ContentPage
{
    private readonly List<Activity> _staleActivities;
    private readonly ActivityService _activityService;
    private readonly Dictionary<int, Switch> _activitySwitches;
    private readonly TaskCompletionSource<int> _tcs;

    public StaleActivitiesConfirmationPage(
        List<Activity> staleActivities,
        ActivityService activityService)
    {
        _staleActivities = staleActivities;
        _activityService = activityService;
        _activitySwitches = new Dictionary<int, Switch>();
        _tcs = new TaskCompletionSource<int>();

        Title = "Stale Activities";
        BackgroundColor = Color.FromArgb("#F5F7FC");

        BuildUI();
    }

    public Task<int> GetMovedCountAsync() => _tcs.Task;

    private void BuildUI()
    {
        // Main grid: top section (fixed) + scrollable activities
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }, // Header + buttons (fixed)
                new RowDefinition { Height = GridLength.Star }  // Scrollable activities
            },
            BackgroundColor = Color.FromArgb("#F5F7FC")
        };

        // ===== TOP SECTION (Fixed, no scroll) =====
        var topSection = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 15,
            BackgroundColor = Color.FromArgb("#F5F7FC")
        };

        // Header
        var lblTitle = new Label
        {
            Text = "📦 Stale Activities Found",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalOptions = LayoutOptions.Center
        };
        topSection.Children.Add(lblTitle);

        // Count display
        var lblCount = new Label
        {
            Text = $"{_staleActivities.Count} activities not used in 30+ days",
            FontSize = 16,
            TextColor = Color.FromArgb("#FF9800"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        topSection.Children.Add(lblCount);

        // Move Selected button
        var btnMove = new Button
        {
            Text = "📦 Move Selected to Stale",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 12,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 55,
            HorizontalOptions = LayoutOptions.Fill
        };
        btnMove.Clicked += OnMoveClicked;
        topSection.Children.Add(btnMove);

        // Skip button (smaller, secondary)
        var btnSkip = new Button
        {
            Text = "Keep All Active",
            BackgroundColor = Color.FromArgb("#999"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Fill
        };
        btnSkip.Clicked += OnSkipClicked;
        topSection.Children.Add(btnSkip);

        // Toggle all row
        var toggleAllStack = new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 10, 0, 0)
        };
        
        var lblToggle = new Label
        {
            Text = "Toggle which to move:",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        toggleAllStack.Children.Add(lblToggle);

        var btnSelectAll = new Button
        {
            Text = "Select All",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1976D2"),
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            CornerRadius = 6
        };
        btnSelectAll.Clicked += (s, e) => SetAllSwitches(true);
        toggleAllStack.Children.Add(btnSelectAll);

        var btnSelectNone = new Button
        {
            Text = "Select None",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            CornerRadius = 6
        };
        btnSelectNone.Clicked += (s, e) => SetAllSwitches(false);
        toggleAllStack.Children.Add(btnSelectNone);

        topSection.Children.Add(toggleAllStack);

        mainGrid.Add(topSection, 0, 0);

        // ===== SCROLLABLE ACTIVITIES LIST =====
        var scrollView = new ScrollView();
        var activityStack = new VerticalStackLayout
        {
            Padding = new Thickness(20, 10, 20, 20),
            Spacing = 10
        };

        foreach (var activity in _staleActivities)
        {
            activityStack.Children.Add(BuildActivityCard(activity));
        }

        scrollView.Content = activityStack;
        mainGrid.Add(scrollView, 0, 1);

        Content = mainGrid;
    }

    private Frame BuildActivityCard(Activity activity)
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#DDD"),
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        // Switch (on by default - will be moved unless toggled off)
        var toggle = new Switch
        {
            IsToggled = true,
            OnColor = Color.FromArgb("#FF9800"),
            ThumbColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        _activitySwitches[activity.Id] = toggle;
        grid.Add(toggle, 0, 0);

        // Activity info
        var infoStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center
        };

        infoStack.Children.Add(new Label
        {
            Text = activity.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Show start date info
        string dateText = activity.StartDate.HasValue 
            ? $"Created: {activity.StartDate.Value:MMM dd, yyyy}" 
            : "No start date";

        infoStack.Children.Add(new Label
        {
            Text = $"📁 {activity.Category} • {dateText}",
            FontSize = 12,
            TextColor = Color.FromArgb("#888")
        });

        grid.Add(infoStack, 1, 0);

        // EXP value
        var expLabel = new Label
        {
            Text = activity.ExpGain >= 0 ? $"+{activity.ExpGain}" : $"{activity.ExpGain}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = activity.ExpGain >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336"),
            VerticalOptions = LayoutOptions.Center
        };
        grid.Add(expLabel, 2, 0);

        frame.Content = grid;
        return frame;
    }

    private void SetAllSwitches(bool value)
    {
        foreach (var toggle in _activitySwitches.Values)
        {
            toggle.IsToggled = value;
        }
    }

    private async void OnMoveClicked(object? sender, EventArgs e)
    {
        var selectedActivities = _staleActivities
            .Where(a => _activitySwitches.ContainsKey(a.Id) && _activitySwitches[a.Id].IsToggled)
            .ToList();

        if (selectedActivities.Count == 0)
        {
            await DisplayAlert("None Selected", "No activities selected to move.", "OK");
            return;
        }

        foreach (var activity in selectedActivities)
        {
            activity.Category = "Stale";
            await _activityService.UpdateActivityAsync(activity);
        }

        await DisplayAlert(
            "Done",
            $"Moved {selectedActivities.Count} activity(ies) to 'Stale' category.\n\n" +
            "Use the 'Stale' filter to view them.",
            "OK");

        _tcs.TrySetResult(selectedActivities.Count);
        await Navigation.PopModalAsync();
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(0);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(0);
        return base.OnBackButtonPressed();
    }
}
