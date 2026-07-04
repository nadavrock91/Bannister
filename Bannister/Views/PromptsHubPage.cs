using Bannister.Services;

namespace Bannister.Views;

public class PromptsHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomPromptService _customPrompts;
    private readonly PromptService _prompts;
    private readonly IdeaLoggerService _ideaLogger;
    private readonly IdeasService _ideas;
    private readonly PromptLibraryService _libraryService;

    public PromptsHubPage(
        AuthService auth,
        CustomPromptService customPrompts,
        PromptService prompts,
        IdeaLoggerService ideaLogger,
        IdeasService ideas,
        PromptLibraryService libraryService)
    {
        _auth = auth;
        _customPrompts = customPrompts;
        _prompts = prompts;
        _ideaLogger = ideaLogger;
        _ideas = ideas;
        _libraryService = libraryService;

        Title = "Prompts";
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
            Text = " Prompts",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        stack.Children.Add(new Label
        {
            Text = "Prompt generation tools and library.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(BuildHubCard(
            "️ Prompts Generation",
            "Manage and use custom prompts for various tasks.",
            Color.FromArgb("#6A1B9A"),
            async () => await Navigation.PushAsync(new PromptsPage(_auth, _prompts, _ideaLogger, _ideas))));

        stack.Children.Add(BuildHubCard(
            " Prompts Library",
            "Browse and organize your prompt collection.",
            Color.FromArgb("#00838F"),
            async () => await Navigation.PushAsync(new PromptsLibraryPage(_auth, _libraryService))));

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
