using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing the history of daily logs for a dragon attempt
/// </summary>
public class DragonHistoryPage : Bannister.Views.ModeThemedPage
{
    private readonly Dragon _dragon;
    private readonly Attempt _attempt;
    private readonly DatabaseService _db;
    private VerticalStackLayout _historyContainer;
    private Label _lblCurrentDays;
    private Label _lblHeaderDays;

    public DragonHistoryPage(DatabaseService db, Dragon dragon, Attempt attempt)
    {
        _db = db;
        _dragon = dragon;
        _attempt = attempt;

        Title = "Dragon History";


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
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground);

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add((new Label
        {
            Text = $"🐉 {_dragon.Title}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText1, classifyPageText: true));

        // This will be updated after loading logs
        _lblHeaderDays = (new Label
        {
            Text = $"Attempt #{_attempt.AttemptNumber} • Currently {_attempt.DurationDays} days",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellowStrong, ThemeKey.PageText1, classifyPageText: true);
        headerStack.Children.Add(_lblHeaderDays);

        if (_attempt.StartedAt.HasValue)
        {
            headerStack.Children.Add((new Label
            {
                Text = $"Started: {_attempt.StartedAt.Value.ToLocalTime():MMM dd, yyyy}",
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellowStrong, ThemeKey.PageText3, classifyPageText: true));
        }

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Current summary card
        var summaryFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.SecondaryAccentText);

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

        // Current days - will be updated after loading logs
        var currentStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center };
        _lblCurrentDays = (new Label
        {
            Text = _attempt.DurationDays.ToString(),
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondaryAccentText, ThemeKey.PageText1, classifyPageText: true);
        currentStack.Children.Add(_lblCurrentDays);
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
            Text = _attempt.IsActive ? "🔥" : "⏸️",
            FontSize = 32,
            HorizontalTextAlignment = TextAlignment.Center
        });
        statusStack.Children.Add((new Label
        {
            Text = _attempt.IsActive ? "Active" : "Ended",
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(statusStack, 1);
        summaryGrid.Children.Add(statusStack);

        // Calendar days
        var durationStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center };
        int calendarDays = _attempt.StartedAt.HasValue 
            ? (int)(DateTime.UtcNow - _attempt.StartedAt.Value).TotalDays + 1
            : _attempt.DurationDays;
        durationStack.Children.Add((new Label
        {
            Text = calendarDays.ToString(),
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningAccentText, ThemeKey.PageText1, classifyPageText: true));
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

        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<DragonDayLog>();

        var logs = await conn.Table<DragonDayLog>()
            .Where(l => l.Username == _dragon.Username 
                && l.Game == _dragon.Game 
                && l.DragonTitle == _dragon.Title
                && l.AttemptNumber == _attempt.AttemptNumber)
            .OrderByDescending(l => l.LogDate)
            .ToListAsync();

        // Update the current days display with actual value from logs
        int actualCurrentDays = logs.Count > 0 
            ? logs.Max(l => l.DayNumber) 
            : _attempt.DurationDays;
        
        _lblCurrentDays.Text = actualCurrentDays.ToString();
        _lblHeaderDays.Text = $"Attempt #{_attempt.AttemptNumber} • Currently {actualCurrentDays} days";

        if (logs.Count == 0)
        {
            // No logs yet - show info message
            _historyContainer.Children.Add((new Frame
            {
                Padding = 16,
                CornerRadius = 8,
                BorderColor = Colors.Transparent,
                HasShadow = false,
                Content = (new Label
                {
                    Text = "📝 No detailed history recorded yet.\n\nEnable auto-increment or manually log days to see history here.",
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoStrongText, ThemeKey.PageText1, classifyPageText: true)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground));

            // Show synthetic start entry
            if (_attempt.StartedAt.HasValue)
            {
                _historyContainer.Children.Add(BuildHistoryCard(new DragonDayLog
                {
                    DayNumber = 1,
                    LogDate = _attempt.StartedAt.Value,
                    Description = "Attempt started",
                    Source = "system"
                }));
            }

            return;
        }

        // Group logs by date
        var groupedLogs = logs
            .GroupBy(l => l.LogDate.ToLocalTime().Date)
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
            }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondaryAccentText, ThemeKey.PageText1, classifyPageText: true);
            _historyContainer.Children.Add(dateLabel);

            // Log entries for this date
            foreach (var log in group.OrderByDescending(l => l.CreatedAt))
            {
                _historyContainer.Children.Add(BuildHistoryCard(log));
            }
        }
    }

    private Frame BuildHistoryCard(DragonDayLog log)
    {
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

        // Day number (left)
        var dayStack = new VerticalStackLayout 
        { 
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        dayStack.Children.Add((new Label
        {
            Text = $"+1",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessLightText, ThemeKey.PageText1, classifyPageText: true));
        dayStack.Children.Add((new Label
        {
            Text = $"→ {log.DayNumber}",
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(dayStack, 0);
        grid.Children.Add(dayStack);

        // Details (middle)
        var detailStack = new VerticalStackLayout 
        { 
            VerticalOptions = LayoutOptions.Center,
            Spacing = 2
        };
        detailStack.Children.Add((new Label
        {
            Text = log.Description ?? "Daily use",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        if (log.Source == "auto")
        {
            detailStack.Children.Add((new Label
            {
                Text = "🔄 Auto-incremented",
                FontSize = 11
            }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoLinkText, ThemeKey.PageText3, classifyPageText: true));
        }
        Grid.SetColumn(detailStack, 1);
        grid.Children.Add(detailStack);

        // Time (right)
        var timeLabel = (new Label
        {
            Text = log.CreatedAt.ToLocalTime().ToString("h:mm tt"),
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);
        Grid.SetColumn(timeLabel, 2);
        grid.Children.Add(timeLabel);

        frame.Content = grid;
        return frame;
    }
}
