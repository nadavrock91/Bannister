using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bannister.Views;

public class AutoAwardConfirmationPage : ContentPage
{
    private readonly List<Activity> _eligibleActivities;
    private readonly ExpService _expService;
    private readonly ActivityService _activityService;
    private readonly string _username;
    private readonly string _gameId;
    private readonly int _currentLevel;
    
    private Dictionary<int, CheckBox> _activityCheckBoxes;
    private Label _lblTotal;
    private int _totalExp = 0;

    public AutoAwardConfirmationPage(
        List<Activity> eligibleActivities,
        ExpService expService,
        ActivityService activityService,
        string username,
        string gameId,
        int currentLevel)
    {
        _eligibleActivities = eligibleActivities;
        _expService = expService;
        _activityService = activityService;
        _username = username;
        _gameId = gameId;
        _currentLevel = currentLevel;
        _activityCheckBoxes = new Dictionary<int, CheckBox>();

        Title = "Auto-Award Activities";
        BackgroundColor = Color.FromArgb("#F5F7FC");

        BuildUI();
        CalculateTotal();
    }

    /// <summary>
    /// Calculate the actual EXP for an activity, handling PercentOfLevel type
    /// </summary>
    private int GetActivityExp(Activity activity)
    {
        if (activity.RewardType == "PercentOfLevel")
        {
            return ExpEngine.ExpForPercentOfLevel(_currentLevel, activity.PercentOfLevel, activity.PercentCutoffLevel);
        }
        return activity.ExpGain;
    }

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
            Text = "🎁 Auto-Award Activities",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalOptions = LayoutOptions.Center
        };
        topSection.Children.Add(lblTitle);

        // Total EXP display (plain text, not button-like)
        _lblTotal = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4CAF50"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 5, 0, 10)
        };
        topSection.Children.Add(_lblTotal);

        // Big Award button
        var btnAward = new Button
        {
            Text = "✓ Award EXP",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 12,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 60,
            HorizontalOptions = LayoutOptions.Fill
        };
        btnAward.Clicked += OnAwardClicked;
        topSection.Children.Add(btnAward);

        // Skip button (smaller, secondary)
        var btnSkip = new Button
        {
            Text = "Skip All",
            BackgroundColor = Color.FromArgb("#999"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Fill
        };
        btnSkip.Clicked += OnSkipClicked;
        topSection.Children.Add(btnSkip);

        // Subtitle
        var lblSubtitle = new Label
        {
            Text = "Uncheck any you don't want to award:",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 10, 0, 0)
        };
        topSection.Children.Add(lblSubtitle);

        mainGrid.Add(topSection, 0, 0);

        // ===== SCROLLABLE ACTIVITIES LIST =====
        var scrollView = new ScrollView();
        var activityStack = new VerticalStackLayout
        {
            Padding = new Thickness(20, 10, 20, 20),
            Spacing = 10
        };

        foreach (var activity in _eligibleActivities)
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
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#DDD")
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };

        // Checkbox
        var checkbox = new CheckBox
        {
            IsChecked = true,
            Color = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.CheckedChanged += (s, e) => CalculateTotal();
        _activityCheckBoxes[activity.Id] = checkbox;
        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        // Activity info
        var infoStack = new VerticalStackLayout { Spacing = 4 };

        var lblName = new Label
        {
            Text = activity.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        infoStack.Children.Add(lblName);

        var lblFrequency = new Label
        {
            Text = GetFrequencyDescription(activity),
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        infoStack.Children.Add(lblFrequency);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // EXP badge - use calculated EXP for PercentOfLevel activities
        int expAmount = GetActivityExp(activity);
        var expBadge = new Label
        {
            Text = $"+{expAmount}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(expBadge, 2);
        grid.Children.Add(expBadge);

        frame.Content = grid;
        return frame;
    }

    private string GetFrequencyDescription(Activity activity)
    {
        if (activity.AutoAwardFrequency == "Daily")
            return "📅 Daily";
        else if (activity.AutoAwardFrequency == "Weekly")
            return $"📆 {activity.AutoAwardDays}";
        else if (activity.AutoAwardFrequency == "Monthly")
            return "🗓️ Monthly (1st of month)";
        return "";
    }

    private void CalculateTotal()
    {
        _totalExp = 0;
        foreach (var kvp in _activityCheckBoxes)
        {
            if (kvp.Value.IsChecked)
            {
                var activity = _eligibleActivities.FirstOrDefault(a => a.Id == kvp.Key);
                if (activity != null)
                {
                    int expAmount = GetActivityExp(activity);
                    _totalExp += expAmount * activity.Multiplier;
                }
            }
        }

        _lblTotal.Text = $"Total EXP to Award: +{_totalExp}";
    }

    private async void OnAwardClicked(object? sender, EventArgs e)
    {
        var selectedActivities = _eligibleActivities
            .Where(a => _activityCheckBoxes[a.Id].IsChecked)
            .ToList();

        if (selectedActivities.Count == 0)
        {
            await Navigation.PopModalAsync();
            return;
        }

        // Award EXP for each selected activity
        int bonusExp = 0;
        var bonusDetails = new List<string>();
        
        foreach (var activity in selectedActivities)
        {
            int expAmount = GetActivityExp(activity);
            
            // Apply EXP (repeated per multiplier)
            for (int i = 0; i < activity.Multiplier; i++)
            {
                await _expService.ApplyExpAsync(_username, _gameId, activity.Name, expAmount, activity.Id);
            }

            // Record display day streak (same as manual calculation)
            await _activityService.RecordDisplayDayStreakAsync(activity);
            
            // Increment times completed
            activity.TimesCompleted++;
            
            // Check for streak milestone bonus (only for positive EXP activities)
            if (expAmount > 0)
            {
                int streakBonus = ActivityService.CalculateStreakBonus(activity.DisplayDayStreak);
                if (streakBonus > 0)
                {
                    await _expService.ApplyExpAsync(_username, _gameId, $"{activity.Name} (Streak Bonus)", streakBonus, activity.Id);
                    bonusExp += streakBonus;
                    bonusDetails.Add($"🔥 {activity.Name} streak bonus ({activity.DisplayDayStreak} days): +{streakBonus}");
                }
            }

            // Update LastAutoAwarded
            activity.LastAutoAwarded = DateTime.Now;
            await _activityService.UpdateActivityAsync(activity);
        }

        int grandTotal = _totalExp + bonusExp;
        string bonusMessage = bonusDetails.Count > 0 
            ? $"\n\n{string.Join("\n", bonusDetails)}" 
            : "";
        
        await DisplayAlert(
            "EXP Awarded!",
            $"Awarded {grandTotal} total EXP from {selectedActivities.Count} auto-award activit{(selectedActivities.Count == 1 ? "y" : "ies")}.{bonusMessage}",
            "OK"
        );

        await Navigation.PopModalAsync();
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Skip All",
            "Skip all auto-awards for today?",
            "Yes",
            "No"
        );

        if (confirm)
        {
            // Mark all as "awarded" without actually awarding EXP
            foreach (var activity in _eligibleActivities)
            {
                activity.LastAutoAwarded = DateTime.Now;
                await _activityService.UpdateActivityAsync(activity);
            }

            await Navigation.PopModalAsync();
        }
    }
}
