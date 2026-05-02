using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Hub page for Story Production - entry point from HomePage.
/// Provides access to Drafts (main production UI) and Production Stats.
/// </summary>
public class StoryProductionHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly IdeasService? _ideasService;
    private readonly IdeaLoggerService? _ideaLogger;
    private readonly SubActivityService? _subActivityService;
    
    private Label _statsLabel;

    public StoryProductionHubPage(AuthService auth, StoryProductionService storyService, IdeasService? ideasService = null, IdeaLoggerService? ideaLogger = null, SubActivityService? subActivityService = null)
    {
        _auth = auth;
        _storyService = storyService;
        _ideasService = ideasService;
        _ideaLogger = ideaLogger;
        _subActivityService = subActivityService;
        
        Title = "Story Production";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
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
        mainStack.Children.Add(new Label
        {
            Text = "🎬 Story Production",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#F57C00"),
            HorizontalOptions = LayoutOptions.Center
        });

        mainStack.Children.Add(new Label
        {
            Text = "Create and manage video production scripts",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        });

        // Stats summary
        _statsLabel = new Label
        {
            Text = "Loading...",
            FontSize = 13,
            TextColor = Color.FromArgb("#888"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        mainStack.Children.Add(_statsLabel);

        // Drafts button (main production UI)
        var draftsBtn = CreateMenuButton(
            "📝 Drafts",
            "Create and edit production scripts with visual breakdowns",
            Color.FromArgb("#FFF8E1"),
            Color.FromArgb("#F57C00"));
        draftsBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnDraftsClicked())
        });
        mainStack.Children.Add(draftsBtn);

        // Production Stats button
        var statsBtn = CreateMenuButton(
            "📊 Production Stats",
            "View completion rates, timelines, and productivity metrics",
            Color.FromArgb("#E3F2FD"),
            Color.FromArgb("#1565C0"));
        statsBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnStatsClicked())
        });
        mainStack.Children.Add(statsBtn);

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
            HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 6 };
        
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = textColor
        });
        
        stack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

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
        var page = new StoryProductionPage(_auth, _storyService, _ideasService, _ideaLogger, _subActivityService);
        await Navigation.PushAsync(page);
    }

    private async Task OnStatsClicked()
    {
        var page = new ProductionStatsPage(_auth, _storyService);
        await Navigation.PushAsync(page);
    }
}
