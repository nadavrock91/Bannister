using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing streak attempt history for a specific activity
/// </summary>
public class StreakAttemptsPage : Bannister.Views.ModeThemedPage
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
        var headerFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningText);

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add((new Label
        {
            Text = $"🔥 {_activity.Name}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        headerStack.Children.Add((new Label
        {
            Text = "Streak Attempt History",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Start New Streak Button
        var btnNewStreak = (new Button
        {
            Text = "🔥 Start New Streak",
            CornerRadius = 8,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 45
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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
            _attemptsContainer.Children.Add((new Label
            {
                Text = "No streak attempts yet.\nClick 'Start New Streak' to begin tracking!",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
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
        int displayDays = GetAttemptDisplayDays(attempt);
        var borderColor = isActive ? ThemeKey.SuccessButtonBackground : ThemeKey.Separator;
        var bgColor = isActive ? ThemeKey.SuccessBackground : ThemeKey.CardBackground;

        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 10,
            HasShadow = isActive
        }).ThemeColor(VisualElement.BackgroundColorProperty, bgColor).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, borderColor);

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Header row
        var headerRow = new HorizontalStackLayout { Spacing = 12 };

        headerRow.Children.Add((new Label
        {
            Text = $"Attempt #{attempt.AttemptNumber}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        headerRow.Children.Add((new Label
        {
            Text = attempt.Status,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, isActive ? ThemeKey.SuccessButtonBackground : ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add(headerRow);

        // Days achieved (big number)
        var daysRow = new HorizontalStackLayout { Spacing = 8 };

        daysRow.Children.Add((new Label
        {
            Text = displayDays.ToString(),
            FontSize = 36,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, isActive ? ThemeKey.WarningText : ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true));

        daysRow.Children.Add((new Label
        {
            Text = displayDays == 1 ? "day" : "days",
            FontSize = 16,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 8)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(daysRow);

        // Date range
        if (!string.IsNullOrEmpty(attempt.DateRangeDisplay))
        {
            stack.Children.Add((new Label
            {
                Text = $"📅 {attempt.DateRangeDisplay}",
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }

        // End button for active streaks
        if (isActive)
        {
            var btnEnd = (new Button
            {
                Text = "End Streak",
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 35,
                Margin = new Thickness(0, 8, 0, 0)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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
            $"End this streak at {GetAttemptDisplayDays(attempt)} days?",
            "End",
            "Cancel");

        if (confirm)
        {
            await _streaks.EndStreakAsync(attempt.Id);
            await LoadAttemptsAsync();
        }
    }

    private int GetAttemptDisplayDays(StreakAttempt attempt)
    {
        return attempt.DaysAchieved;
    }
}
