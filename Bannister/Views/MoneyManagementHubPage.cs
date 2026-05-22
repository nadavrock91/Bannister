using Bannister.Services;

namespace Bannister.Views;

public class MoneyManagementHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MoneyManagementService _moneyManagement;

    public MoneyManagementHubPage(AuthService auth, MoneyManagementService moneyManagement)
    {
        _auth = auth;
        _moneyManagement = moneyManagement;

        Title = "Money Management";
        BackgroundColor = Color.FromArgb("#F5F5F5");

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

        stack.Children.Add(new Label
        {
            Text = "Money Management",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1B5E20")
        });

        stack.Children.Add(new Label
        {
            Text = "Money planning tools",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });

        stack.Children.Add(CreateHubCard(
            "Monthly Expenses",
            "Track recurring monthly expenses and totals.",
            Color.FromArgb("#E8F5E9"),
            Color.FromArgb("#1B5E20"),
            OnMonthlyExpensesClicked));

        scroll.Content = stack;
        Content = scroll;
    }

    private View CreateHubCard(string title, string subtitle, Color background, Color accent, EventHandler clicked)
    {
        var frame = new Frame
        {
            Padding = 18,
            CornerRadius = 12,
            BackgroundColor = background,
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

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
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = accent
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#555")
        });
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        var button = new Button
        {
            Text = "Open",
            BackgroundColor = accent,
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(18, 10),
            VerticalOptions = LayoutOptions.Center
        };
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
}
