using Bannister.Services;

namespace Bannister.Views;

public class MusicProductionHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly MusicProductionService _musicService;
    private readonly DatabaseService _db;
    private readonly IdeasService _ideas;
    private readonly CustomPromptService _customPrompts;

    public MusicProductionHubPage(AuthService auth, MusicProductionService musicService, DatabaseService db, IdeasService ideas, CustomPromptService customPrompts)
    {
        _auth = auth;
        _musicService = musicService;
        _db = db;
        _ideas = ideas;
        _customPrompts = customPrompts;

        Title = "Music Production";


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
            Text = "Music Production",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoButton, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Music planning tools",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(CreateHubCard(
            "Music for Stories",
            "Plan music cues alongside script and visuals.",
            ThemeKey.HighlightIndigo,
            ThemeKey.HighlightIndigoButton,
            OnMusicForStoriesClicked));

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
        }).ThemeColor(VisualElement.BackgroundColorProperty, accent).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        button.Clicked += clicked;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        frame.Content = grid;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => clicked(s, EventArgs.Empty);
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async void OnMusicForStoriesClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new MusicForStoriesPage(_auth, _musicService, _db, _ideas, _customPrompts));
    }
}
