using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Hub page for Story Production - entry point from HomePage.
/// Provides access to Drafts (main production UI) and Production Stats.
/// </summary>
public class StoryProductionHubPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly IdeasService? _ideasService;
    private readonly IdeaLoggerService? _ideaLogger;
    private readonly SubActivityService? _subActivityService;
    private readonly CustomPromptService? _customPrompts;
    private readonly AssetLibraryService _assetLibraryService;
    private readonly AssetThumbnailService _assetThumbnailService;
    private readonly WritingExperimentService? _experimentService;
    private readonly ClipsExperimentService? _clipsExperimentService;
    private readonly VideoGenExperimentService? _videoGenExperimentService;
    
    private Label _statsLabel;

    public StoryProductionHubPage(AuthService auth, StoryProductionService storyService, AssetLibraryService assetLibraryService, AssetThumbnailService assetThumbnailService, IdeasService? ideasService = null, IdeaLoggerService? ideaLogger = null, SubActivityService? subActivityService = null, CustomPromptService? customPrompts = null, WritingExperimentService? experimentService = null, ClipsExperimentService? clipsExperimentService = null, VideoGenExperimentService? videoGenExperimentService = null)
    {
        _auth = auth;
        _storyService = storyService;
        _ideasService = ideasService;
        _ideaLogger = ideaLogger;
        _subActivityService = subActivityService;
        _customPrompts = customPrompts;
        _assetLibraryService = assetLibraryService;
        _assetThumbnailService = assetThumbnailService;
        _experimentService = experimentService;
        _clipsExperimentService = clipsExperimentService;
        _videoGenExperimentService = videoGenExperimentService;
        
        Title = "Story Production";

        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStatsAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };

        // Header
        mainStack.Children.Add((new Label
        {
            Text = "🎬 Story Production",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true));

        mainStack.Children.Add((new Label
        {
            Text = "Create and manage video production scripts",
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        // Stats summary
        _statsLabel = (new Label
        {
            Text = "Loading...",
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 10)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true);
        mainStack.Children.Add(_statsLabel);

        // Drafts button (main production UI)
        var draftsBtn = CreateMenuButton(
            "📝 Drafts",
            "Create and edit production scripts with visual breakdowns",
            ThemeKey.HighlightYellow,
            ThemeKey.WarningText);
        draftsBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnDraftsClicked())
        });
        mainStack.Children.Add(draftsBtn);

        var clipsBtn = CreateMenuButton(
            "\U0001F3AC Clips Creation",
            "Manage parallel clip creation tasks across all shots",
            ThemeKey.InfoBackground,
            ThemeKey.InfoText);
        clipsBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnClipsClicked())
        });
        mainStack.Children.Add(clipsBtn);

        // Production Stats button
        var statsBtn = CreateMenuButton(
            "📊 Production Stats",
            "View completion rates, timelines, and productivity metrics",
            ThemeKey.InfoBackground,
            ThemeKey.InfoText);
        statsBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnStatsClicked())
        });
        mainStack.Children.Add(statsBtn);

        // Asset Library button
        var assetLibraryBtn = CreateMenuButton(
            "📁 Asset Library",
            "Index and categorize reusable image and video files",
            ThemeKey.HighlightTeal,
            ThemeKey.HighlightTealText);
        assetLibraryBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnAssetLibraryClicked())
        });
        mainStack.Children.Add(assetLibraryBtn);

        // Writing Processes button
        var processesBtn = CreateMenuButton(
            "Writing Processes",
            "Define and manage writing process types for your projects",
            ThemeKey.HighlightViolet,
            ThemeKey.HighlightVioletText);
        processesBtn.GestureRecognizers.Clear();
        processesBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnProcessesClicked())
        });
        mainStack.Children.Add(processesBtn);

        var clipsExperimentBtn = CreateMenuButton(
            "🎬 AI Clips Experiment",
            "Compare short-video hook concepts by viewer retention",
            ThemeKey.InfoBackground,
            ThemeKey.InfoText);
        clipsExperimentBtn.GestureRecognizers.Clear();
        clipsExperimentBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnClipsExperimentClicked())
        });
        mainStack.Children.Add(clipsExperimentBtn);

        var videoGenBtn = CreateMenuButton(
            "🎥 Video Generation Methods",
            "Test production methods and compare short-video retention",
            ThemeKey.SuccessBackground, ThemeKey.SuccessText);
        videoGenBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnVideoGenExperimentClicked())
        });
        mainStack.Children.Add(videoGenBtn);

        Content = new ScrollView { Content = mainStack };
    }

    private Frame CreateMenuButton(string title, string subtitle, ThemeKey bgColor, ThemeKey textColor)
    {
        var frame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BorderColor = Colors.Transparent,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, bgColor);

        var stack = new VerticalStackLayout { Spacing = 6 };
        
        stack.Children.Add((new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, textColor, ThemeKey.PageText1, classifyPageText: true));
        
        stack.Children.Add((new Label
        {
            Text = subtitle,
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        frame.Content = stack;
        return frame;
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var projects = await _storyService.GetActiveProjectsAsync(_auth.CurrentUsername);
            var originalProjects = projects.Where(p => p.ParentProjectId == null).ToList();
            
            int totalProjects = originalProjects.Count;
            int totalDrafts = projects.Count;
            
            int totalLines = 0;
            int preparedLines = 0;
            
            foreach (var project in projects)
            {
                var (total, prepared) = await _storyService.GetProjectStatsAsync(project.Id);
                totalLines += total;
                preparedLines += prepared;
            }

            if (totalProjects == 0)
            {
                _statsLabel.Text = "No projects yet";
            }
            else
            {
                int percent = totalLines > 0 ? (int)Math.Round(100.0 * preparedLines / totalLines) : 0;
                _statsLabel.Text = $"{totalProjects} project(s) • {totalDrafts} draft(s) • {preparedLines}/{totalLines} visuals ({percent}%)";
            }
        }
        catch
        {
            _statsLabel.Text = "";
        }
    }

    private async Task OnDraftsClicked()
    {
        var page = new StoryProductionPage(_auth, _storyService, _ideasService, _ideaLogger, _subActivityService, _customPrompts);
        await Navigation.PushAsync(page);
    }

    private async Task OnStatsClicked()
    {
        var page = new ProductionStatsPage(_auth, _storyService);
        await Navigation.PushAsync(page);
    }

    private async Task OnClipsClicked()
    {
        var page = new ClipsCreationPage(_auth, _storyService);
        await Navigation.PushAsync(page);
    }

    private async Task OnAssetLibraryClicked()
    {
        var page = new AssetLibraryPage(_auth, _assetLibraryService, _assetThumbnailService);
        await Navigation.PushAsync(page);
    }

    private async Task OnProcessesClicked()
    {
        var experimentService = _experimentService ?? Handler?.MauiContext?.Services.GetService<WritingExperimentService>();
        if (experimentService == null)
        {
            await DisplayAlert("Unavailable", "Writing Experiment service is unavailable.", "OK");
            return;
        }

        var page = new WritingProcessesPage(_auth, _storyService, experimentService, _ideaLogger);
        await Navigation.PushAsync(page);
    }

    private async Task OnClipsExperimentClicked()
    {
        var service = _clipsExperimentService
            ?? Handler?.MauiContext?.Services
                .GetService<ClipsExperimentService>();
        if (service == null)
        {
            await DisplayAlert("Unavailable", "Clips Experiment service is unavailable.", "OK");
            return;
        }

        await Navigation.PushAsync(new ClipsExperimentPage(service, _auth));
    }

    private async Task OnVideoGenExperimentClicked()
    {
        var service = _videoGenExperimentService
            ?? Handler?.MauiContext?.Services.GetService<VideoGenExperimentService>();
        if (service == null)
        {
            await DisplayAlert("Unavailable", "Video generation experiment service is unavailable.", "OK");
            return;
        }
        await Navigation.PushAsync(new VideoGenExperimentPage(service, _auth));
    }
}
