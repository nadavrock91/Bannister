using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class WritingProcessesPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly WritingExperimentService _experimentService;
    private readonly IdeaLoggerService? _ideaLogger;
    private VerticalStackLayout _listStack;
    private bool _processesExpanded;

    public WritingProcessesPage(AuthService auth, StoryProductionService storyService, WritingExperimentService experimentService, IdeaLoggerService? ideaLogger = null)
    {
        _auth = auth;
        _storyService = storyService;
        _experimentService = experimentService;
        _ideaLogger = ideaLogger;

        Title = "Writing Processes";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProcessesAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        mainStack.Children.Add((new Label
        {
            Text = "Writing Processes",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true));

        var experimentBtn = (new Button
        {
            Text = "\U0001F9EA Writing Experiment",
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 12, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        experimentBtn.Clicked += async (_, _) =>
        {
            var page = new WritingExperimentPage(_auth, _experimentService, _storyService, _ideaLogger);
            await Navigation.PushAsync(page);
        };
        mainStack.Children.Add(experimentBtn);

        mainStack.Children.Add((new Label
        {
            Text = "Manage the writing processes you can assign to story projects.",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var addBtn = (new Button
        {
            Text = "+ Add Process",
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Start
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addBtn.Clicked += OnAddClicked;
        mainStack.Children.Add(addBtn);

        _listStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_listStack);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadProcessesAsync()
    {
        _listStack.Children.Clear();

        var processes = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);

        var processesHeader = (new Label
        {
            Text = _processesExpanded
                ? $"\u25BC \U0001F4DD Processes ({processes.Count})"
                : $"\u25B6 \U0001F4DD Processes ({processes.Count})",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true);
        var headerTap = new TapGestureRecognizer();
        headerTap.Tapped += async (_, _) =>
        {
            _processesExpanded = !_processesExpanded;
            await LoadProcessesAsync();
        };
        processesHeader.GestureRecognizers.Add(headerTap);
        _listStack.Children.Add(processesHeader);

        if (!_processesExpanded) return;

        if (processes.Count == 0)
        {
            _listStack.Children.Add((new Label
            {
                Text = "No writing processes defined yet. Add one to get started.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 12)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var originalProjects = allProjects.Where(project => project.ParentProjectId == null).ToList();

        foreach (var process in processes)
        {
            var processFrame = (new Frame
            {
                Padding = 12,
                CornerRadius = 8,
                HasShadow = true,
                BorderColor = Colors.Transparent
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

            var processStack = new VerticalStackLayout { Spacing = 6 };
            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12
            };

            headerGrid.Add((new Label
            {
                Text = process.Name,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true), 0, 0);

            {
                var processProjects = originalProjects
                    .Where(project => string.Equals(
                        project.WritingProcess?.Trim(),
                        process.Name,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                int totalCount = processProjects.Count;
                var withStats = processProjects.Where(project =>
                    project.YouTubeStatsCapturedAt.HasValue ||
                    project.FacebookStatsCapturedAt.HasValue ||
                    project.TikTokStatsCapturedAt.HasValue).ToList();
                int statsCount = withStats.Count;
                var ytProjects = withStats.Where(project => project.YouTubeStatsCapturedAt.HasValue).ToList();
                var fbProjects = withStats.Where(project => project.FacebookStatsCapturedAt.HasValue).ToList();
                var ttProjects = withStats.Where(project => project.TikTokStatsCapturedAt.HasValue).ToList();

                var deleteButton = (new Button
                {
                    Text = "Delete",
                    CornerRadius = 8,
                    HeightRequest = 36,
                    FontSize = 12,
                    Padding = new Thickness(12, 0),
                    VerticalOptions = LayoutOptions.Center
                }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
                var capturedProcess = process;
                deleteButton.Clicked += async (_, _) =>
                    await DeleteProcessAsync(capturedProcess, totalCount);
                headerGrid.Add(deleteButton, 1, 0);

                processStack.Children.Add(headerGrid);
                string statsText = statsCount == 1 ? "1 has stats" : $"{statsCount} have stats";
                processStack.Children.Add((new Label
                {
                    Text = $"{totalCount} project(s) · {statsText}",
                    FontSize = 12
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));

                if (ytProjects.Count > 0)
                {
                    var topYt = ytProjects.OrderByDescending(p => p.YouTubeViews).First();
                    processStack.Children.Add(BuildPlatformStats(
                        "YouTube", ThemeKey.ErrorText, ytProjects.Count,
                        ytProjects.Average(p => p.YouTubeViews),
                        ytProjects.Average(p => p.YouTubeLikes),
                        ytProjects.Average(p => p.YouTubeComments),
                        ytProjects.Average(p => p.YouTubeAverageViewDurationSeconds),
                        topYt.Name, topYt.YouTubeViews, topYt.YouTubeLikes));
                }

                if (fbProjects.Count > 0)
                {
                    var topFb = fbProjects.OrderByDescending(p => p.FacebookViews).First();
                    processStack.Children.Add(BuildPlatformStats(
                        "Facebook", ThemeKey.InfoText, fbProjects.Count,
                        fbProjects.Average(p => p.FacebookViews),
                        fbProjects.Average(p => p.FacebookLikes),
                        fbProjects.Average(p => p.FacebookComments),
                        fbProjects.Average(p => p.FacebookAverageViewDurationSeconds),
                        topFb.Name, topFb.FacebookViews, topFb.FacebookLikes));
                }

                if (ttProjects.Count > 0)
                {
                    var topTt = ttProjects.OrderByDescending(p => p.TikTokViews).First();
                    processStack.Children.Add(BuildPlatformStats(
                        "TikTok", ThemeKey.CardText1, ttProjects.Count,
                        ttProjects.Average(p => p.TikTokViews),
                        ttProjects.Average(p => p.TikTokLikes),
                        ttProjects.Average(p => p.TikTokComments),
                        ttProjects.Average(p => p.TikTokAverageWatchTimeSeconds),
                        topTt.Name, topTt.TikTokViews, topTt.TikTokLikes));
                }

                if (statsCount == 0 && totalCount > 0)
                {
                    processStack.Children.Add((new Label
                    {
                        Text = "No stats logged yet for any project in this process.",
                        FontSize = 11,
                        FontAttributes = FontAttributes.Italic
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
                }
            }
            processFrame.Content = processStack;
            _listStack.Children.Add(processFrame);
        }
    }

    private static View BuildPlatformStats(
        string platform,
        ThemeKey color,
        int projectCount,
        double avgViews,
        double avgLikes,
        double avgComments,
        double avgDurationSeconds,
        string topProjectName,
        int topViews,
        int topLikes)
    {
        var stack = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };

        stack.Children.Add((new Label
        {
            Text = $"{platform} ({projectCount} logged)",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, color, ThemeKey.PageText1, classifyPageText: true));

        string durationDisplay = avgDurationSeconds >= 60
            ? $"{(int)(avgDurationSeconds / 60)}m {(int)(avgDurationSeconds % 60)}s"
            : $"{(int)avgDurationSeconds}s";

        stack.Children.Add((new Label
        {
            Text = $"Avg: {avgViews:F0} views · {avgLikes:F0} likes · {avgComments:F0} comments · {durationDisplay} avg watch",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        string topName = topProjectName.Length > 30 ? topProjectName[..27] + "..." : topProjectName;
        stack.Children.Add((new Label
        {
            Text = $"Top: \"{topName}\" ({topViews} views, {topLikes} likes)",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3, classifyPageText: true));

        return stack;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync(
            "New Writing Process",
            "Enter process name:",
            "Add",
            "Cancel",
            placeholder: "e.g., Fable, Video Essay, Documentary...");

        if (string.IsNullOrWhiteSpace(name)) return;

        // Check for duplicate
        var existing = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);
        if (existing.Any(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("Duplicate", $"A process named '{name.Trim()}' already exists.", "OK");
            return;
        }

        try
        {
            await _storyService.AddWritingProcessAsync(_auth.CurrentUsername, name.Trim());

            // Offer to add as idea under Story Production Processes category
            if (_ideaLogger != null)
            {
                bool addAsIdea = await DisplayAlert(
                    "Log as Idea?",
                    $"Open the idea logger to log '{name.Trim()}' under 'Story Production Processes'?",
                    "Yes",
                    "No");

                if (addAsIdea)
                {
                    await _ideaLogger.LogIdeaAsync(
                        new IdeaLogRequest { Username = _auth.CurrentUsername, Title = name.Trim(), Body = name.Trim(), Category = "Story Production Processes" },
                        IdeaLogMode.Interactive,
                        this);
                }
            }

            await LoadProcessesAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await DisplayAlert("Read Only", "Cannot add processes on a secondary device.", "OK");
        }
    }

    private async Task DeleteProcessAsync(WritingProcessDefinition process, int usageCount)
    {
        string message = usageCount > 0
            ? $"Delete '{process.Name}'?\n\nThis process is used by {usageCount} project(s). Those projects will keep their current process label but it won't appear in the defined list."
            : $"Delete '{process.Name}'?";

        bool confirm = await DisplayAlert("Delete Process", message, "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _storyService.DeleteWritingProcessAsync(process.Id);
            await LoadProcessesAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await DisplayAlert("Read Only", "Cannot delete processes on a secondary device.", "OK");
        }
    }
}
