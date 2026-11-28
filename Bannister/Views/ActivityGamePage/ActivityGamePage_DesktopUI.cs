using Bannister.Drawables;
using Bannister.Services;
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

        SetPageContentWithBusyOverlay(mainGrid);
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

        var frame = (new Frame
        {
            Padding = 10,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var innerStack = new VerticalStackLayout { Spacing = 6 };

        lblGameTitle = new Label
        {
            Text = "Diet Game",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        innerStack.Children.Add(lblGameTitle);

        expBarView = new ExpBarView(
            _auth.CurrentUsername, "", ExpBarPresentation.CenteredCard);
        innerStack.Children.Add(expBarView);

        // Dragon image below EXP info (smaller)
        var imgFrame = (new Frame
        {
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            HeightRequest = 120
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
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

        lblDragonSubtitle = (new Label
        {
            Text = "",
            FontSize = 10,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true);
        innerStack.Children.Add(lblDragonSubtitle);

        lblDragonDesc = (new Label
        {
            Text = "Dragon description",
            FontSize = 10,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);
        innerStack.Children.Add(lblDragonDesc);

        btnDefineDragon = (new Button
        {
            Text = "Define Dragon",
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(6, 4),
            Margin = new Thickness(0, 4, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnDefineDragon.Clicked += OnDefineDragonClicked;
        innerStack.Children.Add(btnDefineDragon);

        // Calculate EXP button - prominent, square-ish
        btnCalculateExp = (new Button
        {
            Text = "Calculate EXP",
            CornerRadius = 12,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 80,
            Margin = new Thickness(0, 12, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnCalculateExp.Clicked += OnCalculateClicked;
        innerStack.Children.Add(btnCalculateExp);

        btnNextGame = (new Button
        {
            Text = "Next Game →",
            CornerRadius = 12,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 56,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        btnNextGame.Clicked += OnNextGameClicked;
        innerStack.Children.Add(btnNextGame);

        btnHome = (new Button
        {
            Text = " Home",
            CornerRadius = 12,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 56,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        btnHome.Clicked += OnHomeClicked;
        innerStack.Children.Add(btnHome);

        frame.Content = innerStack;
        stack.Children.Add(frame);

        return stack;
    }

    private Frame BuildButtonPanel()
    {
        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            Margin = new Thickness(0, 12, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 8 };

        var btnSelectAll = (new Button
        {
            Text = "Select All",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnSelectAll.Clicked += OnSelectAllClicked;
        stack.Children.Add(btnSelectAll);

        var btnClear = (new Button
        {
            Text = "Clear Selection",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnClear.Clicked += OnClearSelectionClicked;
        stack.Children.Add(btnClear);

        AddSetDisplayDaysButton(stack);

        var btnAdd = (new Button
        {
            Text = "+ Add Activity",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnAdd.Clicked += OnAddActivityClicked;
        stack.Children.Add(btnAdd);
        AddPendingActivityIdeaControls(stack);

        var btnManage = (new Button
        {
            Text = "Manage",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnManage.Clicked += OnManageClicked;
        stack.Children.Add(btnManage);

        var btnViewLog = (new Button
        {
            Text = "📊 View Activity Log",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnViewLog.Clicked += OnViewLogClicked;
        stack.Children.Add(btnViewLog);

        // Options button with context menu
        var btnOptions = (new Button
        {
            Text = "⚙️ Options",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnOptions.Clicked += OnOptionsClicked;
        stack.Children.Add(btnOptions);

        // Conversation Practice button removed - access now from HomePage

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

        // Level Caps Panel (at very top - shows activities that cap levels)
        var levelCapsPanel = BuildLevelCapsPanel();
        stack.Children.Add(levelCapsPanel);

        // Meaningful Escalation Timer (compact version)
        var escalationTimer = BuildEscalationTimerPanel();
        stack.Children.Add(escalationTimer);

        // Level Over Time Chart
        var levelChartFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

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
            HeightRequest = 200, // Reduced from 250
            Drawable = new LevelChartDrawable(_levelChartData)
        };
        levelChartStack.Children.Add(_levelChartView);

        levelChartFrame.Content = levelChartStack;
        stack.Children.Add(levelChartFrame);

        // EXP Over Time Chart
        var expChartFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

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
            HeightRequest = 200, // Reduced from 250
            Drawable = new ExpChartDrawable(_expChartData)
        };
        expChartStack.Children.Add(_expChartView);

        expChartFrame.Content = expChartStack;
        stack.Children.Add(expChartFrame);

        scrollView.Content = stack;
        return scrollView;
    }
}
