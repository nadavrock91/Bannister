using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class StreakTargetStatHistoryPage : ContentPage
{
    private readonly StreakService _streaks;
    private readonly string _username;
    private readonly int _targetDays;
    private Label _currentCountLabel;
    private VerticalStackLayout _historyContainer;

    public StreakTargetStatHistoryPage(StreakService streaks, string username, int targetDays)
    {
        _streaks = streaks;
        _username = username;
        _targetDays = targetDays;

        Title = $"{targetDays} Stat History";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHistoryAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#FF9800"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 4 };
        _currentCountLabel = new Label
        {
            Text = "0",
            FontSize = 36,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };
        headerStack.Children.Add(_currentCountLabel);
        headerStack.Children.Add(new Label
        {
            Text = $"{_targetDays} in a row completed",
            FontSize = 14,
            TextColor = Color.FromArgb("#FFFFFFCC"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        mainStack.Children.Add(new Label
        {
            Text = "Stat History",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _historyContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_historyContainer);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadHistoryAsync()
    {
        _historyContainer.Children.Clear();

        int currentCount = await _streaks.GetCurrentTargetCompletionCountAsync(_username, _targetDays);
        _currentCountLabel.Text = currentCount.ToString();

        var logs = await _streaks.GetTargetStatLogsAsync(_username, _targetDays);
        if (logs.Count == 0)
        {
            _historyContainer.Children.Add(new Frame
            {
                Padding = 16,
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#FFF8E1"),
                BorderColor = Colors.Transparent,
                HasShadow = false,
                Content = new Label
                {
                    Text = "No stat history recorded yet.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#F57C00"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            });
            return;
        }

        foreach (var group in logs.GroupBy(l => l.LoggedAt.ToLocalTime().Date).OrderByDescending(g => g.Key))
        {
            _historyContainer.Children.Add(new Label
            {
                Text = group.Key.ToString("dddd, MMM dd, yyyy"),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5B63EE"),
                Margin = new Thickness(0, 8, 0, 4)
            });

            foreach (var log in group.OrderByDescending(l => l.LoggedAt))
            {
                _historyContainer.Children.Add(BuildLogCard(log));
            }
        }
    }

    private Frame BuildLogCard(StreakTargetStatLog log)
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = log.CountAfter > log.CountBefore ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E53935"),
            HasShadow = false
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 58 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        var changeLabel = new Label
        {
            Text = log.ChangeDisplay,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = log.Change >= 0 ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };
        grid.Children.Add(changeLabel);

        var details = new VerticalStackLayout { Spacing = 2 };
        details.Children.Add(new Label
        {
            Text = $"{log.CountBefore} -> {log.CountAfter} completed",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        details.Children.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(log.ActivityName)
                ? log.ChangeTypeDisplay
                : $"{log.ChangeTypeDisplay}: {log.ActivityName}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        if (!string.IsNullOrWhiteSpace(log.Note))
        {
            details.Children.Add(new Label
            {
                Text = log.Note,
                FontSize = 11,
                TextColor = Color.FromArgb("#888")
            });
        }
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var timeLabel = new Label
        {
            Text = log.TimeDisplay,
            FontSize = 12,
            TextColor = Color.FromArgb("#777"),
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(timeLabel, 2);
        grid.Children.Add(timeLabel);

        frame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await ShowLogActionsAsync(log))
        });

        frame.Content = grid;
        return frame;
    }

    private async Task ShowLogActionsAsync(StreakTargetStatLog log)
    {
        string? action = await DisplayActionSheet("Stat History Row", "Cancel", null, "Edit counts", "Delete row");
        if (action == "Edit counts")
        {
            await EditLogCountsAsync(log);
        }
        else if (action == "Delete row")
        {
            await DeleteLogAsync(log);
        }
    }

    private async Task EditLogCountsAsync(StreakTargetStatLog log)
    {
        string? beforeText = await DisplayPromptAsync(
            "Edit Count Before",
            "Count before this stat change:",
            initialValue: log.CountBefore.ToString(),
            keyboard: Keyboard.Numeric);

        if (!int.TryParse(beforeText, out int countBefore) || countBefore < 0)
        {
            return;
        }

        string? afterText = await DisplayPromptAsync(
            "Edit Count After",
            "Count after this stat change:",
            initialValue: log.CountAfter.ToString(),
            keyboard: Keyboard.Numeric);

        if (!int.TryParse(afterText, out int countAfter) || countAfter < 0)
        {
            return;
        }

        await _streaks.UpdateTargetStatLogAsync(log.Id, countBefore, countAfter, "Stat history row edited by user");
        await LoadHistoryAsync();
    }

    private async Task DeleteLogAsync(StreakTargetStatLog log)
    {
        bool confirm = await DisplayAlert(
            "Delete History Row?",
            $"Delete this stat history row?\n\n{log.CountBefore} -> {log.CountAfter} completed",
            "Delete",
            "Cancel");

        if (!confirm)
        {
            return;
        }

        await _streaks.DeleteTargetStatLogAsync(log.Id);
        await LoadHistoryAsync();
    }
}
