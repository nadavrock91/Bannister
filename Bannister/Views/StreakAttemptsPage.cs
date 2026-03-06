using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing streak attempt history for a specific activity
/// </summary>
public class StreakAttemptsPage : ContentPage
{
    private readonly StreakService _streaks;
    private readonly string _username;
    private readonly string _game;
    private readonly Activity _activity;

    private VerticalStackLayout _attemptsContainer;

    public StreakAttemptsPage(StreakService streaks, string username, string game, Activity activity)
    {
        _streaks = streaks;
        _username = username;
        _game = game;
        _activity = activity;

        Title = $"Streak History: {activity.Name}";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAttemptsAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#FF9800"),
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add(new Label
        {
            Text = $"🔥 {_activity.Name}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        headerStack.Children.Add(new Label
        {
            Text = "Streak Attempt History",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Start New Streak Button
        var btnNewStreak = new Button
        {
            Text = "🔥 Start New Streak",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 45
        };
        btnNewStreak.Clicked += OnStartNewStreakClicked;
        mainStack.Children.Add(btnNewStreak);

        // Attempts list
        var scrollView = new ScrollView();
        _attemptsContainer = new VerticalStackLayout { Spacing = 12 };
        scrollView.Content = _attemptsContainer;

        mainStack.Children.Add(scrollView);

        Content = mainStack;
    }

    private async Task LoadAttemptsAsync()
    {
        _attemptsContainer.Children.Clear();

        var attempts = await _streaks.GetStreakAttemptsAsync(_username, _game, _activity.Id);

        if (attempts.Count == 0)
        {
            _attemptsContainer.Children.Add(new Label
            {
                Text = "No streak attempts yet.\nClick 'Start New Streak' to begin tracking!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        foreach (var attempt in attempts)
        {
            _attemptsContainer.Children.Add(BuildAttemptCard(attempt));
        }
    }

    private Frame BuildAttemptCard(StreakAttempt attempt)
    {
        var isActive = attempt.IsActive;
        var borderColor = isActive ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
        var bgColor = isActive ? Color.FromArgb("#E8F5E9") : Colors.White;

        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 10,
            BackgroundColor = bgColor,
            BorderColor = borderColor,
            HasShadow = isActive
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Header row
        var headerRow = new HorizontalStackLayout { Spacing = 12 };

        headerRow.Children.Add(new Label
        {
            Text = $"Attempt #{attempt.AttemptNumber}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });

        headerRow.Children.Add(new Label
        {
            Text = attempt.Status,
            FontSize = 14,
            TextColor = isActive ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336"),
            VerticalOptions = LayoutOptions.Center
        });

        stack.Children.Add(headerRow);

        // Days achieved (big number)
        var daysRow = new HorizontalStackLayout { Spacing = 8 };

        daysRow.Children.Add(new Label
        {
            Text = attempt.DaysAchieved.ToString(),
            FontSize = 36,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#FF9800") : Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        daysRow.Children.Add(new Label
        {
            Text = attempt.DaysAchieved == 1 ? "day" : "days",
            FontSize = 16,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(daysRow);

        // Date range
        if (!string.IsNullOrEmpty(attempt.DateRangeDisplay))
        {
            stack.Children.Add(new Label
            {
                Text = $"📅 {attempt.DateRangeDisplay}",
                FontSize = 12,
                TextColor = Color.FromArgb("#999")
            });
        }

        // End button for active streaks
        if (isActive)
        {
            var btnEnd = new Button
            {
                Text = "End Streak",
                BackgroundColor = Color.FromArgb("#F44336"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 35,
                Margin = new Thickness(0, 8, 0, 0)
            };
            btnEnd.Clicked += async (s, e) => await OnEndStreakClicked(attempt);
            stack.Children.Add(btnEnd);
        }

        frame.Content = stack;
        return frame;
    }

    private async void OnStartNewStreakClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Start New Streak?",
            "This will start a new streak attempt. If there's an active streak, it will be ended.",
            "Start",
            "Cancel");

        if (confirm)
        {
            await _streaks.StartNewStreakAsync(_username, _game, _activity.Id, _activity.Name);
            await LoadAttemptsAsync();
        }
    }

    private async Task OnEndStreakClicked(StreakAttempt attempt)
    {
        bool confirm = await DisplayAlert(
            "End Streak?",
            $"End this streak at {attempt.DaysAchieved} days?",
            "End",
            "Cancel");

        if (confirm)
        {
            await _streaks.EndStreakAsync(attempt.Id);
            await LoadAttemptsAsync();
        }
    }
}
