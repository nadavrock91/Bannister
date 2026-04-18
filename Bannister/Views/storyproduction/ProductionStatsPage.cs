using Bannister.Services;
using Bannister.Models;

namespace Bannister.Views;

/// <summary>
/// Page for viewing production statistics - publication status and timelines.
/// </summary>
public class ProductionStatsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    
    private VerticalStackLayout _statsStack;
    private Grid _loadingOverlay;

    public ProductionStatsPage(AuthService auth, StoryProductionService storyService)
    {
        _auth = auth;
        _storyService = storyService;
        
        Title = "Production Stats";
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
        var mainGrid = new Grid();

        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        // Header row with export button
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        headerRow.Children.Add(new Label
        {
            Text = "📊 Production Stats",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        var exportBtn = new Button
        {
            Text = "📤 Export",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            FontSize = 13,
            HeightRequest = 36,
            CornerRadius = 8,
            Padding = new Thickness(12, 0)
        };
        exportBtn.Clicked += OnExportClicked;
        Grid.SetColumn(exportBtn, 1);
        headerRow.Children.Add(exportBtn);

        mainStack.Children.Add(headerRow);

        _statsStack = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_statsStack);

        scrollView.Content = mainStack;
        mainGrid.Children.Add(scrollView);

        // Loading overlay
        _loadingOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#80000000")
        };
        _loadingOverlay.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            WidthRequest = 50,
            HeightRequest = 50,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        });
        mainGrid.Children.Add(_loadingOverlay);

        Content = mainGrid;
    }

    private async Task LoadStatsAsync()
    {
        _loadingOverlay.IsVisible = true;

        try
        {
            _statsStack.Children.Clear();

            var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
            var originalProjects = allProjects.Where(p => p.ParentProjectId == null).OrderByDescending(p => p.CreatedAt).ToList();

            if (originalProjects.Count == 0)
            {
                _statsStack.Children.Add(new Label
                {
                    Text = "No projects yet",
                    TextColor = Color.FromArgb("#999"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40)
                });
                return;
            }

            // Published section
            var published = originalProjects.Where(p => p.IsPublished).ToList();
            if (published.Count > 0)
            {
                _statsStack.Children.Add(CreateSectionHeader($"✅ Published ({published.Count})"));
                foreach (var project in published)
                {
                    _statsStack.Children.Add(await BuildProjectCardAsync(project));
                }
            }

            // Not yet published section
            var notPublished = originalProjects.Where(p => !p.IsPublished).ToList();
            if (notPublished.Count > 0)
            {
                _statsStack.Children.Add(CreateSectionHeader($"🚧 Not Yet Published ({notPublished.Count})"));
                foreach (var project in notPublished)
                {
                    _statsStack.Children.Add(await BuildProjectCardAsync(project));
                }
            }
        }
        catch (Exception ex)
        {
            _statsStack.Children.Add(new Label
            {
                Text = $"Error: {ex.Message}",
                TextColor = Colors.Red
            });
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    private Label CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 8, 0, 4)
        };
    }

    private async Task<Frame> BuildProjectCardAsync(StoryProject project)
    {
        var lines = await _storyService.GetLinesAsync(project.Id);
        int clipCount = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            clipCount += shots.Count > 0 ? shots.Count : 1;
        }

        var card = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = project.IsPublished ? Color.FromArgb("#E8F5E9") : Colors.White,
            BorderColor = project.IsPublished ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Title row with status and settings
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var titleStack = new HorizontalStackLayout { Spacing = 8 };
        titleStack.Children.Add(new Label
        {
            Text = project.IsPublished ? "✅" : "🚧",
            FontSize = 16
        });
        titleStack.Children.Add(new Label
        {
            Text = project.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        Grid.SetColumn(titleStack, 0);
        titleRow.Children.Add(titleStack);

        var settingsBtn = new Button
        {
            Text = "⚙️",
            BackgroundColor = Colors.Transparent,
            FontSize = 16,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0
        };
        settingsBtn.Clicked += async (s, e) => await ShowProjectSettingsAsync(project);
        Grid.SetColumn(settingsBtn, 1);
        titleRow.Children.Add(settingsBtn);

        stack.Children.Add(titleRow);

        // Stats grid
        var statsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 4,
            ColumnSpacing = 12
        };

        // Row 0: Start Date | Published Date
        AddStatCell(statsGrid, 0, 0, "Started", project.CreatedAt.ToString("MMM d, yyyy"));
        if (project.IsPublished && project.PublishedAt.HasValue)
        {
            AddStatCell(statsGrid, 0, 1, "Published", project.PublishedAt.Value.ToString("MMM d, yyyy"));
        }
        else
        {
            AddStatCell(statsGrid, 0, 1, "Published", "—");
        }

        // Row 1: Days Taken | Projected Days
        int actualDays = project.ActualDays;
        string daysText = project.IsPublished ? $"{actualDays} days" : $"{actualDays} days (ongoing)";
        AddStatCell(statsGrid, 1, 0, "Duration", daysText);
        
        if (project.ProjectedDays > 0)
        {
            string projDaysText = $"{project.ProjectedDays} days";
            if (project.IsPublished)
            {
                int diff = actualDays - project.ProjectedDays;
                if (diff > 0) projDaysText += $" (+{diff})";
                else if (diff < 0) projDaysText += $" ({diff})";
            }
            AddStatCell(statsGrid, 1, 1, "Projected", projDaysText);
        }
        else
        {
            AddStatCell(statsGrid, 1, 1, "Projected", "—");
        }

        // Row 2: Clips | Projected Clips
        string clipsText = project.IsPublished ? $"{project.FinalClipCount} clips" : $"{clipCount} clips";
        AddStatCell(statsGrid, 2, 0, "Clips", clipsText);
        
        if (project.ProjectedClipCount > 0)
        {
            string projClipsText = $"{project.ProjectedClipCount} clips";
            if (project.IsPublished && project.FinalClipCount > 0)
            {
                int diff = project.FinalClipCount - project.ProjectedClipCount;
                if (diff > 0) projClipsText += $" (+{diff})";
                else if (diff < 0) projClipsText += $" ({diff})";
            }
            AddStatCell(statsGrid, 2, 1, "Projected", projClipsText);
        }
        else
        {
            AddStatCell(statsGrid, 2, 1, "Projected", "—");
        }

        stack.Children.Add(statsGrid);

        card.Content = stack;
        return card;
    }

    private void AddStatCell(Grid grid, int row, int col, string label, string value)
    {
        var stack = new VerticalStackLayout { Spacing = 0 };
        stack.Children.Add(new Label
        {
            Text = label,
            FontSize = 10,
            TextColor = Color.FromArgb("#999")
        });
        stack.Children.Add(new Label
        {
            Text = value,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, col);
        grid.Children.Add(stack);
    }

    private async Task ShowProjectSettingsAsync(StoryProject project)
    {
        var options = new List<string>();
        
        options.Add("📅 Edit Start Date");
        
        if (!project.IsPublished)
        {
            options.Add("✅ Mark as Published");
            options.Add("📐 Set Projections");
        }
        else
        {
            options.Add("📅 Edit Published Date");
            options.Add("✏️ Edit Clip Count");
            options.Add("↩️ Unpublish");
        }

        var action = await DisplayActionSheet(project.Name, "Cancel", null, options.ToArray());
        if (action == null || action == "Cancel") return;

        if (action == "📅 Edit Start Date")
        {
            await EditStartDateAsync(project);
        }
        else if (action == "📅 Edit Published Date")
        {
            await EditPublishedDateAsync(project);
        }
        else if (action == "✅ Mark as Published")
        {
            await PublishProjectAsync(project);
        }
        else if (action == "📐 Set Projections")
        {
            await SetProjectionsAsync(project);
        }
        else if (action == "↩️ Unpublish")
        {
            bool confirm = await DisplayAlert("Unpublish", $"Revert '{project.Name}' to unpublished?", "Yes", "No");
            if (confirm)
            {
                await _storyService.UnpublishProjectAsync(project.Id);
                await LoadStatsAsync();
            }
        }
        else if (action == "✏️ Edit Clip Count")
        {
            string? clipStr = await DisplayPromptAsync("Edit Clip Count", "Final clip count:", "Save", "Cancel", 
                initialValue: project.FinalClipCount.ToString(), keyboard: Keyboard.Numeric);
            if (!string.IsNullOrEmpty(clipStr) && int.TryParse(clipStr, out int clips))
            {
                project.FinalClipCount = clips;
                await _storyService.UpdateProjectAsync(project);
                await LoadStatsAsync();
            }
        }
    }

    private async Task EditStartDateAsync(StoryProject project)
    {
        // Show current date and ask for new one
        string currentDate = project.CreatedAt.ToString("yyyy-MM-dd");
        string? newDateStr = await DisplayPromptAsync(
            "Edit Start Date", 
            $"Current: {project.CreatedAt:MMM d, yyyy}\n\nEnter new date (YYYY-MM-DD):", 
            "Save", "Cancel", 
            initialValue: currentDate);
        
        if (string.IsNullOrEmpty(newDateStr)) return;
        
        if (DateTime.TryParse(newDateStr, out DateTime newDate))
        {
            project.CreatedAt = newDate;
            await _storyService.UpdateProjectAsync(project);
            await LoadStatsAsync();
        }
        else
        {
            await DisplayAlert("Invalid Date", "Please use format YYYY-MM-DD (e.g., 2026-04-01)", "OK");
        }
    }

    private async Task EditPublishedDateAsync(StoryProject project)
    {
        if (!project.PublishedAt.HasValue) return;
        
        string currentDate = project.PublishedAt.Value.ToString("yyyy-MM-dd");
        string? newDateStr = await DisplayPromptAsync(
            "Edit Published Date", 
            $"Current: {project.PublishedAt.Value:MMM d, yyyy}\n\nEnter new date (YYYY-MM-DD):", 
            "Save", "Cancel", 
            initialValue: currentDate);
        
        if (string.IsNullOrEmpty(newDateStr)) return;
        
        if (DateTime.TryParse(newDateStr, out DateTime newDate))
        {
            project.PublishedAt = newDate;
            project.CompletedAt = newDate;
            await _storyService.UpdateProjectAsync(project);
            await LoadStatsAsync();
        }
        else
        {
            await DisplayAlert("Invalid Date", "Please use format YYYY-MM-DD (e.g., 2026-04-08)", "OK");
        }
    }

    private async Task PublishProjectAsync(StoryProject project)
    {
        // Get lines to count clips
        var lines = await _storyService.GetLinesAsync(project.Id);
        int clipCount = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            clipCount += shots.Count > 0 ? shots.Count : 1;
        }

        string? clipStr = await DisplayPromptAsync("Publish", "Final clip count:", "Publish", "Cancel", 
            initialValue: clipCount.ToString(), keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrEmpty(clipStr)) return;
        if (!int.TryParse(clipStr, out int finalClips)) return;

        await _storyService.PublishProjectAsync(project.Id, finalClips);
        await LoadStatsAsync();
    }

    private async Task SetProjectionsAsync(StoryProject project)
    {
        string? clipsStr = await DisplayPromptAsync("Projected Clips", "Expected number of clips:", "Next", "Cancel",
            initialValue: project.ProjectedClipCount > 0 ? project.ProjectedClipCount.ToString() : "", keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrEmpty(clipsStr)) return;
        if (!int.TryParse(clipsStr, out int projectedClips)) return;

        string? daysStr = await DisplayPromptAsync("Projected Days", "Expected production days:", "Save", "Cancel",
            initialValue: project.ProjectedDays > 0 ? project.ProjectedDays.ToString() : "", keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrEmpty(daysStr)) return;
        if (!int.TryParse(daysStr, out int projectedDays)) return;

        await _storyService.SetProjectionsAsync(project.Id, projectedClips, projectedDays);
        await LoadStatsAsync();
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        var options = new[] { "📋 Copy to Clipboard", "💾 Save as CSV File" };
        var result = await DisplayActionSheet("Export Task Logs", "Cancel", null, options);

        if (result == null || result == "Cancel") return;

        _loadingOverlay.IsVisible = true;

        try
        {
            var logs = await _storyService.GetAllTaskLogsAsync(_auth.CurrentUsername);
            var projects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
            
            // Build lookup that maps any project/draft ID to the original project name
            var projectLookup = new Dictionary<int, string>();
            foreach (var project in projects)
            {
                if (project.ParentProjectId == null)
                {
                    // This is an original project
                    projectLookup[project.Id] = project.Name;
                }
                else
                {
                    // This is a draft - find and use parent name
                    var parent = projects.FirstOrDefault(p => p.Id == project.ParentProjectId);
                    projectLookup[project.Id] = parent?.Name ?? project.Name;
                }
            }

            if (logs.Count == 0)
            {
                await DisplayAlert("No Data", "No task logs found to export.", "OK");
                return;
            }

            // Build CSV content
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Date,Project,Task Type,Minutes,Line Text,Visual Description,Shot Description");

            foreach (var log in logs.OrderByDescending(l => l.CompletedAt))
            {
                string projectName = projectLookup.TryGetValue(log.ProjectId, out var name) ? name : $"Project {log.ProjectId}";
                string minutes = log.MinutesSpent?.ToString() ?? "Unknown";
                
                // Escape CSV fields (replace commas with semicolons, quotes with single quotes, newlines with spaces)
                string lineText = EscapeCsvField(log.LineText);
                string visualDesc = EscapeCsvField(log.VisualDescription);
                string shotDesc = EscapeCsvField(log.ShotDescription);
                
                sb.AppendLine($"{log.CompletedAt:yyyy-MM-dd HH:mm},{EscapeCsvField(projectName)},{log.TaskType},{minutes},{lineText},{visualDesc},{shotDesc}");
            }

            string csv = sb.ToString();

            if (result == "📋 Copy to Clipboard")
            {
                await Clipboard.SetTextAsync(csv);
                await DisplayAlert("Copied", $"Copied {logs.Count} task logs to clipboard.", "OK");
            }
            else if (result == "💾 Save as CSV File")
            {
                string fileName = $"production_tasks_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                
                await File.WriteAllTextAsync(filePath, csv);
                
                // Also copy to clipboard for easy access
                await Clipboard.SetTextAsync(filePath);
                
                await DisplayAlert("Saved", 
                    $"Saved {logs.Count} task logs to:\n{filePath}\n\n(Path copied to clipboard)", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    private string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ") + "\"";
        }
        return field;
    }
}
