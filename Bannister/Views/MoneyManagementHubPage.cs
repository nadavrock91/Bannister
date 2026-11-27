using Bannister.Services;

namespace Bannister.Views;

public class MoneyManagementHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly MoneyManagementService _moneyManagement;
    private readonly RecurringExpenditureService _recurringExpenditureService;

    public MoneyManagementHubPage(
        AuthService auth,
        MoneyManagementService moneyManagement,
        RecurringExpenditureService recurringExpenditureService)
    {
        _auth = auth;
        _moneyManagement = moneyManagement;
        _recurringExpenditureService = recurringExpenditureService;

        Title = "Money Management";


        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollView();
        var stack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        stack.Children.Add((new Label
        {
            Text = "Money Management",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Money planning tools",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(CreateHubCard(
            "Monthly Expenses",
            "Track recurring monthly expenses and totals.",
            ThemeKey.SuccessBackground,
            ThemeKey.SuccessText,
            OnMonthlyExpensesClicked));

        stack.Children.Add(CreateHubCard(
            "Assets",
            "Track assets, units, and total worth.",
            ThemeKey.InfoBackground,
            ThemeKey.InfoText,
            OnAssetsClicked));

        stack.Children.Add(CreateHubCard(
            "Data-Based Recurring Expenditures",
            "Trial-gate recurring costs — only keep what the data justifies.",
            ThemeKey.HighlightViolet,
            ThemeKey.HighlightVioletText,
            OnRecurringExpendituresClicked));

        scroll.Content = stack;
        Content = scroll;
    }

    private View CreateHubCard(string title, string subtitle, ThemeKey background, ThemeKey accent, EventHandler clicked)
    {
        var frame = (new Frame
        {
            Padding = 18,
            CornerRadius = 12,
            BorderColor = Colors.Transparent,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, background);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add((new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, accent, ThemeKey.PageText1, classifyPageText: true));
        textStack.Children.Add((new Label
        {
            Text = subtitle,
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        var button = (new Button
        {
            Text = "Open",
            CornerRadius = 8,
            Padding = new Thickness(18, 10),
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, accent).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        button.Clicked += clicked;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        frame.Content = grid;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => clicked(s, EventArgs.Empty);
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async void OnMonthlyExpensesClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new MoneyManagementPage(_auth, _moneyManagement));
    }

    private async void OnAssetsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AssetsPage(_auth, _moneyManagement));
    }

    private async void OnRecurringExpendituresClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new RecurringExpenditurePage(_auth, _recurringExpenditureService));
    }
}
