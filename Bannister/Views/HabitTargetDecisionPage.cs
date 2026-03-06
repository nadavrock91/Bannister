using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Full-screen page for setting habit target dates on activities.
/// Shows activities one at a time with full name visibility and easy date selection.
/// </summary>
public class HabitTargetDecisionPage : ContentPage
{
    private readonly ActivityService _activities;
    private readonly List<Activity> _activitiesToProcess;
    private readonly bool _isExpiredMode;
    private int _currentIndex = 0;

    // UI Elements
    private Label _lblProgress;
    private Label _lblActivityName;
    private Label _lblExpiredInfo;
    private DatePicker _datePicker;
    private VerticalStackLayout _mainContent;

    public HabitTargetDecisionPage(ActivityService activities, List<Activity> activitiesToProcess, bool isExpiredMode = false)
    {
        _activities = activities;
        _activitiesToProcess = activitiesToProcess;
        _isExpiredMode = isExpiredMode;

        Title = isExpiredMode ? "Expired Habit Targets" : "Set Habit Targets";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
        ShowCurrentActivity();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        _mainContent = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center
        };

        // Progress indicator
        _lblProgress = new Label
        {
            Text = "1 of 1",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        _mainContent.Children.Add(_lblProgress);

        // Header
        var headerFrame = new Frame
        {
            BackgroundColor = _isExpiredMode ? Color.FromArgb("#FF9800") : Color.FromArgb("#4CAF50"),
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        headerStack.Children.Add(new Label
        {
            Text = _isExpiredMode ? "⏰ Habit Target Expired" : "🎯 Set Habit Target",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        });
        headerStack.Children.Add(new Label
        {
            Text = _isExpiredMode 
                ? "This activity's target date has passed. Choose a new date or opt out."
                : "When do you want to make this activity a habit?",
            FontSize = 14,
            TextColor = Colors.White,
            Opacity = 0.9,
            HorizontalTextAlignment = TextAlignment.Center
        });
        headerFrame.Content = headerStack;
        _mainContent.Children.Add(headerFrame);

        // Activity name frame
        var activityFrame = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true
        };

        var activityStack = new VerticalStackLayout { Spacing = 8 };
        activityStack.Children.Add(new Label
        {
            Text = "Activity:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        _lblActivityName = new Label
        {
            Text = "",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        activityStack.Children.Add(_lblActivityName);

        _lblExpiredInfo = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#FF5722"),
            IsVisible = false
        };
        activityStack.Children.Add(_lblExpiredInfo);

        activityFrame.Content = activityStack;
        _mainContent.Children.Add(activityFrame);

        // Quick select buttons
        _mainContent.Children.Add(new Label
        {
            Text = "Add days to target date:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 8, 0, 0)
        });

        var quickButtonsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        var btn7 = CreateQuickButton("7d", 7);
        Grid.SetColumn(btn7, 0);
        Grid.SetRow(btn7, 0);
        quickButtonsGrid.Children.Add(btn7);

        var btn30 = CreateQuickButton("30d", 30);
        Grid.SetColumn(btn30, 1);
        Grid.SetRow(btn30, 0);
        quickButtonsGrid.Children.Add(btn30);

        var btn90 = CreateQuickButton("90d", 90);
        Grid.SetColumn(btn90, 2);
        Grid.SetRow(btn90, 0);
        quickButtonsGrid.Children.Add(btn90);

        var btn180 = CreateQuickButton("180d", 180);
        Grid.SetColumn(btn180, 0);
        Grid.SetRow(btn180, 1);
        quickButtonsGrid.Children.Add(btn180);

        var btn365 = CreateQuickButton("1yr", 365);
        Grid.SetColumn(btn365, 1);
        Grid.SetRow(btn365, 1);
        quickButtonsGrid.Children.Add(btn365);

        var btn730 = CreateQuickButton("2yr", 730);
        Grid.SetColumn(btn730, 2);
        Grid.SetRow(btn730, 1);
        quickButtonsGrid.Children.Add(btn730);

        _mainContent.Children.Add(quickButtonsGrid);

        // Date picker section
        _mainContent.Children.Add(new Label
        {
            Text = "Target Date:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 16, 0, 0)
        });

        var datePickerFrame = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 16,
            CornerRadius = 8,
            HasShadow = true,
            BorderColor = Color.FromArgb("#4CAF50")
        };

        var dateStack = new VerticalStackLayout { Spacing = 12 };
        _datePicker = new DatePicker
        {
            MinimumDate = DateTime.Now.Date.AddDays(1),
            Date = DateTime.Now.AddDays(1),
            HorizontalOptions = LayoutOptions.Center,
            FontSize = 18
        };
        dateStack.Children.Add(_datePicker);

        var btnSetDate = new Button
        {
            Text = "✓ Set This Target Date",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        btnSetDate.Clicked += OnSetSpecificDateClicked;
        dateStack.Children.Add(btnSetDate);

        datePickerFrame.Content = dateStack;
        _mainContent.Children.Add(datePickerFrame);

        // No habit target button
        var btnNoHabit = new Button
        {
            Text = "❌ I don't want this as a habit",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 16, 0, 0)
        };
        btnNoHabit.Clicked += OnNoHabitTargetClicked;
        _mainContent.Children.Add(btnNoHabit);

        // Skip button (only for non-expired)
        if (!_isExpiredMode)
        {
            var btnSkip = new Button
            {
                Text = "Skip (decide later)",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#666"),
                FontSize = 14
            };
            btnSkip.Clicked += OnSkipClicked;
            _mainContent.Children.Add(btnSkip);
        }

        // Skip all button
        var btnSkipAll = new Button
        {
            Text = _isExpiredMode ? "Skip remaining (default +30 days)" : "Skip all remaining",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#999"),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        btnSkipAll.Clicked += OnSkipAllClicked;
        _mainContent.Children.Add(btnSkipAll);

        scrollView.Content = _mainContent;
        Content = scrollView;
    }

    private Button CreateQuickButton(string text, int days)
    {
        var btn = new Button
        {
            Text = "+" + text,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        btn.Clicked += (s, e) => {
            _datePicker.Date = _datePicker.Date.AddDays(days);
        };
        return btn;
    }

    private void ShowCurrentActivity()
    {
        if (_currentIndex >= _activitiesToProcess.Count)
        {
            // All done
            Navigation.PopModalAsync();
            return;
        }

        var activity = _activitiesToProcess[_currentIndex];
        _lblProgress.Text = $"{_currentIndex + 1} of {_activitiesToProcess.Count}";
        _lblActivityName.Text = activity.Name;

        if (_isExpiredMode && activity.HabitTargetDate.HasValue)
        {
            _lblExpiredInfo.Text = $"Was due: {activity.HabitTargetDate.Value:MMMM dd, yyyy}";
            _lblExpiredInfo.IsVisible = true;
        }
        else
        {
            _lblExpiredInfo.IsVisible = false;
        }

        // Reset date picker to default (tomorrow)
        _datePicker.Date = DateTime.Now.AddDays(1);
    }

    private async void OnSetSpecificDateClicked(object? sender, EventArgs e)
    {
        var activity = _activitiesToProcess[_currentIndex];
        
        // Track first time target was set
        if (!activity.HabitTargetFirstSet.HasValue)
        {
            activity.HabitTargetFirstSet = DateTime.Now;
        }
        else if (_isExpiredMode)
        {
            // Increment postpone count when setting from expired mode
            activity.HabitTargetPostponeCount++;
        }
        
        activity.HabitTargetDate = _datePicker.Date;
        activity.NoHabitTarget = false;
        await _activities.UpdateActivityAsync(activity);
        
        _currentIndex++;
        ShowCurrentActivity();
    }

    private async void OnNoHabitTargetClicked(object? sender, EventArgs e)
    {
        var activity = _activitiesToProcess[_currentIndex];
        activity.HabitTargetDate = null;
        activity.NoHabitTarget = true;
        await _activities.UpdateActivityAsync(activity);
        
        _currentIndex++;
        ShowCurrentActivity();
    }

    private void OnSkipClicked(object? sender, EventArgs e)
    {
        _currentIndex++;
        ShowCurrentActivity();
    }

    private async void OnSkipAllClicked(object? sender, EventArgs e)
    {
        // For expired mode, set remaining to +30 days and track postpone
        // For normal mode, just skip without setting
        if (_isExpiredMode)
        {
            for (int i = _currentIndex; i < _activitiesToProcess.Count; i++)
            {
                var activity = _activitiesToProcess[i];
                activity.HabitTargetDate = DateTime.Now.AddDays(30);
                activity.HabitTargetPostponeCount++;
                await _activities.UpdateActivityAsync(activity);
            }
        }
        
        await Navigation.PopModalAsync();
    }
}
