using Bannister.Services;

namespace Bannister.Views;

public class EmotionalManagementHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly EmotionService _emotions;
    private readonly IdeasService _ideasService;

    public EmotionalManagementHubPage(AuthService auth, EmotionService emotions, IdeasService ideasService)
    {
        _auth = auth;
        _emotions = emotions;
        _ideasService = ideasService;
        Title = "Emotional Management";

        BuildUI();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        mainStack.Children.Add((new Label
        {
            Text = "\U0001F9E0 Emotional Management",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true));

        mainStack.Children.Add((new Label
        {
            Text = "Track, understand, and manage your emotional landscape.",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var activeBtn = CreateMenuButton(
            "\U0001F525 Active Emotions",
            "Track what you're feeling now. Add, edit, or archive emotions.",
            ThemeKey.WarningBackground,
            ThemeKey.WarningText);
        activeBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                var page = new ActiveEmotionsPage(_auth, _emotions, _ideasService);
                await Navigation.PushAsync(page);
            })
        });
        mainStack.Children.Add(activeBtn);

        Content = new ScrollView { Content = mainStack };
    }

    private Frame CreateMenuButton(string title, string subtitle, ThemeKey bgColor, ThemeKey textColor)
    {
        var frame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BorderColor = Colors.Transparent,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, bgColor);

        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add((new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, textColor, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = subtitle,
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));

        frame.Content = stack;
        return frame;
    }
}
