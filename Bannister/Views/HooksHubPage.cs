using Bannister.Services;

namespace Bannister.Views;

public class HooksHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly HookWordService _hookWordService;
    private readonly CustomPromptService _customPrompts;
    private readonly CropPresetService _cropPresets;

    public HooksHubPage(
        AuthService auth,
        HookWordService hookWordService,
        CustomPromptService customPrompts,
        CropPresetService cropPresets)
    {
        _auth = auth;
        _hookWordService = hookWordService;
        _customPrompts = customPrompts;
        _cropPresets = cropPresets;

        Title = "Hooks Creation";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = " Hooks Creation",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222"),
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = "Choose a hook generation workflow.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666"),
                    HorizontalOptions = LayoutOptions.Center
                },
                CreateHubCard(
                    "Hooks from Random Words",
                    "4-stage variety amplifier using your persistent random word pool.",
                    OnRandomWordsTapped),
                CreateHubCard(
                    "Targeted Hooks",
                    "Generate scroll-stopping hooks for a specific topic or niche.",
                    OnTargetedHooksTapped)
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
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        grid.Add(textStack, 0, 0);
        grid.Add(new Label
        {
            Text = "→",
            FontSize = 24,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true,
            Content = grid
        };
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
            new TargetedHooksPage(_auth, _customPrompts, _cropPresets));
    }
}
