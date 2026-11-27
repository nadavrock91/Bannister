using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AllCountdownsPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly CountdownService _countdowns;
    
    private VerticalStackLayout _countdownsLayout;
    private Picker _filterPicker;
    private Picker _sortPicker;
    
    public AllCountdownsPage(AuthService auth, CountdownService countdowns)
    {
        _auth = auth;
        _countdowns = countdowns;
        
        Title = "All Countdowns";

        
        BuildUI();
    }
    
    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };
        
        // Header
        mainStack.Children.Add((new Label
        {
            Text = "📊 All Countdowns",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));
        
        // Filters row
        var filterRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };
        
        // Status filter
        var filterStack = new VerticalStackLayout { Spacing = 4 };
        filterStack.Children.Add((new Label
        {
            Text = "Filter:",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        
        _filterPicker = (new Picker
        {
            ItemsSource = new[] { "All", "Active", "Correct", "Wrong", "Cancelled" },
            SelectedIndex = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _filterPicker.SelectedIndexChanged += async (s, e) => await LoadCountdownsAsync();
        filterStack.Children.Add(_filterPicker);
        
        Grid.SetColumn(filterStack, 0);
        filterRow.Children.Add(filterStack);
        
        // Sort
        var sortStack = new VerticalStackLayout { Spacing = 4 };
        sortStack.Children.Add((new Label
        {
            Text = "Sort:",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        
        _sortPicker = (new Picker
        {
            ItemsSource = new[] { "Target Date", "Created Date", "Category", "Days Remaining" },
            SelectedIndex = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _sortPicker.SelectedIndexChanged += async (s, e) => await LoadCountdownsAsync();
        sortStack.Children.Add(_sortPicker);
        
        Grid.SetColumn(sortStack, 1);
        filterRow.Children.Add(sortStack);
        
        mainStack.Children.Add(filterRow);
        
        // Countdowns list
        _countdownsLayout = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_countdownsLayout);
        
        scrollView.Content = mainStack;
        Content = scrollView;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCountdownsAsync();
    }
    
    private async Task LoadCountdownsAsync()
    {
        _countdownsLayout.Children.Clear();
        
        var allCountdowns = await _countdowns.GetCountdownsAsync(_auth.CurrentUsername);
        
        // Apply filter
        string filter = _filterPicker.SelectedItem?.ToString() ?? "All";
        var filtered = filter switch
        {
            "Active" => allCountdowns.Where(c => c.Status == "Active").ToList(),
            "Correct" => allCountdowns.Where(c => c.Status == "Correct").ToList(),
            "Wrong" => allCountdowns.Where(c => c.Status == "Wrong").ToList(),
            "Cancelled" => allCountdowns.Where(c => c.Status == "Cancelled").ToList(),
            _ => allCountdowns
        };
        
        // Apply sort
        string sort = _sortPicker.SelectedItem?.ToString() ?? "Target Date";
        var sorted = sort switch
        {
            "Created Date" => filtered.OrderByDescending(c => c.CreatedAt).ToList(),
            "Category" => filtered.OrderBy(c => c.Category).ThenBy(c => c.TargetDate).ToList(),
            "Days Remaining" => filtered.OrderBy(c => c.DaysRemaining).ToList(),
            _ => filtered.OrderBy(c => c.TargetDate).ToList()
        };
        
        if (sorted.Count == 0)
        {
            _countdownsLayout.Children.Add((new Label
            {
                Text = "No countdowns found.",
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }
        
        // Stats
        var stats = await _countdowns.GetStatsAsync(_auth.CurrentUsername);
        var accuracy = await _countdowns.GetAccuracyAsync(_auth.CurrentUsername);
        
        _countdownsLayout.Children.Add((new Label
        {
            Text = $"Showing {sorted.Count} countdowns • Accuracy: {accuracy:F0}%",
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        
        foreach (var countdown in sorted)
        {
            var card = CreateCountdownCard(countdown);
            _countdownsLayout.Children.Add(card);
        }
    }
    
    private Frame CreateCountdownCard(Countdown countdown)
    {
        var frame = (new Frame
        {
            BorderColor = countdown.StatusColor,
            CornerRadius = 12,
            Padding = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground);
        
        var stack = new VerticalStackLayout { Spacing = 6 };
        
        // Title row with category badge
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };
        
        // Category badge
        var categoryBadge = (new Frame
        {
            BorderColor = Colors.Transparent,
            CornerRadius = 4,
            Padding = new Thickness(6, 2),
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton);
        categoryBadge.Content = (new Label
        {
            Text = countdown.Category,
            FontSize = 10
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);
        Grid.SetColumn(categoryBadge, 0);
        titleRow.Children.Add(categoryBadge);
        
        // Name
        var nameLabel = (new Label
        {
            Text = countdown.StatusEmoji + " " + countdown.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true);
        Grid.SetColumn(nameLabel, 1);
        titleRow.Children.Add(nameLabel);
        
        // Days remaining
        var daysLabel = new Label
        {
            Text = countdown.Status == "Active" ? countdown.DaysRemainingDisplay : countdown.Status,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = countdown.StatusColor,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(daysLabel, 2);
        titleRow.Children.Add(daysLabel);
        
        stack.Children.Add(titleRow);
        
        // Target date
        stack.Children.Add((new Label
        {
            Text = $"Target: {countdown.TargetDate:MMM d, yyyy}",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        
        // Postpone info if any
        if (countdown.PostponeCount > 0)
        {
            stack.Children.Add((new Label
            {
                Text = $"⏱️ Postponed {countdown.PostponeCount}x",
                FontSize = 11
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true));
        }
        
        frame.Content = stack;
        
        return frame;
    }
}
