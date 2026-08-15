using Bannister.Services;

namespace Bannister.Views;

public class EmotionalManagementHubPage : ContentPage
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
        BackgroundColor = Color.FromArgb("#FFF8E1");
        BuildUI();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        mainStack.Children.Add(new Label
        {
            Text = "\U0001F9E0 Emotional Management",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100")
        });

        mainStack.Children.Add(new Label
        {
            Text = "Track, understand, and manage your emotional landscape.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });

        var activeBtn = CreateMenuButton(
            "\U0001F525 Active Emotions",
            "Track what you're feeling now. Add, edit, or archive emotions.",
            Color.FromArgb("#FFF3E0"),
            Color.FromArgb("#E65100"));
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

    private Frame CreateMenuButton(string title, string subtitle, Color bgColor, Color textColor)
    {
        var frame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = bgColor,
            BorderColor = Colors.Transparent,
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = textColor
        });
        stack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#888")
        });

        frame.Content = stack;
        return frame;
    }
}
