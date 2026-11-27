using Bannister.Services;

namespace Bannister.Views;

public class VideoEditingHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly FFmpegService _ffmpeg;
    private readonly TruncatePresetService _truncatePresets;
    private readonly ShortsCutterProjectService _shortsProjects;

    public VideoEditingHubPage(AuthService auth, FFmpegService ffmpeg, TruncatePresetService truncatePresets, ShortsCutterProjectService shortsProjects)
    {
        _auth = auth;
        _ffmpeg = ffmpeg;
        _truncatePresets = truncatePresets;
        _shortsProjects = shortsProjects;

        Title = "Video Editing";


        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        stack.Children.Add((new Label
        {
            Text = "Video Editing",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightMintText, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Tools for cutting and preparing source videos.",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add(BuildHubCard(
            "\u2702\ufe0f Talking Head Shorts Cutter",
            "Pick a talking head video, import timestamped transcript analysis, review suggested shorts, and batch cut clips.",
            ThemeKey.HighlightMintText,
            async () => await Navigation.PushAsync(new TalkingHeadCutterPage(_auth, _ffmpeg, _truncatePresets, _shortsProjects))));

        stack.Children.Add(BuildHubCard(
            " Clip Continuation Comparator",
            "Pick a base clip and a folder of AI continuations. Play each base→continuation pair, remove the losers, loop until one remains.",
            ThemeKey.HighlightVioletText,
            async () => await Navigation.PushAsync(
                new ClipContinuationPickerPage(_ffmpeg))));

        Content = new ScrollView { Content = stack };
    }

    private Frame BuildHubCard(string header, string subtitle, ThemeKey accent, Func<Task> onTap)
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
