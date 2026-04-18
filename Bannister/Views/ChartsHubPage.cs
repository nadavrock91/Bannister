using Bannister.Services;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

/// <summary>
/// Main hub page for all chart visualizations
/// </summary>
public class ChartsHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;

    public ChartsHubPage(AuthService auth, GameService games, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _exp = exp;
        _db = db;

        Title = "Charts";
        BackgroundColor = Color.FromArgb("#F5F5F5");

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
        mainStack.Children.Add(new Label
        {
            Text = "📊 Charts & Analytics",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        mainStack.Children.Add(new Label
        {
            Text = "Visualize your progress across all games",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Chart options
        mainStack.Children.Add(CreateChartButton(
            "📈 EXP Over Time",
            "See EXP progression for each game",
            Color.FromArgb("#4CAF50"),
            OnExpOverTimeClicked));

        mainStack.Children.Add(CreateChartButton(
            "🎚️ Level Over Time",
            "Track level progression for each game",
            Color.FromArgb("#2196F3"),
            OnLevelOverTimeClicked));

        mainStack.Children.Add(CreateChartButton(
            "🏆 All Games Comparison",
            "Compare levels across all games with monthly history",
            Color.FromArgb("#9C27B0"),
            OnAllGamesComparisonClicked));

        // Back button
        var btnBack = new Button
        {
            Text = "← Back to Home",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 45,
            Margin = new Thickness(0, 24, 0, 0)
        };
        btnBack.Clicked += async (s, e) => await Shell.Current.GoToAsync("..");
        mainStack.Children.Add(btnBack);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private Frame CreateChartButton(string title, string subtitle, Color color, EventHandler<TappedEventArgs> clickHandler)
    {
        var frame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = color,
            HasShadow = true,
            Margin = new Thickness(0, 4)
        };

        var stack = new VerticalStackLayout { Spacing = 4 };

        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        stack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Colors.White,
            Opacity = 0.9
        });

        frame.Content = stack;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += clickHandler;
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private async void OnExpOverTimeClicked(object? sender, TappedEventArgs e)
    {
        var page = new ExpOverTimeChartsPage(_auth, _games, _db);
        await Navigation.PushAsync(page);
    }

    private async void OnLevelOverTimeClicked(object? sender, TappedEventArgs e)
    {
        var page = new LevelOverTimeChartsPage(_auth, _games, _db);
        await Navigation.PushAsync(page);
    }

    private async void OnAllGamesComparisonClicked(object? sender, TappedEventArgs e)
    {
        var page = new AllGamesComparisonPage(_auth, _games, _exp, _db);
        await Navigation.PushAsync(page);
    }
}
