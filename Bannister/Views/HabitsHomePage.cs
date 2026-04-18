using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class HabitsHomePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly NewHabitService _newHabits;
    private readonly ExpService _exp;
    private readonly ActivityService _activities;
    private readonly DatabaseService _db;

    private Label _lblDailyCount;
    private Label _lblWeeklyCount;
    private Label _lblMonthlyCount;

    public HabitsHomePage(
        AuthService auth,
        GameService games,
        NewHabitService newHabits,
        ExpService exp,
        ActivityService activities,
        DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _newHabits = newHabits;
        _exp = exp;
        _activities = activities;
        _db = db;

        Title = "New Habits";
        BackgroundColor = Color.FromArgb("#667eea"); // Nice purple-blue gradient base

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCountsAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "🌱 New Habits",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        });

        mainStack.Children.Add(new Label
        {
            Text = "Build habits at your own pace.\nChoose a frequency to get started.",
            FontSize = 16,
            TextColor = Colors.White,
            Opacity = 0.9,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        });

        // Daily Habits Card
        var dailyCard = CreateFrequencyCard(
            "🌅 Daily Habits",
            "7 days to graduate",
            "#5C6BC0", // Indigo
            "Daily");
        _lblDailyCount = (Label)((VerticalStackLayout)((Frame)dailyCard).Content).Children[2];
        mainStack.Children.Add(dailyCard);

        // Weekly Habits Card
        var weeklyCard = CreateFrequencyCard(
            "📆 Weekly Habits",
            "4 weeks to graduate",
            "#00897B", // Teal
            "Weekly");
        _lblWeeklyCount = (Label)((VerticalStackLayout)((Frame)weeklyCard).Content).Children[2];
        mainStack.Children.Add(weeklyCard);

        // Monthly Habits Card
        var monthlyCard = CreateFrequencyCard(
            "📅 Monthly Habits",
            "3 months to graduate",
            "#9C27B0", // Purple
            "Monthly");
        _lblMonthlyCount = (Label)((VerticalStackLayout)((Frame)monthlyCard).Content).Children[2];
        mainStack.Children.Add(monthlyCard);

        // Info section
        var infoFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            Padding = 20,
            CornerRadius = 16,
            HasShadow = false,
            Margin = new Thickness(0, 20, 0, 0)
        };

        infoFrame.Content = new Label
        {
            Text = "💡 How it works:\n\n" +
                   "• Each frequency has its own allowance\n" +
                   "• Complete habits to earn more slots\n" +
                   "• Miss a habit = lose a slot (min 1)\n" +
                   "• Graduate habits to unlock more!",
            TextColor = Color.FromArgb("#3949AB"),
            FontSize = 14,
            LineHeight = 1.5
        };
        mainStack.Children.Add(infoFrame);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private Frame CreateFrequencyCard(string title, string subtitle, string color, string frequency)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb(color),
            Padding = 24,
            CornerRadius = 16,
            HasShadow = true
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await NavigateToFrequency(frequency);
        frame.GestureRecognizers.Add(tapGesture);

        var stack = new VerticalStackLayout { Spacing = 8 };

        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        stack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 14,
            TextColor = Colors.White,
            Opacity = 0.9
        });

        // Count label (will be updated)
        stack.Children.Add(new Label
        {
            Text = "0 active",
            FontSize = 16,
            TextColor = Colors.White,
            Opacity = 0.8,
            Margin = new Thickness(0, 8, 0, 0)
        });

        // Arrow indicator
        stack.Children.Add(new Label
        {
            Text = "Tap to manage →",
            FontSize = 12,
            TextColor = Colors.White,
            Opacity = 0.7,
            HorizontalTextAlignment = TextAlignment.End,
            Margin = new Thickness(0, 8, 0, 0)
        });

        frame.Content = stack;
        return frame;
    }

    private async Task LoadCountsAsync()
    {
        var activeHabits = await _newHabits.GetAllActiveHabitsAsync(_auth.CurrentUsername);

        int dailyCount = activeHabits.Count(h => h.Frequency == "Daily");
        int weeklyCount = activeHabits.Count(h => h.Frequency == "Weekly");
        int monthlyCount = activeHabits.Count(h => h.Frequency == "Monthly");

        _lblDailyCount.Text = dailyCount == 1 ? "1 active habit" : $"{dailyCount} active habits";
        _lblWeeklyCount.Text = weeklyCount == 1 ? "1 active habit" : $"{weeklyCount} active habits";
        _lblMonthlyCount.Text = monthlyCount == 1 ? "1 active habit" : $"{monthlyCount} active habits";
    }

    private async Task NavigateToFrequency(string frequency)
    {
        await Shell.Current.GoToAsync($"newhabits?frequency={frequency}");
    }
}
