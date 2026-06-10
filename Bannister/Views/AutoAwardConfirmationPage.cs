using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AutoAwardConfirmationPage : ContentPage
{
    private readonly List<Activity> _eligibleActivities;
    private readonly int _currentLevel;
    private readonly Dictionary<int, int> _pendingDayCounts;
    private readonly Dictionary<int, CheckBox> _activityCheckBoxes = new();
    private readonly TaskCompletionSource<bool> _completion = new();
    private readonly Label _totalLabel = new();
    private readonly Button _awardButton;
    private bool _isClosing;

    public List<Activity> SelectedActivities { get; private set; } = new();

    public Task<bool> WaitForCompletionAsync() => _completion.Task;

    public AutoAwardConfirmationPage(
        List<Activity> eligibleActivities,
        ExpService expService,
        ActivityService activityService,
        string username,
        string gameId,
        int currentLevel,
        Dictionary<int, int>? pendingDayCounts = null)
    {
        _eligibleActivities = eligibleActivities;
        _currentLevel = currentLevel;
        _pendingDayCounts = pendingDayCounts ?? eligibleActivities.ToDictionary(a => a.Id, _ => 1);

        Title = "Auto-Award Activities";
        BackgroundColor = Color.FromArgb("#F5F7FC");

        _awardButton = new Button
        {
            Text = "Award EXP",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 12,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 60,
            HorizontalOptions = LayoutOptions.Fill
        };
        _awardButton.Clicked += OnAwardClicked;

        BuildUI();
        CalculateTotal();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#F5F7FC")
        };

        var topSection = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 15,
            BackgroundColor = Color.FromArgb("#F5F7FC")
        };

        topSection.Children.Add(new Label
        {
            Text = "Auto-Award Activities",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            HorizontalOptions = LayoutOptions.Center
        });

        _totalLabel.FontSize = 28;
        _totalLabel.FontAttributes = FontAttributes.Bold;
        _totalLabel.TextColor = Color.FromArgb("#4CAF50");
        _totalLabel.HorizontalOptions = LayoutOptions.Center;
        _totalLabel.Margin = new Thickness(0, 5, 0, 10);
        topSection.Children.Add(_totalLabel);

        topSection.Children.Add(_awardButton);

        var skipButton = new Button
        {
            Text = "Skip For Now",
            BackgroundColor = Color.FromArgb("#999999"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Fill
        };
        skipButton.Clicked += OnSkipClicked;
        topSection.Children.Add(skipButton);

        topSection.Children.Add(new Label
        {
            Text = "Uncheck any you do not want to award. Pending awards will be backdated per day.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666666"),
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 10, 0, 0)
        });

        mainGrid.Add(topSection, 0, 0);

        var activityStack = new VerticalStackLayout
        {
            Padding = new Thickness(20, 10, 20, 20),
            Spacing = 10
        };

        foreach (var activity in _eligibleActivities)
            activityStack.Children.Add(BuildActivityCard(activity));

        mainGrid.Add(new ScrollView { Content = activityStack }, 0, 1);
        Content = mainGrid;
    }

    private Frame BuildActivityCard(Activity activity)
    {
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

        var checkbox = new CheckBox
        {
            IsChecked = true,
            Color = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.CheckedChanged += (_, _) => CalculateTotal();
        _activityCheckBoxes[activity.Id] = checkbox;
        grid.Add(checkbox, 0, 0);

        int pendingDays = GetPendingDayCount(activity);
        int expPerDay = GetActivityExp(activity) * Math.Max(1, activity.Multiplier);
        int totalExp = expPerDay * pendingDays;

        var infoStack = new VerticalStackLayout { Spacing = 4 };
        infoStack.Children.Add(new Label
        {
            Text = activity.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        infoStack.Children.Add(new Label
        {
            Text = $"{GetFrequencyDescription(activity)} - {pendingDays} pending day{(pendingDays == 1 ? "" : "s")}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        infoStack.Children.Add(new Label
        {
            Text = $"+{expPerDay} per day",
            FontSize = 12,
            TextColor = Color.FromArgb("#777777")
        });
        grid.Add(infoStack, 1, 0);

        grid.Add(new Label
        {
            Text = $"+{totalExp}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Center
        }, 2, 0);

        return new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#DDDDDD"),
            Content = grid
        };
    }

    private void CalculateTotal()
    {
        int totalExp = 0;
        int selectedDays = 0;

        foreach (var activity in _eligibleActivities)
        {
            if (!_activityCheckBoxes.TryGetValue(activity.Id, out var checkbox) || !checkbox.IsChecked)
                continue;

            int pendingDays = GetPendingDayCount(activity);
            totalExp += GetActivityExp(activity) * Math.Max(1, activity.Multiplier) * pendingDays;
            selectedDays += pendingDays;
        }

        _totalLabel.Text = $"Total EXP to Award: +{totalExp} ({selectedDays} day{(selectedDays == 1 ? "" : "s")})";
    }

    private async void OnAwardClicked(object? sender, EventArgs e)
    {
        SelectedActivities = _eligibleActivities
            .Where(activity => _activityCheckBoxes.TryGetValue(activity.Id, out var checkbox) && checkbox.IsChecked)
            .ToList();

        await CloseAsync(true);
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Skip For Now",
            "Skip these auto-awards for now? They will remain pending until you confirm them.",
            "Skip",
            "Cancel");

        if (confirm)
        {
            SelectedActivities.Clear();
            await CloseAsync(false);
        }
    }

    private async Task CloseAsync(bool confirmed)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        _awardButton.IsEnabled = false;

        try
        {
            await Navigation.PopModalAsync(animated: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO-AWARD] Failed to close confirmation modal: {ex}");
        }

        _completion.TrySetResult(confirmed);
    }

    protected override void OnDisappearing()
    {
        if (!_isClosing)
            _completion.TrySetResult(false);

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false);
        return true;
    }

    private int GetActivityExp(Activity activity)
    {
        if (activity.RewardType == "PercentOfLevel")
            return ExpEngine.ExpForPercentOfLevel(_currentLevel, activity.PercentOfLevel, activity.PercentCutoffLevel);

        return activity.ExpGain;
    }

    private int GetPendingDayCount(Activity activity) =>
        _pendingDayCounts.TryGetValue(activity.Id, out int days) ? Math.Max(1, days) : 1;

    private static string GetFrequencyDescription(Activity activity)
    {
        if (activity.AutoAwardFrequency == "Daily")
            return "Daily";
        if (activity.AutoAwardFrequency == "Weekly")
            return string.IsNullOrWhiteSpace(activity.AutoAwardDays) ? "Weekly" : $"Weekly: {activity.AutoAwardDays}";
        if (activity.AutoAwardFrequency == "Monthly")
            return "Monthly";
        return "Auto-award";
    }
}
