using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class WritingProcessesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly WritingExperimentService _experimentService;
    private readonly IdeaLoggerService? _ideaLogger;
    private VerticalStackLayout _listStack;

    public WritingProcessesPage(AuthService auth, StoryProductionService storyService, WritingExperimentService experimentService, IdeaLoggerService? ideaLogger = null)
    {
        _auth = auth;
        _storyService = storyService;
        _experimentService = experimentService;
        _ideaLogger = ideaLogger;

        Title = "Writing Processes";
        BackgroundColor = Color.FromArgb("#F5F5F5");

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

        mainStack.Children.Add(new Label
        {
            Text = "Writing Processes",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2")
        });

        var experimentBtn = new Button
        {
            Text = "\U0001F9EA Writing Experiment",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#6A1B9A"),
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 12, 0, 0)
        };
        experimentBtn.Clicked += async (_, _) =>
        {
            var page = new WritingExperimentPage(_auth, _experimentService, _storyService);
            await Navigation.PushAsync(page);
        };
        mainStack.Children.Add(experimentBtn);

        mainStack.Children.Add(new Label
        {
            Text = "Manage the writing processes you can assign to story projects.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var addBtn = new Button
        {
            Text = "+ Add Process",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Start
        };
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

        if (processes.Count == 0)
        {
            _listStack.Children.Add(new Label
            {
                Text = "No writing processes defined yet. Add one to get started.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 12)
            });
            return;
        }

        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var originalProjects = allProjects.Where(p => p.ParentProjectId == null).ToList();

        foreach (var process in processes)
        {
            var processProjects = originalProjects
                .Where(p => string.Equals(p.WritingProcess?.Trim(), process.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int totalCount = processProjects.Count;

            // Projects with any stats logged
            var withStats = processProjects.Where(p =>
                p.YouTubeStatsCapturedAt.HasValue ||
                p.FacebookStatsCapturedAt.HasValue ||
                p.TikTokStatsCapturedAt.HasValue).ToList();

            int statsCount = withStats.Count;

            // Compute averages per platform (only from projects that have that platform's stats)
            var ytProjects = withStats.Where(p => p.YouTubeStatsCapturedAt.HasValue).ToList();
            var fbProjects = withStats.Where(p => p.FacebookStatsCapturedAt.HasValue).ToList();
            var ttProjects = withStats.Where(p => p.TikTokStatsCapturedAt.HasValue).ToList();

            var row = new Frame
            {
                Padding = 12,
                CornerRadius = 8,
                BackgroundColor = Colors.White,
                HasShadow = true,
                BorderColor = Colors.Transparent
            };

            var cardStack = new VerticalStackLayout { Spacing = 6 };

            // Header row with name and delete
            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12
            };

            headerGrid.Add(new Label
            {
                Text = process.Name,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333")
            }, 0, 0);

            var deleteBtn = new Button
            {
                Text = "Delete",
                BackgroundColor = Color.FromArgb("#FFCDD2"),
                TextColor = Color.FromArgb("#C62828"),
                CornerRadius = 8,
                HeightRequest = 36,
                FontSize = 12,
                Padding = new Thickness(12, 0),
                VerticalOptions = LayoutOptions.Center
            };
            var capturedProcess = process;
            var capturedUsageCount = totalCount;
            deleteBtn.Clicked += async (_, _) => await DeleteProcessAsync(capturedProcess, capturedUsageCount);
            headerGrid.Add(deleteBtn, 1, 0);

            cardStack.Children.Add(headerGrid);

            // Project counts
            string statsText = statsCount == 1 ? "1 has stats" : $"{statsCount} have stats";
            cardStack.Children.Add(new Label
            {
                Text = $"{totalCount} project(s) · {statsText}",
                FontSize = 12,
                TextColor = Color.FromArgb("#888")
            });

            // Platform stats sections
            if (statsCount > 0)
            {
                if (ytProjects.Count > 0)
                {
                    var topYt = ytProjects.OrderByDescending(p => p.YouTubeViews).First();
                    double avgViews = ytProjects.Average(p => p.YouTubeViews);
                    double avgLikes = ytProjects.Average(p => p.YouTubeLikes);
                    double avgComments = ytProjects.Average(p => p.YouTubeComments);
                    double avgDuration = ytProjects.Average(p => p.YouTubeAverageViewDurationSeconds);

                    cardStack.Children.Add(BuildPlatformStats(
                        "YouTube",
                        "#C62828",
                        ytProjects.Count,
                        avgViews, avgLikes, avgComments, avgDuration,
                        topYt.Name, topYt.YouTubeViews, topYt.YouTubeLikes));
                }

                if (fbProjects.Count > 0)
                {
                    var topFb = fbProjects.OrderByDescending(p => p.FacebookViews).First();
                    double avgViews = fbProjects.Average(p => p.FacebookViews);
                    double avgLikes = fbProjects.Average(p => p.FacebookLikes);
                    double avgComments = fbProjects.Average(p => p.FacebookComments);
                    double avgDuration = fbProjects.Average(p => p.FacebookAverageViewDurationSeconds);

                    cardStack.Children.Add(BuildPlatformStats(
                        "Facebook",
                        "#1565C0",
                        fbProjects.Count,
                        avgViews, avgLikes, avgComments, avgDuration,
                        topFb.Name, topFb.FacebookViews, topFb.FacebookLikes));
                }

                if (ttProjects.Count > 0)
                {
                    var topTt = ttProjects.OrderByDescending(p => p.TikTokViews).First();
                    double avgViews = ttProjects.Average(p => p.TikTokViews);
                    double avgLikes = ttProjects.Average(p => p.TikTokLikes);
                    double avgComments = ttProjects.Average(p => p.TikTokComments);
                    double avgWatchTime = ttProjects.Average(p => p.TikTokAverageWatchTimeSeconds);

                    cardStack.Children.Add(BuildPlatformStats(
                        "TikTok",
                        "#000000",
                        ttProjects.Count,
                        avgViews, avgLikes, avgComments, avgWatchTime,
                        topTt.Name, topTt.TikTokViews, topTt.TikTokLikes));
                }
            }
            else if (totalCount > 0)
            {
                cardStack.Children.Add(new Label
                {
                    Text = "No stats logged yet for any project in this process.",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#AAA"),
                    FontAttributes = FontAttributes.Italic
                });
            }

            row.Content = cardStack;
            _listStack.Children.Add(row);
        }
    }

    private static View BuildPlatformStats(
        string platform,
        string color,
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

        stack.Children.Add(new Label
        {
            Text = $"{platform} ({projectCount} logged)",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(color)
        });

        string durationDisplay = avgDurationSeconds >= 60
            ? $"{(int)(avgDurationSeconds / 60)}m {(int)(avgDurationSeconds % 60)}s"
            : $"{(int)avgDurationSeconds}s";

        stack.Children.Add(new Label
        {
            Text = $"Avg: {avgViews:F0} views · {avgLikes:F0} likes · {avgComments:F0} comments · {durationDisplay} avg watch",
            FontSize = 11,
            TextColor = Color.FromArgb("#666")
        });

        string topName = topProjectName.Length > 30 ? topProjectName[..27] + "..." : topProjectName;
        stack.Children.Add(new Label
        {
            Text = $"Top: \"{topName}\" ({topViews} views, {topLikes} likes)",
            FontSize = 11,
            TextColor = Color.FromArgb("#444"),
            FontAttributes = FontAttributes.Italic
        });

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
                        this,
                        _auth.CurrentUsername,
                        name.Trim(),
                        "Story Production Processes");
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
