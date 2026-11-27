using Bannister.Services;

namespace Bannister.Views;

public class VideoProductionHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly PromptTechniqueService _promptTechniqueService;
    public VideoProductionHubPage(
        AuthService auth,
        PromptTechniqueService promptTechniqueService)
    {
        _auth = auth;
        _promptTechniqueService = promptTechniqueService;
        Title = "Video Production";

        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 14 };
        stack.Children.Add((new Label
        {
            Text = "Video Production",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightPinkText, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Tools for producing videos, testing prompts, and building connected generated sequences.",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(BuildHubCard(
            "Slideshow",
            "Turn a folder of images into an MP4 video with configurable slide duration and playback order.",
            ThemeKey.HighlightPinkText,
            async () => await Navigation.PushAsync(new SlideshowPage(_auth))));
        stack.Children.Add(BuildHubCard(
            "Prompt Technique Lab",
            "Track and compare prompting techniques for AI video generation. Log results, identify failure patterns, and discover what works.",
            ThemeKey.InfoText,
            async () => await Navigation.PushAsync(new PromptTechniqueLabPage(_auth, _promptTechniqueService))));
        Content = new ScrollView { Content = stack };
    }

    private static Frame BuildHubCard(string header, string subtitle, ThemeKey accent, Func<Task> onTap)
    {
        var frame = (new Frame
        {
            CornerRadius = 12,
            Padding = 16,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    (new Label { Text = header, FontSize = 16, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, accent, ThemeKey.PageText1, classifyPageText: true),
                    (new Label { Text = subtitle, FontSize = 13 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true)
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, accent);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        frame.GestureRecognizers.Add(tap);
        return frame;
    }
}
