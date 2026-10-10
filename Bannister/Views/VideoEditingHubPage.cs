using Bannister.Services;

namespace Bannister.Views;

public class VideoEditingHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly FFmpegService _ffmpeg;
    private readonly TruncatePresetService _truncatePresets;

    public VideoEditingHubPage(AuthService auth, FFmpegService ffmpeg, TruncatePresetService truncatePresets)
    {
        _auth = auth;
        _ffmpeg = ffmpeg;
        _truncatePresets = truncatePresets;

        Title = "Video Editing";
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
            Text = "Video Editing",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#00695C")
        });

        stack.Children.Add(new Label
        {
            Text = "Tools for cutting and preparing source videos.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(BuildHubCard(
            "\u2702\ufe0f Talking Head Shorts Cutter",
            "Pick a talking head video, import timestamped transcript analysis, review suggested shorts, and batch cut clips.",
            Color.FromArgb("#00695C"),
            async () => await Navigation.PushAsync(new TalkingHeadCutterPage(_auth, _ffmpeg, _truncatePresets))));

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
