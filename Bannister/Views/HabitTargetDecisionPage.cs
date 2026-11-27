using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Full-screen page for setting habit target dates on activities.
/// Shows activities one at a time with full name visibility and easy date selection.
/// </summary>
public class HabitTargetDecisionPage : Bannister.Views.ModeThemedPage
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
        _lblProgress = (new Label
        {
            Text = "1 of 1",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        _mainContent.Children.Add(_lblProgress);

        // Header
        var headerFrame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, _isExpiredMode ? ThemeKey.WarningButtonBackground : ThemeKey.SuccessButtonBackground);

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        headerStack.Children.Add((new Label
        {
            Text = _isExpiredMode ? "⏰ Habit Target Expired" : "🎯 Set Habit Target",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText1, classifyPageText: true));
        headerStack.Children.Add((new Label
        {
            Text = _isExpiredMode 
                ? "This activity's target date has passed. Choose a new date or opt out."
                : "When do you want to make this activity a habit?",
            FontSize = 14,
            Opacity = 0.9,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText1, classifyPageText: true));
        headerFrame.Content = headerStack;
        _mainContent.Children.Add(headerFrame);

        // Activity name frame
        var activityFrame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var activityStack = new VerticalStackLayout { Spacing = 8 };
        activityStack.Children.Add((new Label
        {
            Text = "Activity:",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        _lblActivityName = (new Label
        {
            Text = "",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        activityStack.Children.Add(_lblActivityName);

        _lblExpiredInfo = (new Label
        {
            Text = "",
            FontSize = 12,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.DeepOrangeButtonBackground, ThemeKey.PageText3, classifyPageText: true);
        activityStack.Children.Add(_lblExpiredInfo);

        activityFrame.Content = activityStack;
        _mainContent.Children.Add(activityFrame);

        // Quick select buttons
        _mainContent.Children.Add((new Label
        {
            Text = "Add days to target date:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

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
        _mainContent.Children.Add((new Label
        {
            Text = "Target Date:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 16, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        var datePickerFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 8,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.SuccessLightText);

        var dateStack = new VerticalStackLayout { Spacing = 12 };
        _datePicker = new DatePicker
        {
            MinimumDate = DateTime.Now.Date.AddDays(1),
            Date = DateTime.Now.AddDays(1),
            HorizontalOptions = LayoutOptions.Center,
            FontSize = 18
        };
        dateStack.Children.Add(_datePicker);

        var btnSetDate = (new Button
        {
            Text = "✓ Set This Target Date",
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        btnSetDate.Clicked += OnSetSpecificDateClicked;
        dateStack.Children.Add(btnSetDate);

        datePickerFrame.Content = dateStack;
        _mainContent.Children.Add(datePickerFrame);

        // No habit target button
        var btnNoHabit = (new Button
        {
            Text = "❌ I don't want this as a habit",
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 16, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        btnNoHabit.Clicked += OnNoHabitTargetClicked;
        _mainContent.Children.Add(btnNoHabit);

        // Skip button (only for non-expired)
        if (!_isExpiredMode)
        {
            var btnSkip = (new Button
            {
                Text = "Skip (decide later)",
                BackgroundColor = Colors.Transparent,
                FontSize = 14
            }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText2);
            btnSkip.Clicked += OnSkipClicked;
            _mainContent.Children.Add(btnSkip);
        }

        // Skip all button
        var btnSkipAll = (new Button
        {
            Text = _isExpiredMode ? "Skip remaining (default +30 days)" : "Skip all remaining",
            BackgroundColor = Colors.Transparent,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        btnSkipAll.Clicked += OnSkipAllClicked;
        _mainContent.Children.Add(btnSkipAll);

        scrollView.Content = _mainContent;
        Content = scrollView;
    }

    private Button CreateQuickButton(string text, int days)
    {
        var btn = (new Button
        {
            Text = "+" + text,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
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
