using Bannister.Services;

namespace Bannister.Views;

public class HooksHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly HookWordService _hookWordService;
    private readonly CustomPromptService _customPrompts;
    private readonly CropPresetService _cropPresets;
    private readonly IPanelSaver _panelSaver;
    private readonly DoNotService _doNotService;
    private readonly HookPrefixService _hookPrefixes;
    private readonly AppSettingsService _appSettings;

    public HooksHubPage(
        AuthService auth,
        HookWordService hookWordService,
        CustomPromptService customPrompts,
        CropPresetService cropPresets,
        IPanelSaver panelSaver,
        DoNotService doNotService,
        HookPrefixService hookPrefixes,
        AppSettingsService appSettings)
    {
        _auth = auth;
        _hookWordService = hookWordService;
        _customPrompts = customPrompts;
        _cropPresets = cropPresets;
        _panelSaver = panelSaver;
        _doNotService = doNotService;
        _hookPrefixes = hookPrefixes;
        _appSettings = appSettings;

        Title = "Hooks Creation";


        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                (new Label
                {
                    Text = " Hooks Creation",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                (new Label
                {
                    Text = "Choose a hook generation workflow.",
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
                CreateHubCard(
                    "Hooks from Random Words",
                    "4-stage variety amplifier using your persistent random word pool.",
                    OnRandomWordsTapped),
                CreateHubCard(
                    "Targeted Hooks",
                    "Generate scroll-stopping hooks for a specific topic or niche.",
                    OnTargetedHooksTapped),
                CreateHubCard(
                    "Opening Clip Prompts",
                    "Generate 5 Grok video-prompt variations from a starting frame " +
                    "to maximise 10-second retention.",
                    OnOpeningClipPromptsTapped)
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private Frame CreateHubCard(string title, string subtitle, EventHandler<TappedEventArgs> tapped)
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
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
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
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true), 1, 0);

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

    private async void OnRandomWordsTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new HooksFromRandomWordsPage(_auth, _hookWordService));
    }

    private async void OnTargetedHooksTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
            new TargetedHooksPage(
                _auth,
                _cropPresets,
                _panelSaver,
                _hookPrefixes,
                _appSettings));
    }

    private async void OnOpeningClipPromptsTapped(
        object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
            new OpeningClipPromptPage(_auth, _customPrompts, _doNotService));
    }
}
