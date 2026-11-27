using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Partial class containing mobile UI building methods
/// </summary>
public partial class ActivityGamePage
{
    private void BuildMobileUI()
    {
        var scrollView = new ScrollView();
        var stack = new VerticalStackLayout
        {
            Padding = 12,
            Spacing = 12
        };

        // Dragon panel (collapsed on mobile)
        var dragonCard = BuildDragonCardCollapsed();
        stack.Children.Add(dragonCard);

        // Action buttons
        var buttonPanel = BuildMobileButtonPanel();
        stack.Children.Add(buttonPanel);

        // Charts (before activities on mobile so they're visible)
        var chartsPanel = BuildChartsPanel();
        stack.Children.Add(chartsPanel);

        // Activities panel
        var activitiesPanel = BuildActivitiesPanel();
        stack.Children.Add(activitiesPanel);

        scrollView.Content = stack;
        SetPageContentWithBusyOverlay(scrollView);
    }

    private Frame BuildDragonCardCollapsed()
    {
        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        // Small dragon image
        imgDragon = new Image 
        { 
            Aspect = Aspect.AspectFit,
            WidthRequest = 80,
            HeightRequest = 80
        };
        Grid.SetColumn(imgDragon, 0);
        grid.Children.Add(imgDragon);

        // Info stack
        var infoStack = new VerticalStackLayout { Spacing = 4 };

        lblGameTitle = new Label
        {
            Text = "Diet Game",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        };
        infoStack.Children.Add(lblGameTitle);

        // Current Level
        lblCurrentLevel = (new Label
        {
            Text = "Level 1",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText1, classifyPageText: true);
        infoStack.Children.Add(lblCurrentLevel);

        // EXP Progress Bar
        expProgressBar = (new ProgressBar
        {
            Progress = 0,
            HeightRequest = 16
        }).ThemeColor(ProgressBar.ProgressColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator);
        infoStack.Children.Add(expProgressBar);

        // EXP to Next Level
        lblExpToNext = (new Label
        {
            Text = "0 / 100 EXP",
            FontSize = 10
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true);
        infoStack.Children.Add(lblExpToNext);

        // Total EXP
        lblExpTotal = (new Label
        {
            Text = "Total EXP: 0",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);
        infoStack.Children.Add(lblExpTotal);

        lblDragonTitle = new Label
        {
            Text = "Your Dragon",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold
        };
        infoStack.Children.Add(lblDragonTitle);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Hidden labels (needed for binding but not displayed on mobile)
        lblDragonSubtitle = new Label { IsVisible = false };
        lblDragonDesc = new Label { IsVisible = false };
        btnDefineDragon = new Button { IsVisible = false };

        frame.Content = grid;
        return frame;
    }

    private Frame BuildMobileButtonPanel()
    {
        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Conversation Practice button removed - access now from HomePage

        btnCalculateExp = (new Button
        {
            Text = "Calculate EXP",
            CornerRadius = 8,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnCalculateExp.Clicked += OnCalculateClicked;
        stack.Children.Add(btnCalculateExp);

        btnNextGame = (new Button
        {
            Text = "Next Game →",
            CornerRadius = 8,
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        btnNextGame.Clicked += OnNextGameClicked;
        stack.Children.Add(btnNextGame);

        btnHome = (new Button
        {
            Text = " Home",
            CornerRadius = 8,
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        btnHome.Clicked += OnHomeClicked;
        stack.Children.Add(btnHome);

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

        frame.Content = stack;
        return frame;
    }
}
