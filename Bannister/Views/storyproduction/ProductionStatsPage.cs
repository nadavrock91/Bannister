using Bannister.Services;
using Bannister.Models;
using System.Globalization;
using System.Text;

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
    private Picker _categoryPicker;
    private bool _isLoadingCategories;
    private string _selectedCategory = "All";

    private static string GetProjectCategoryFilterKey(string username) => $"story_production_category_filter_{username}";

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

        var categoryRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        categoryRow.Children.Add(new Label
        {
            Text = "Category:",
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            VerticalOptions = LayoutOptions.Center
        });

        _categoryPicker = new Picker
        {
            Title = "Category",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HorizontalOptions = LayoutOptions.Fill
        };
        _categoryPicker.SelectedIndexChanged += OnCategoryFilterChanged;
        Grid.SetColumn(_categoryPicker, 1);
        categoryRow.Children.Add(_categoryPicker);
        mainStack.Children.Add(categoryRow);

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
            await LoadSelectedCategoryAsync();
            RefreshCategoryPicker(originalProjects);
            var filteredProjects = FilterProjectsBySelectedCategory(originalProjects);

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

            // Produced section
            var produced = filteredProjects.Where(p => p.IsProduced).ToList();
            _statsStack.Children.Add(CreateSectionHeader($"✅ Produced ({produced.Count})"));
            if (produced.Count > 0)
            {
                foreach (var project in produced)
                {
                    _statsStack.Children.Add(await BuildProjectCardAsync(project));
                }
            }
            else
            {
                _statsStack.Children.Add(CreateEmptyFilterLabel());
            }

            // Not produced section
            var notProduced = filteredProjects.Where(p => !p.IsProduced).ToList();
            _statsStack.Children.Add(CreateNotProducedSectionHeader(notProduced.Count));
            if (notProduced.Count > 0)
            {
                foreach (var project in notProduced)
                {
                    _statsStack.Children.Add(await BuildProjectCardAsync(project));
                }
            }
            else
            {
                _statsStack.Children.Add(CreateEmptyFilterLabel());
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

    private View CreateNotProducedSectionHeader(int count)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(0, 8, 0, 4)
        };

        grid.Children.Add(new Label
        {
            Text = $"🚧 Not Produced ({count})",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });

        var exportBtn = new Button
        {
            Text = "📤 Export for LLM",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            FontSize = 12,
            Padding = new Thickness(10, 4)
        };
        exportBtn.Clicked += async (s, e) => await ExportNotProducedCandidatesAsync();
        Grid.SetColumn(exportBtn, 1);
        grid.Children.Add(exportBtn);

        return grid;
    }

    private Label CreateEmptyFilterLabel()
    {
        return new Label
        {
            Text = _selectedCategory == "All" ? "(none)" : "(none in this category)",
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#777"),
            Margin = new Thickness(4, 0, 0, 8)
        };
    }

    private async Task LoadSelectedCategoryAsync()
    {
        try
        {
            var saved = await SecureStorage.GetAsync(GetProjectCategoryFilterKey(_auth.CurrentUsername));
            if (!string.IsNullOrWhiteSpace(saved))
                _selectedCategory = saved;
        }
        catch
        {
        }
    }

    private async Task SaveSelectedCategoryAsync()
    {
        try
        {
            await SecureStorage.SetAsync(GetProjectCategoryFilterKey(_auth.CurrentUsername), _selectedCategory);
        }
        catch
        {
        }
    }

    private void RefreshCategoryPicker(List<StoryProject> projects)
    {
        _isLoadingCategories = true;
        var previous = _selectedCategory;

        var categories = projects
            .Select(p => string.IsNullOrWhiteSpace(p.ProjectCategory) ? "Uncategorized" : p.ProjectCategory.Trim())
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => c).First())
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("All");
        foreach (var category in categories)
            _categoryPicker.Items.Add(category);

        int index = _categoryPicker.Items.IndexOf(previous);
        if (index < 0)
        {
            _selectedCategory = "All";
            index = 0;
        }

        _categoryPicker.SelectedIndex = index;
        _isLoadingCategories = false;
    }

    private List<StoryProject> FilterProjectsBySelectedCategory(List<StoryProject> projects)
    {
        if (_selectedCategory == "All")
            return projects.ToList();

        if (_selectedCategory == "Uncategorized")
            return projects.Where(p => string.IsNullOrWhiteSpace(p.ProjectCategory)).ToList();

        return projects
            .Where(p => string.Equals(p.ProjectCategory?.Trim(), _selectedCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async void OnCategoryFilterChanged(object? sender, EventArgs e)
    {
        if (_isLoadingCategories || _categoryPicker.SelectedIndex < 0)
            return;

        _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex];
        await SaveSelectedCategoryAsync();
        await LoadStatsAsync();
    }

    private Task<Frame> BuildProjectCardAsync(StoryProject project)
    {
        var card = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = project.IsProduced ? Color.FromArgb("#E8F5E9") : Colors.White,
            BorderColor = project.IsProduced ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) =>
        {
            await Navigation.PushAsync(new ProductionStatsDetailPage(_storyService, _auth, project.Id));
        };
        card.GestureRecognizers.Add(tapGesture);

        var stack = new VerticalStackLayout { Spacing = 8 };

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        titleRow.Children.Add(new Label
        {
            Text = project.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var chevron = new Label
        {
            Text = ">",
            FontSize = 18,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(chevron, 1);
        titleRow.Children.Add(chevron);

        stack.Children.Add(titleRow);

        var datesRow = new Label
        {
            Text = $"Started {project.CreatedAt:MMM d, yyyy}  •  Published {(project.IsPublished && project.PublishedAt.HasValue ? project.PublishedAt.Value.ToString("MMM d, yyyy") : "—")}",
            FontSize = 11,
            TextColor = Color.FromArgb("#666")
        };
        stack.Children.Add(datesRow);

        if (project.IsPublished)
        {
            var platformRow = new HorizontalStackLayout { Spacing = 8 };
            bool hasAnyStats = project.YouTubeStatsCapturedAt.HasValue || project.FacebookStatsCapturedAt.HasValue || project.TikTokStatsCapturedAt.HasValue;
            int totalViews = project.YouTubeViews + project.FacebookViews + project.TikTokViews;
            platformRow.Children.Add(CreatePlatformPill("Total", hasAnyStats ? ProjectStatsRendering.FormatLargeNumber(totalViews) : "—", "#ECEFF1", "#263238"));
            platformRow.Children.Add(CreatePlatformPill("YT", project.YouTubeStatsCapturedAt.HasValue ? ProjectStatsRendering.FormatLargeNumber(project.YouTubeViews) : "—", "#FFEBEE", "#C62828"));
            platformRow.Children.Add(CreatePlatformPill("FB", project.FacebookStatsCapturedAt.HasValue ? ProjectStatsRendering.FormatLargeNumber(project.FacebookViews) : "—", "#E7F3FF", "#1877F2"));
            platformRow.Children.Add(CreatePlatformPill("TT", project.TikTokStatsCapturedAt.HasValue ? ProjectStatsRendering.FormatLargeNumber(project.TikTokViews) : "—", "#FFE6EA", "#FE2C55"));
            stack.Children.Add(platformRow);
        }
        else
        {
            stack.Children.Add(new Label
            {
                Text = "Not yet published",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic,
                TextColor = Color.FromArgb("#666")
            });
        }

        card.Content = stack;
        return Task.FromResult(card);
    }

    private async Task ExportNotProducedCandidatesAsync()
    {
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var parentProjects = allProjects.Where(p => p.ParentProjectId == null).ToList();
        var candidates = FilterProjectsBySelectedCategory(parentProjects)
            .Where(p => !p.IsProduced && p.IsPublished)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        if (candidates.Count == 0)
        {
            await DisplayAlert("No candidates", "No candidate stories available. Mark some projects as Published to gather stats first.", "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("NEXT PRODUCTION SELECTION");
        sb.AppendLine();
        sb.AppendLine("I have several stories I've published as talking-head narrations and gathered initial stats on. I haven't yet produced the full visual versions of any of these. Help me decide which to produce next, weighing initial reception against expected production cost (longer/more complex stories cost more to produce visually).");
        sb.AppendLine();
        sb.AppendLine("For each candidate below I've included:");
        sb.AppendLine("- The story content (line by line, with visual context and shot breakdown if drafted)");
        sb.AppendLine("- Stats from the talking-head publish (views, likes, comments, etc. across YouTube, Facebook, TikTok)");
        sb.AppendLine("- Projected production days if set");
        sb.AppendLine();
        sb.AppendLine("Rank these stories by which to produce next. Give your top 3 with reasoning. Consider both signal strength (which stories audiences responded to) and production efficiency (which give the best return on production effort).");

        for (int i = 0; i < candidates.Count; i++)
        {
            var project = candidates[i];
            var statsSourceDraft = await _storyService.ResolveStatsSourceDraftAsync(project) ?? project;
            var lines = await _storyService.GetLinesAsync(statsSourceDraft.Id);

            sb.AppendLine();
            sb.AppendLine("================================================================");
            sb.AppendLine($"CANDIDATE {i + 1}: {project.Name}");
            sb.AppendLine($"Category: {(string.IsNullOrWhiteSpace(project.ProjectCategory) ? "Uncategorized" : project.ProjectCategory.Trim())}");
            sb.AppendLine($"Stats source draft: {GetStatsSourceDraftLabel(project, statsSourceDraft)}");
            sb.AppendLine($"Projected production days: {(project.ProjectedDays > 0 ? project.ProjectedDays.ToString(CultureInfo.InvariantCulture) : "not estimated")}");
            sb.AppendLine();
            sb.AppendLine(ProjectStatsRendering.BuildStatsExportBlock(project));
            sb.AppendLine();
            sb.AppendLine("--- STORY ---");

            foreach (var line in lines.OrderBy(l => l.LineOrder))
            {
                sb.AppendLine($"Line {line.LineOrder}");
                sb.AppendLine($"  Script: {line.LineText}");
                if (!string.IsNullOrWhiteSpace(line.VisualDescription))
                    sb.AppendLine($"  Visual: {line.VisualDescription}");
                if (!string.IsNullOrWhiteSpace(line.ImagePrompt))
                    sb.AppendLine($"  ImagePrompt: {line.ImagePrompt}");
                if (!string.IsNullOrWhiteSpace(line.VideoPrompt))
                    sb.AppendLine($"  VideoPrompt: {line.VideoPrompt}");

                var shots = _storyService.GetShots(line);
                if (shots.Count > 0)
                {
                    sb.AppendLine("  Shots:");
                    for (int shotIndex = 0; shotIndex < shots.Count; shotIndex++)
                    {
                        var shot = shots[shotIndex];
                        var parts = new List<string> { $"    Shot {shotIndex + 1}: {shot.Description}" };
                        if (!string.IsNullOrWhiteSpace(shot.ImagePrompt))
                            parts.Add($"image: {shot.ImagePrompt}");
                        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
                            parts.Add($"video: {shot.VideoPrompt}");
                        sb.AppendLine(string.Join(" | ", parts));
                    }
                }
            }
        }

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", $"Copied {candidates.Count} candidate stories to clipboard. Paste into your LLM of choice for next-production recommendation.", "OK");
    }

    private string GetStatsSourceDraftLabel(StoryProject project, StoryProject resolvedProject)
    {
        if (resolvedProject.Id == project.Id || resolvedProject.ParentProjectId == null && resolvedProject.Id == (project.ParentProjectId ?? project.Id))
            return "Original";

        return $"Draft {resolvedProject.DraftVersion}: \"{resolvedProject.Name}\"";
    }

    private View CreatePlatformPill(string label, string value, string background, string foreground)
    {
        return new Frame
        {
            Padding = new Thickness(8, 3),
            CornerRadius = 10,
            BackgroundColor = Color.FromArgb(background),
            BorderColor = Colors.Transparent,
            HasShadow = false,
            Content = new Label
            {
                Text = $"{label} {value}",
                FontSize = 11,
                TextColor = Color.FromArgb(foreground),
                FontAttributes = FontAttributes.Bold
            }
        };
    }

    private async Task ShowReadOnlyAlertAsync()
    {
        await DisplayAlert("Read-only", "Read-only on this device. Sync from master to modify Story Production data.", "OK");
    }

    private Task ShowEditYouTubeStatsAsync(StoryProject project)
    {
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var card = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = 520,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label
        {
            Text = "Edit YouTube Stats",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828")
        });
        stack.Children.Add(new Label
        {
            Text = project.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        bool hasStats = project.YouTubeStatsCapturedAt.HasValue;
        var viewsEntry = CreateYouTubeStatsEntry(hasStats ? project.YouTubeViews.ToString(CultureInfo.InvariantCulture) : "0", "Views", Keyboard.Numeric);
        var likesEntry = CreateYouTubeStatsEntry(hasStats ? project.YouTubeLikes.ToString(CultureInfo.InvariantCulture) : "0", "Likes", Keyboard.Numeric);
        var commentsEntry = CreateYouTubeStatsEntry(hasStats ? project.YouTubeComments.ToString(CultureInfo.InvariantCulture) : "0", "Comments", Keyboard.Numeric);

        stack.Children.Add(CreateYouTubeStatsInputRow("Views", viewsEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Likes", likesEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Comments", commentsEntry));
        stack.Children.Add(CreateDurationInputRow("Average view duration", hasStats ? project.YouTubeAverageViewDurationSeconds : 0, out var ytMinutesEntry, out var ytSecondsEntry));

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var clearBtn = new Button
        {
            Text = "Clear stats",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 12,
            Padding = new Thickness(8, 0)
        };
        Grid.SetColumn(clearBtn, 0);
        footer.Children.Add(clearBtn);

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(18, 8)
        };
        Grid.SetColumn(cancelBtn, 2);
        footer.Children.Add(cancelBtn);

        var saveBtn = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(22, 8)
        };
        Grid.SetColumn(saveBtn, 3);
        footer.Children.Add(saveBtn);

        stack.Children.Add(footer);
        card.Content = stack;
        overlay.Children.Add(card);

        void CloseOverlay()
        {
            if (Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
        }

        cancelBtn.Clicked += (s, e) => CloseOverlay();

        clearBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Clear YouTube stats?", "All four values will be reset to empty.", "Clear", "Cancel");
            if (!confirm) return;

            try
            {
                await _storyService.ClearYouTubeStatsAsync(project.Id);
                CloseOverlay();
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };

        saveBtn.Clicked += async (s, e) =>
        {
            int views = ParseNonNegativeIntegerOrZero(viewsEntry.Text);
            int likes = ParseNonNegativeIntegerOrZero(likesEntry.Text);
            int comments = ParseNonNegativeIntegerOrZero(commentsEntry.Text);

            if (!TryReadMinutesSeconds(ytMinutesEntry, ytSecondsEntry, out int durationSeconds))
            {
                await DisplayAlert("Invalid Duration", "Minutes and seconds must be non-negative numbers; seconds must be 0-59.", "OK");
                return;
            }

            try
            {
                await _storyService.SetYouTubeStatsAsync(project.Id, views, likes, comments, durationSeconds);
                CloseOverlay();
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };

        if (Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        return Task.CompletedTask;
    }

    private Task ShowEditFacebookStatsAsync(StoryProject project)
    {
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var card = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = 520,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label
        {
            Text = "Edit Facebook Stats",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1877F2")
        });
        stack.Children.Add(new Label
        {
            Text = project.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        bool hasStats = project.FacebookStatsCapturedAt.HasValue;
        var viewsEntry = CreateYouTubeStatsEntry(hasStats ? project.FacebookViews.ToString(CultureInfo.InvariantCulture) : "0", "Views", Keyboard.Numeric);
        var likesEntry = CreateYouTubeStatsEntry(hasStats ? project.FacebookLikes.ToString(CultureInfo.InvariantCulture) : "0", "Likes", Keyboard.Numeric);
        var commentsEntry = CreateYouTubeStatsEntry(hasStats ? project.FacebookComments.ToString(CultureInfo.InvariantCulture) : "0", "Comments", Keyboard.Numeric);
        var threeSecondViewsEntry = CreateYouTubeStatsEntry(hasStats ? project.FacebookThreeSecondViews.ToString(CultureInfo.InvariantCulture) : "0", "3-second views", Keyboard.Numeric);
        var oneMinuteViewsEntry = CreateYouTubeStatsEntry(hasStats ? project.FacebookOneMinuteViews.ToString(CultureInfo.InvariantCulture) : "0", "1-minute views", Keyboard.Numeric);

        stack.Children.Add(CreateYouTubeStatsInputRow("Views", viewsEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Likes", likesEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Comments", commentsEntry));
        stack.Children.Add(CreateDurationInputRow("Average view duration", hasStats ? project.FacebookAverageViewDurationSeconds : 0, out var fbMinutesEntry, out var fbSecondsEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("3-second views", threeSecondViewsEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("1-minute views", oneMinuteViewsEntry));

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var clearBtn = new Button
        {
            Text = "Clear stats",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#1877F2"),
            FontSize = 12,
            Padding = new Thickness(8, 0)
        };
        Grid.SetColumn(clearBtn, 0);
        footer.Children.Add(clearBtn);

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(18, 8)
        };
        Grid.SetColumn(cancelBtn, 2);
        footer.Children.Add(cancelBtn);

        var saveBtn = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#1877F2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(22, 8)
        };
        Grid.SetColumn(saveBtn, 3);
        footer.Children.Add(saveBtn);

        stack.Children.Add(footer);
        card.Content = stack;
        overlay.Children.Add(card);

        void CloseOverlay()
        {
            if (Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
        }

        cancelBtn.Clicked += (s, e) => CloseOverlay();

        clearBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Clear Facebook stats?", "All six values will be reset to empty.", "Clear", "Cancel");
            if (!confirm) return;

            try
            {
                await _storyService.ClearFacebookStatsAsync(project.Id);
                CloseOverlay();
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };

        saveBtn.Clicked += async (s, e) =>
        {
            int views = ParseNonNegativeIntegerOrZero(viewsEntry.Text);
            int likes = ParseNonNegativeIntegerOrZero(likesEntry.Text);
            int comments = ParseNonNegativeIntegerOrZero(commentsEntry.Text);
            int threeSecondViews = ParseNonNegativeIntegerOrZero(threeSecondViewsEntry.Text);
            int oneMinuteViews = ParseNonNegativeIntegerOrZero(oneMinuteViewsEntry.Text);

            if (!TryReadMinutesSeconds(fbMinutesEntry, fbSecondsEntry, out int durationSeconds))
            {
                await DisplayAlert("Invalid Duration", "Minutes and seconds must be non-negative numbers; seconds must be 0-59.", "OK");
                return;
            }

            try
            {
                await _storyService.SetFacebookStatsAsync(project.Id, views, likes, comments, durationSeconds, threeSecondViews, oneMinuteViews);
                CloseOverlay();
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };

        if (Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        return Task.CompletedTask;
    }

    private Task ShowEditTikTokStatsAsync(StoryProject project)
    {
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var card = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = 520,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label
        {
            Text = "Edit TikTok Stats",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FE2C55")
        });
        stack.Children.Add(new Label
        {
            Text = project.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        bool hasStats = project.TikTokStatsCapturedAt.HasValue;
        var viewsEntry = CreateYouTubeStatsEntry(hasStats ? project.TikTokViews.ToString(CultureInfo.InvariantCulture) : "0", "Views", Keyboard.Numeric);
        var likesEntry = CreateYouTubeStatsEntry(hasStats ? project.TikTokLikes.ToString(CultureInfo.InvariantCulture) : "0", "Likes", Keyboard.Numeric);
        var commentsEntry = CreateYouTubeStatsEntry(hasStats ? project.TikTokComments.ToString(CultureInfo.InvariantCulture) : "0", "Comments", Keyboard.Numeric);
        var percentEntry = CreateYouTubeStatsEntry(hasStats ? project.TikTokPercentWatchedFullVideo.ToString("0.0", CultureInfo.InvariantCulture) : "0.0", "0-100", Keyboard.Numeric);

        stack.Children.Add(CreateYouTubeStatsInputRow("Views", viewsEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Likes", likesEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Comments", commentsEntry));
        stack.Children.Add(CreateDurationInputRow("Average watch time", hasStats ? project.TikTokAverageWatchTimeSeconds : 0, out var ttMinutesEntry, out var ttSecondsEntry));
        stack.Children.Add(CreateYouTubeStatsInputRow("Percent watched full video", percentEntry));

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var clearBtn = new Button
        {
            Text = "Clear stats",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#FE2C55"),
            FontSize = 12,
            Padding = new Thickness(8, 0)
        };
        Grid.SetColumn(clearBtn, 0);
        footer.Children.Add(clearBtn);

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(18, 8)
        };
        Grid.SetColumn(cancelBtn, 2);
        footer.Children.Add(cancelBtn);

        var saveBtn = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#FE2C55"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(22, 8)
        };
        Grid.SetColumn(saveBtn, 3);
        footer.Children.Add(saveBtn);

        stack.Children.Add(footer);
        card.Content = stack;
        overlay.Children.Add(card);

        void CloseOverlay()
        {
            if (Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
        }

        cancelBtn.Clicked += (s, e) => CloseOverlay();

        clearBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Clear TikTok stats?", "All five values will be reset to empty.", "Clear", "Cancel");
            if (!confirm) return;

            try
            {
                await _storyService.ClearTikTokStatsAsync(project.Id);
                CloseOverlay();
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };

        saveBtn.Clicked += async (s, e) =>
        {
            int views = ParseNonNegativeIntegerOrZero(viewsEntry.Text);
            int likes = ParseNonNegativeIntegerOrZero(likesEntry.Text);
            int comments = ParseNonNegativeIntegerOrZero(commentsEntry.Text);

            if (!TryReadMinutesSeconds(ttMinutesEntry, ttSecondsEntry, out int averageWatchTimeSeconds))
            {
                await DisplayAlert("Invalid Duration", "Minutes and seconds must be non-negative numbers; seconds must be 0-59.", "OK");
                return;
            }

            string percentText = (percentEntry.Text ?? "").Trim();
            double percentWatchedFullVideo = 0.0;
            if (!string.IsNullOrEmpty(percentText)
                && !double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out percentWatchedFullVideo))
            {
                await DisplayAlert("Invalid Percent", "Percent must be a number between 0 and 100.", "OK");
                return;
            }

            if (percentWatchedFullVideo < 0.0 || percentWatchedFullVideo > 100.0)
            {
                await DisplayAlert("Invalid Percent", "Percent must be a number between 0 and 100.", "OK");
                return;
            }

            try
            {
                await _storyService.SetTikTokStatsAsync(project.Id, views, likes, comments, averageWatchTimeSeconds, percentWatchedFullVideo);
                CloseOverlay();
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };

        if (Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        return Task.CompletedTask;
    }

    private Entry CreateYouTubeStatsEntry(string initialValue, string placeholder, Keyboard keyboard)
    {
        return new Entry
        {
            Text = initialValue,
            Placeholder = placeholder,
            Keyboard = keyboard,
            WidthRequest = 180,
            HorizontalOptions = LayoutOptions.End
        };
    }

    private View CreateYouTubeStatsInputRow(string label, Entry entry)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        row.Children.Add(new Label
        {
            Text = label,
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });

        Grid.SetColumn(entry, 1);
        row.Children.Add(entry);
        return row;
    }

    private View CreateDurationInputRow(string label, int initialSeconds, out Entry minutesEntry, out Entry secondsEntry)
    {
        initialSeconds = Math.Max(0, initialSeconds);

        minutesEntry = new Entry
        {
            Text = (initialSeconds / 60).ToString(CultureInfo.InvariantCulture),
            Placeholder = "Min",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 70,
            HorizontalOptions = LayoutOptions.End
        };

        secondsEntry = new Entry
        {
            Text = (initialSeconds % 60).ToString(CultureInfo.InvariantCulture),
            Placeholder = "Sec",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 70,
            HorizontalOptions = LayoutOptions.End
        };

        var inputRow = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.End,
            Children =
            {
                minutesEntry,
                new Label
                {
                    Text = ":",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333"),
                    VerticalOptions = LayoutOptions.Center
                },
                secondsEntry
            }
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        row.Children.Add(new Label
        {
            Text = label,
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });

        Grid.SetColumn(inputRow, 1);
        row.Children.Add(inputRow);
        return row;
    }

    private static int ParseNonNegativeIntegerOrZero(string? input)
    {
        if (!int.TryParse((input ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            return 0;

        return Math.Max(0, value);
    }

    private static bool TryReadMinutesSeconds(Entry minutesEntry, Entry secondsEntry, out int totalSeconds)
    {
        totalSeconds = 0;

        string minutesText = (minutesEntry.Text ?? "").Trim();
        string secondsText = (secondsEntry.Text ?? "").Trim();

        int minutes = 0;
        int seconds = 0;

        if (!string.IsNullOrEmpty(minutesText)
            && !int.TryParse(minutesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
            return false;

        if (!string.IsNullOrEmpty(secondsText)
            && !int.TryParse(secondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            return false;

        if (minutes < 0 || seconds < 0 || seconds > 59)
            return false;

        totalSeconds = minutes * 60 + seconds;
        return true;
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
                try
                {
                    await _storyService.UnmarkProducedAsync(project.Id);
                    await LoadStatsAsync();
                }
                catch (ReadOnlyDatabaseException)
                {
                    await ShowReadOnlyAlertAsync();
                }
            }
        }
        else if (action == "✏️ Edit Clip Count")
        {
            string? clipStr = await DisplayPromptAsync("Edit Clip Count", "Final clip count:", "Save", "Cancel", 
                initialValue: project.FinalClipCount.ToString(), keyboard: Keyboard.Numeric);
            if (!string.IsNullOrEmpty(clipStr) && int.TryParse(clipStr, out int clips))
            {
                try
                {
                    project.FinalClipCount = clips;
                    await _storyService.UpdateProjectAsync(project);
                    await LoadStatsAsync();
                }
                catch (ReadOnlyDatabaseException)
                {
                    await ShowReadOnlyAlertAsync();
                }
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
            try
            {
                project.CreatedAt = newDate;
                await _storyService.UpdateProjectAsync(project);
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
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
            try
            {
                project.PublishedAt = newDate;
                project.CompletedAt = newDate;
                await _storyService.UpdateProjectAsync(project);
                await LoadStatsAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
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

        try
        {
            await _storyService.MarkProducedAsync(project.Id, finalClips);
            await LoadStatsAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
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

        try
        {
            await _storyService.SetProjectionsAsync(project.Id, projectedClips, projectedDays);
            await LoadStatsAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
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
