using Bannister.Services;

namespace Bannister.Views;

public class DeadlinesHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DeadlineService _deadlines;
    private readonly VerticalStackLayout _tilesStack = new() { Spacing = 12 };

    public DeadlinesHubPage(AuthService auth, DeadlineService deadlines)
    {
        _auth = auth;
        _deadlines = deadlines;
        Title = "Deadlines";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var root = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        root.Children.Add(new Label
        {
            Text = "Deadlines",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#6A1B9A")
        });
        root.Children.Add(new Label
        {
            Text = "Daily, weekly, monthly self-imposed deadlines.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });
        root.Children.Add(_tilesStack);
        Content = new ScrollView { Content = root };
    }

    private async Task RefreshAsync()
    {
        _tilesStack.Children.Clear();
        await AddTileAsync(DeadlineService.BucketDaily, "Daily Deadlines", "Due at the end of today.");
        await AddTileAsync(DeadlineService.BucketWeekly, "Weekly Deadlines", "Due by Saturday night.");
        await AddTileAsync(DeadlineService.BucketMonthly, "Monthly Deadlines", "Due by the last day of the month.");
    }

    private async Task AddTileAsync(int bucket, string title, string subtitle)
    {
        int active = await _deadlines.GetActiveCountAsync(_auth.CurrentUsername, bucket);
        int allowance = _deadlines.GetAllowance(_auth.CurrentUsername, bucket);

        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CE93D8"),
            CornerRadius = 8,
            HasShadow = false,
            Padding = 14
        };

        frame.Content = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#4A148C") },
                new Label { Text = $"{active} / {allowance} active", FontSize = 15, TextColor = active >= allowance ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32") },
                new Label { Text = subtitle, FontSize = 12, TextColor = Color.FromArgb("#666") }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PushAsync(new DeadlinesPage(_auth, _deadlines, bucket));
        frame.GestureRecognizers.Add(tap);
        _tilesStack.Children.Add(frame);
    }
}
