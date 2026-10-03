using Bannister.Services;

namespace Bannister.Views;

public class VideoGenerationHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly PromptTechniqueService _promptTechniqueService;

    public VideoGenerationHubPage(
        AuthService auth,
        PromptTechniqueService promptTechniqueService)
    {
        _auth = auth;
        _promptTechniqueService = promptTechniqueService;

        Title = "Video Generation";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        stack.Children.Add(new Label
        {
            Text = " Video Generation",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C2185B")
        });

        stack.Children.Add(new Label
        {
            Text = "Tools for generating videos from source content.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(BuildHubCard(
            "️ Slideshow",
            "Turn a folder of images into an MP4 video with configurable slide duration and playback order.",
            Color.FromArgb("#C2185B"),
            async () => await Navigation.PushAsync(new SlideshowPage(_auth))));

        stack.Children.Add(BuildHubCard(
            " Prompt Technique Lab",
            "Track and compare prompting techniques for AI video generation. Log results, identify failure patterns, and discover what works.",
            Color.FromArgb("#1565C0"),
            async () => await Navigation.PushAsync(new PromptTechniqueLabPage(_auth, _promptTechniqueService))));

        Content = new ScrollView { Content = stack };
    }

    private Frame BuildHubCard(string header, string subtitle, Color accent, Func<Task> onTap)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = accent,
            CornerRadius = 12,
            Padding = 16,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = header, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = accent },
                    new Label { Text = subtitle, FontSize = 13, TextColor = Color.FromArgb("#666") }
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        frame.GestureRecognizers.Add(tap);

        return frame;
    }
}
