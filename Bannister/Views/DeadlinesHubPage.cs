using Bannister.Services;

namespace Bannister.Views;

public class DeadlinesHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly DeadlineService _deadlines;
    private readonly VerticalStackLayout _tilesStack = new() { Spacing = 12 };

    public DeadlinesHubPage(AuthService auth, DeadlineService deadlines)
    {
        _auth = auth;
        _deadlines = deadlines;
        Title = "Deadlines";

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
        root.Children.Add((new Label
        {
            Text = "Deadlines",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.AccentPurpleStrongText, ThemeKey.PageText1, classifyPageText: true));
        root.Children.Add((new Label
        {
            Text = "Daily, weekly, monthly self-imposed deadlines.",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
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

        var frame = (new Frame
        {
            CornerRadius = 8,
            HasShadow = false,
            Padding = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.AccentPurpleBorder);

        frame.Content = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                (new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true),
                (new Label { Text = $"{active} / {allowance} active", FontSize = 15 }).ThemeColor(Label.TextColorProperty, active >= allowance ? ThemeKey.ErrorText : ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true),
                (new Label { Text = subtitle, FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true)
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PushAsync(new DeadlinesPage(_auth, _deadlines, bucket));
        frame.GestureRecognizers.Add(tap);
        _tilesStack.Children.Add(frame);
    }
}
