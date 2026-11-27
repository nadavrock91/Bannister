using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing the history of changes for a specific streak
/// </summary>
public class StreakHistoryPage : Bannister.Views.ModeThemedPage
{
    private readonly StreakService _streaks;
    private readonly StreakAttempt _streak;
    private VerticalStackLayout _historyContainer;

    public StreakHistoryPage(StreakService streaks, StreakAttempt streak)
    {
        _streaks = streaks;
        _streak = streak;

        Title = "Streak History";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHistoryAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
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
            BorderColor = Colors.Transparent,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground);

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add((new Label
        {
            Text = $"📊 {_streak.ActivityName}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));

        headerStack.Children.Add((new Label
        {
            Text = $"Attempt #{_streak.AttemptNumber} • Currently {_streak.DaysAchieved} days",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellow, ThemeKey.PageText1, classifyPageText: true));

        if (_streak.StartedAt.HasValue)
        {
            headerStack.Children.Add((new Label
            {
                Text = $"Started: {_streak.StartedAt.Value.ToLocalTime():MMM dd, yyyy}",
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellow, ThemeKey.PageText3, classifyPageText: true));
        }

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Current summary card
        var summaryFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.SuccessButtonBackground);

        var summaryGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowSpacing = 4
        };

        // Current days
        var currentStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center };
        currentStack.Children.Add((new Label
        {
            Text = _streak.DaysAchieved.ToString(),
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true));
        currentStack.Children.Add((new Label
        {
            Text = "Current",
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(currentStack, 0);
        summaryGrid.Children.Add(currentStack);

        // Status
        var statusStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center };
        statusStack.Children.Add(new Label
        {
            Text = _streak.IsActive ? "🔥" : "⏸️",
            FontSize = 32,
            HorizontalTextAlignment = TextAlignment.Center
        });
        statusStack.Children.Add((new Label
        {
            Text = _streak.IsActive ? "Active" : "Ended",
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(statusStack, 1);
        summaryGrid.Children.Add(statusStack);

        // Duration
        var durationStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center };
        int durationDays = _streak.StartedAt.HasValue 
            ? (int)(DateTime.UtcNow - _streak.StartedAt.Value).TotalDays + 1
            : _streak.DaysAchieved;
        durationStack.Children.Add((new Label
        {
            Text = durationDays.ToString(),
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText1, classifyPageText: true));
        durationStack.Children.Add((new Label
        {
            Text = "Calendar Days",
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(durationStack, 2);
        summaryGrid.Children.Add(durationStack);

        summaryFrame.Content = summaryGrid;
        mainStack.Children.Add(summaryFrame);

        // History section header
        mainStack.Children.Add((new Label
        {
            Text = "Change History",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        // History list container
        _historyContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_historyContainer);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async Task LoadHistoryAsync()
    {
        _historyContainer.Children.Clear();

        var completions = await _streaks.GetTargetCompletionsForAttemptAsync(_streak.Id);
        if (completions.Count > 0)
        {
            _historyContainer.Children.Add((new Label
            {
                Text = "Target Completion Rows",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

            foreach (var completion in completions)
            {
                _historyContainer.Children.Add(BuildTargetCompletionCard(completion));
            }
        }

        var logs = await _streaks.GetStreakLogsAsync(_streak.Id);

        if (logs.Count == 0)
        {
            // No logs yet - show synthetic history based on start date
            _historyContainer.Children.Add((new Frame
            {
                Padding = 16,
                CornerRadius = 8,
                BorderColor = Colors.Transparent,
                HasShadow = false,
                Content = (new Label
                {
                    Text = "📝 No detailed history recorded yet.\n\nHistory tracking starts from now - future changes will be logged here.",
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow));

            // Show initial entry based on streak start
            if (_streak.StartedAt.HasValue)
            {
                _historyContainer.Children.Add(BuildHistoryCard(new StreakLog
                {
                    DaysBefore = 0,
                    DaysAfter = 1,
                    ChangeType = "created",
                    LoggedAt = _streak.StartedAt.Value,
                    Note = "Streak started (estimated)"
                }));
            }

            return;
        }

        // Group logs by date
        var groupedLogs = logs
            .GroupBy(l => l.LoggedAt.ToLocalTime().Date)
            .OrderByDescending(g => g.Key);

        foreach (var group in groupedLogs)
        {
            // Date header
            var dateLabel = (new Label
            {
                Text = group.Key.ToString("dddd, MMM dd, yyyy"),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 4)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText1, classifyPageText: true);
            _historyContainer.Children.Add(dateLabel);

            // Log entries for this date
            foreach (var log in group.OrderByDescending(l => l.LoggedAt))
            {
                _historyContainer.Children.Add(BuildHistoryCard(log));
            }
        }
    }

    private Frame BuildHistoryCard(StreakLog log)
    {
        var isPositive = log.Change >= 0;
        var changeColor = log.ChangeType == "created" ? ThemeKey.HighlightIndigoText : isPositive ? ThemeKey.SuccessButtonBackground : ThemeKey.ErrorButtonBackground;

        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 60 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        // Change amount (left)
        var changeStack = new VerticalStackLayout 
        { 
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        changeStack.Children.Add((new Label
        {
            Text = log.ChangeDisplay,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, changeColor, ThemeKey.PageText1, classifyPageText: true));
        if (log.ChangeType != "created")
        {
            changeStack.Children.Add((new Label
            {
                Text = $"→ {log.DaysAfter}",
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }
        Grid.SetColumn(changeStack, 0);
        grid.Children.Add(changeStack);

        // Details (middle)
        var detailStack = new VerticalStackLayout 
        { 
            VerticalOptions = LayoutOptions.Center,
            Spacing = 2
        };
        detailStack.Children.Add((new Label
        {
            Text = log.ChangeTypeDisplay,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        if (!string.IsNullOrEmpty(log.Note))
        {
            detailStack.Children.Add((new Label
            {
                Text = log.Note,
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        }
        Grid.SetColumn(detailStack, 1);
        grid.Children.Add(detailStack);

        // Time (right)
        var timeStack = new VerticalStackLayout 
        { 
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        timeStack.Children.Add((new Label
        {
            Text = log.TimeDisplay,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.End
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(timeStack, 2);
        grid.Children.Add(timeStack);

        if (log.Id > 0)
        {
            frame.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await DeleteStreakLogRowAsync(log))
            });
        }

        frame.Content = grid;
        return frame;
    }

    private Frame BuildTargetCompletionCard(StreakTargetCompletion completion)
    {
        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.SuccessButtonBackground);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 60 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        grid.Children.Add((new Label
        {
            Text = completion.TargetDays.ToString(),
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true));

        var details = new VerticalStackLayout { Spacing = 2 };
        details.Children.Add((new Label
        {
            Text = $"{completion.TargetDays} in a row completed",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        details.Children.Add((new Label
        {
            Text = "Tap to delete this stat row",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var timeLabel = (new Label
        {
            Text = completion.CompletedAtDisplay,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true);
        Grid.SetColumn(timeLabel, 2);
        grid.Children.Add(timeLabel);

        frame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await DeleteTargetCompletionRowAsync(completion))
        });

        frame.Content = grid;
        return frame;
    }

    private async Task DeleteTargetCompletionRowAsync(StreakTargetCompletion completion)
    {
        bool confirm = await DisplayAlert(
            "Delete Stat Row?",
            $"Delete the {completion.TargetDays} in-a-row completion row?\n\nThis removes it from the dashboard stat.",
            "Delete",
            "Cancel");

        if (!confirm) return;

        await _streaks.DeleteTargetCompletionAsync(completion.Id);
        await LoadHistoryAsync();
    }

    private async Task DeleteStreakLogRowAsync(StreakLog log)
    {
        if (log.Id <= 0) return;

        bool confirm = await DisplayAlert(
            "Delete History Row?",
            $"Delete this history row?\n\n{log.ChangeTypeDisplay}: {log.DaysBefore} -> {log.DaysAfter}",
            "Delete",
            "Cancel");

        if (!confirm) return;

        await _streaks.DeleteStreakLogAsync(log.Id);
        await LoadHistoryAsync();
    }
}
