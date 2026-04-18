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
        Content = scrollView;
    }

    private Frame BuildDragonCardCollapsed()
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

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
        lblCurrentLevel = new Label
        {
            Text = "Level 1",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE")
        };
        infoStack.Children.Add(lblCurrentLevel);

        // EXP Progress Bar
        expProgressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HeightRequest = 16
        };
        infoStack.Children.Add(expProgressBar);

        // EXP to Next Level
        lblExpToNext = new Label
        {
            Text = "0 / 100 EXP",
            FontSize = 10,
            TextColor = Color.FromArgb("#666")
        };
        infoStack.Children.Add(lblExpToNext);

        // Total EXP
        lblExpTotal = new Label
        {
            Text = "Total EXP: 0",
            FontSize = 11,
            TextColor = Color.FromArgb("#999")
        };
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
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Conversation Practice button removed - access now from HomePage

        btnCalculateExp = new Button
        {
            Text = "Calculate EXP",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnCalculateExp.Clicked += OnCalculateClicked;
        stack.Children.Add(btnCalculateExp);

        var btnSelectAll = new Button
        {
            Text = "Select All",
            BackgroundColor = Color.FromArgb("#7E57C2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnSelectAll.Clicked += OnSelectAllClicked;
        stack.Children.Add(btnSelectAll);

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

        // Options button with context menu
        var btnOptions = new Button
        {
            Text = "⚙️ Options",
            BackgroundColor = Color.FromArgb("#607D8B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnOptions.Clicked += OnOptionsClicked;
        stack.Children.Add(btnOptions);

        frame.Content = stack;
        return frame;
    }
}
