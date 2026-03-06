using Bannister.Drawables;
using Bannister.Models;

namespace Bannister.Views;

/// <summary>
/// Partial class containing desktop UI building methods
/// </summary>
public partial class ActivityGamePage
{
    private void BuildDesktopUI()
    {
        var mainGrid = new Grid
        {
            Padding = 12,
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 250 },      // Left: Dragon panel
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }, // Middle: Activities (larger)
                new ColumnDefinition { Width = GridLength.Star }  // Right: Charts
            }
        };

        // LEFT PANEL (Dragon)
        var leftPanel = BuildLeftPanel();
        Grid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        // MIDDLE PANEL (Activities)
        var middlePanel = BuildActivitiesPanel();
        Grid.SetColumn(middlePanel, 1);
        mainGrid.Children.Add(middlePanel);

        // RIGHT PANEL (Charts)
        var rightPanel = BuildChartsPanel();
        Grid.SetColumn(rightPanel, 2);
        mainGrid.Children.Add(rightPanel);

        Content = mainGrid;
    }

    private Grid BuildLeftPanel()
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        // Dragon Card (scrollable)
        var scrollView = new ScrollView
        {
            Content = BuildDragonCard()
        };
        Grid.SetRow(scrollView, 0);
        grid.Children.Add(scrollView);

        // Button Panel (fixed at bottom)
        var buttonPanel = BuildButtonPanel();
        Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(buttonPanel);

        return grid;
    }

    private VerticalStackLayout BuildDragonCard()
    {
        var stack = new VerticalStackLayout { Spacing = 8 };

        var frame = new Frame
        {
            Padding = 10,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var innerStack = new VerticalStackLayout { Spacing = 6 };

        lblGameTitle = new Label
        {
            Text = "Diet Game",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblGameTitle);

        // Current Level Label
        lblCurrentLevel = new Label
        {
            Text = "Level 1",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE"),
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblCurrentLevel);

        // EXP Progress Bar
        expProgressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HeightRequest = 16,
            Margin = new Thickness(0, 2)
        };
        innerStack.Children.Add(expProgressBar);

        // EXP to Next Level Label
        lblExpToNext = new Label
        {
            Text = "0 / 100 EXP",
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblExpToNext);

        lblExpTotal = new Label
        {
            Text = "Total EXP: 0",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblExpTotal);

        // Dragon image below EXP info (smaller)
        var imgFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#DDD"),
            HeightRequest = 120
        };
        imgDragon = new Image { Aspect = Aspect.AspectFit };
        imgFrame.Content = imgDragon;
        innerStack.Children.Add(imgFrame);

        lblDragonTitle = new Label
        {
            Text = "Your Dragon",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblDragonTitle);

        lblDragonSubtitle = new Label
        {
            Text = "",
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblDragonSubtitle);

        lblDragonDesc = new Label
        {
            Text = "Dragon description",
            FontSize = 10,
            TextColor = Color.FromArgb("#999"),
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };
        innerStack.Children.Add(lblDragonDesc);

        btnDefineDragon = new Button
        {
            Text = "Define Dragon",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(6, 4),
            Margin = new Thickness(0, 4, 0, 0)
        };
        btnDefineDragon.Clicked += OnDefineDragonClicked;
        innerStack.Children.Add(btnDefineDragon);

        // Calculate EXP button - prominent, square-ish
        btnCalculateExp = new Button
        {
            Text = "Calculate EXP",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 12,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 80,
            Margin = new Thickness(0, 12, 0, 0)
        };
        btnCalculateExp.Clicked += OnCalculateClicked;
        innerStack.Children.Add(btnCalculateExp);

        frame.Content = innerStack;
        stack.Children.Add(frame);

        return stack;
    }

    private Frame BuildButtonPanel()
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        var btnClear = new Button
        {
            Text = "Clear Selection",
            BackgroundColor = Color.FromArgb("#999"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnClear.Clicked += OnClearSelectionClicked;
        stack.Children.Add(btnClear);

        var btnAdd = new Button
        {
            Text = "+ Add Activity",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnAdd.Clicked += OnAddActivityClicked;
        stack.Children.Add(btnAdd);

        var btnManage = new Button
        {
            Text = "Manage",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnManage.Clicked += OnManageClicked;
        stack.Children.Add(btnManage);

        var btnViewLog = new Button
        {
            Text = "📊 View Activity Log",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnViewLog.Clicked += OnViewLogClicked;
        stack.Children.Add(btnViewLog);

        // Add Conversation Practice button for conversation game
        if (GameId == "conversation_practice")
        {
            var btnConversation = new Button
            {
                Text = "💬 Conversation Practice",
                BackgroundColor = Color.FromArgb("#9C27B0"),
                TextColor = Colors.White,
                CornerRadius = 8,
                FontSize = 13,
                Margin = new Thickness(0, 8, 0, 0)
            };
            btnConversation.Clicked += OnConversationPracticeClicked;
            stack.Children.Add(btnConversation);
        }

        frame.Content = stack;
        return frame;
    }

    private ScrollView BuildChartsPanel()
    {
        var scrollView = new ScrollView();

        var stack = new VerticalStackLayout
        {
            Spacing = 12
        };

        // Meaningful Escalation Timer (NEW - at top)
        var escalationTimer = BuildEscalationTimerPanel();
        stack.Children.Add(escalationTimer);

        // Level Over Time Chart (MOVED UP - now before EXP chart)
        var levelChartFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var levelChartStack = new VerticalStackLayout { Spacing = 8 };

        var levelChartTitle = new Label
        {
            Text = "Level Over Time",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        levelChartStack.Children.Add(levelChartTitle);

        _levelChartView = new GraphicsView
        {
            HeightRequest = 250,
            Drawable = new LevelChartDrawable(_levelChartData)
        };
        levelChartStack.Children.Add(_levelChartView);

        levelChartFrame.Content = levelChartStack;
        stack.Children.Add(levelChartFrame);

        // EXP Over Time Chart (NOW BELOW Level chart)
        var expChartFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var expChartStack = new VerticalStackLayout { Spacing = 8 };

        var expChartTitle = new Label
        {
            Text = "Total EXP Over Time",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        expChartStack.Children.Add(expChartTitle);

        _expChartView = new GraphicsView
        {
            HeightRequest = 250,
            Drawable = new ExpChartDrawable(_expChartData)
        };
        expChartStack.Children.Add(_expChartView);

        expChartFrame.Content = expChartStack;
        stack.Children.Add(expChartFrame);

        scrollView.Content = stack;
        return scrollView;
    }
}
