using Bannister.Services;

namespace Bannister.Views;

public class DisciplineHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly ResetEnforcerService _resetService;

    public DisciplineHubPage(
        AuthService auth,
        ResetEnforcerService resetService)
    {
        _auth = auth;
        _resetService = resetService;
        Title = "Discipline";

        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                (new Label
                {
                    Text = " Discipline",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true),
                (new Label
                {
                    Text = "Systems for maintaining discipline and " +
                           "accountability.",
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
                CreateHubCard(
                    "Consequences",
                    "Define and track the consequences of breaking " +
                    "your commitments.",
                    OnConsequencesTapped)
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private Frame CreateHubCard(
        string title, string subtitle,
        EventHandler<TappedEventArgs> tapped)
    {
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
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        textStack.Children.Add((new Label
        {
            Text = subtitle,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
        grid.Add(textStack, 0, 0);
        grid.Add((new Label
        {
            Text = "→",
            FontSize = 24,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true), 1, 0);

        var frame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true,
            Content = grid
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
        var tap = new TapGestureRecognizer();
        tap.Tapped += tapped;
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async void OnConsequencesTapped(
        object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
            new ConsequencesHubPage(_auth, _resetService));
    }
}
