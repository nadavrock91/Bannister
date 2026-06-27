using Bannister.Services;
using Bannister.Models;
using System.Globalization;

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

        if (project.IsPublished)
        {
            stack.Children.Add(BuildYouTubeFeedbackSection(project));
            stack.Children.Add(BuildFacebookFeedbackSection(project));
            stack.Children.Add(BuildTikTokFeedbackSection(project));
        }

        card.Content = stack;
        return card;
    }

    private View BuildYouTubeFeedbackSection(StoryProject project)
    {
        var section = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        section.Children.Add(new Label
        {
            Text = "YouTube Feedback",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828")
        });

        DateTime? capturedAt = project.YouTubeStatsCapturedAt;
        bool hasStats = capturedAt.HasValue;
        section.Children.Add(new Label
        {
            Text = hasStats ? $"Last updated {capturedAt:yyyy-MM-dd HH:mm}" : "No stats entered yet.",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666")
        });

        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };

        AddMetricTile(metricsGrid, 0, 0, "Views", hasStats ? FormatLargeNumber(project.YouTubeViews) : "—");
        AddMetricTile(metricsGrid, 0, 1, "Likes", hasStats ? FormatLargeNumber(project.YouTubeLikes) : "—");
        AddMetricTile(metricsGrid, 0, 2, "Comments", hasStats ? FormatLargeNumber(project.YouTubeComments) : "—");
        AddMetricTile(metricsGrid, 0, 3, "Avg duration", hasStats ? FormatDurationFromSeconds(project.YouTubeAverageViewDurationSeconds) : "—");
        section.Children.Add(metricsGrid);

        var editBtn = new Button
        {
            Text = "Edit YouTube Stats",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        };
        editBtn.Clicked += async (s, e) =>
        {
            if (_storyService.IsReadOnly)
            {
                await ShowReadOnlyAlertAsync();
                return;
            }

            await ShowEditYouTubeStatsAsync(project);
        };
        section.Children.Add(editBtn);

        return section;
    }

    private View BuildFacebookFeedbackSection(StoryProject project)
    {
        var section = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        section.Children.Add(new Label
        {
            Text = "Facebook Feedback",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1877F2")
        });

        DateTime? capturedAt = project.FacebookStatsCapturedAt;
        bool hasStats = capturedAt.HasValue;
        section.Children.Add(new Label
        {
            Text = hasStats ? $"Last updated {capturedAt:yyyy-MM-dd HH:mm}" : "No stats entered yet.",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666")
        });

        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddMetricTile(metricsGrid, 0, 0, "Views", hasStats ? FormatLargeNumber(project.FacebookViews) : "—");
        AddMetricTile(metricsGrid, 0, 1, "Likes", hasStats ? FormatLargeNumber(project.FacebookLikes) : "—");
        AddMetricTile(metricsGrid, 0, 2, "Comments", hasStats ? FormatLargeNumber(project.FacebookComments) : "—");
        AddMetricTile(metricsGrid, 1, 0, "Avg duration", hasStats ? FormatDurationFromSeconds(project.FacebookAverageViewDurationSeconds) : "—");
        AddMetricTile(metricsGrid, 1, 1, "3s views", FormatCountWithPercent(project.FacebookThreeSecondViews, project.FacebookViews, hasStats));
        AddMetricTile(metricsGrid, 1, 2, "1min views", FormatCountWithPercent(project.FacebookOneMinuteViews, project.FacebookViews, hasStats));
        section.Children.Add(metricsGrid);

        var editBtn = new Button
        {
            Text = "Edit Facebook Stats",
            BackgroundColor = Color.FromArgb("#E7F3FF"),
            TextColor = Color.FromArgb("#1877F2"),
            CornerRadius = 8,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        };
        editBtn.Clicked += async (s, e) =>
        {
            if (_storyService.IsReadOnly)
            {
                await ShowReadOnlyAlertAsync();
                return;
            }

            await ShowEditFacebookStatsAsync(project);
        };
        section.Children.Add(editBtn);

        return section;
    }

    private View BuildTikTokFeedbackSection(StoryProject project)
    {
        var section = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        section.Children.Add(new Label
        {
            Text = "TikTok Feedback",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FE2C55")
        });

        DateTime? capturedAt = project.TikTokStatsCapturedAt;
        bool hasStats = capturedAt.HasValue;
        section.Children.Add(new Label
        {
            Text = hasStats ? $"Last updated {capturedAt:yyyy-MM-dd HH:mm}" : "No stats entered yet.",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666")
        });

        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddMetricTile(metricsGrid, 0, 0, "Views", hasStats ? FormatLargeNumber(project.TikTokViews) : "—");
        AddMetricTile(metricsGrid, 0, 1, "Likes", hasStats ? FormatLargeNumber(project.TikTokLikes) : "—");
        AddMetricTile(metricsGrid, 0, 2, "Comments", hasStats ? FormatLargeNumber(project.TikTokComments) : "—");
        AddMetricTile(metricsGrid, 1, 0, "Avg watch time", hasStats ? FormatDurationFromSeconds(project.TikTokAverageWatchTimeSeconds) : "—");
        AddMetricTile(metricsGrid, 1, 1, "% full video", hasStats ? FormatPercent(project.TikTokPercentWatchedFullVideo) : "—");
        section.Children.Add(metricsGrid);

        var editBtn = new Button
        {
            Text = "Edit TikTok Stats",
            BackgroundColor = Color.FromArgb("#FFE6EA"),
            TextColor = Color.FromArgb("#FE2C55"),
            CornerRadius = 8,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        };
        editBtn.Clicked += async (s, e) =>
        {
            if (_storyService.IsReadOnly)
            {
                await ShowReadOnlyAlertAsync();
                return;
            }

            await ShowEditTikTokStatsAsync(project);
        };
        section.Children.Add(editBtn);

        return section;
    }

    private void AddMetricTile(Grid grid, int row, int col, string label, string value)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            Padding = 6,
            CornerRadius = 6,
            HasShadow = false
        };

        frame.Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = label,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#666"),
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = value,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#263238"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        Grid.SetRow(frame, row);
        Grid.SetColumn(frame, col);
        grid.Children.Add(frame);
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

    private static string FormatDurationFromSeconds(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    private static string FormatLargeNumber(int value)
    {
        return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(double value)
    {
        return Math.Clamp(value, 0.0, 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatCountWithPercent(int count, int denominator, bool statsCaptured)
    {
        if (!statsCaptured)
            return "—";

        int safeCount = Math.Max(0, count);
        int safeDenominator = Math.Max(0, denominator);
        string raw = FormatLargeNumber(safeCount);

        if (safeDenominator == 0)
            return $"{raw} (—)";

        double percent = safeCount * 100.0 / safeDenominator;
        return $"{raw} ({percent.ToString("0.0", CultureInfo.InvariantCulture)}%)";
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
                    await _storyService.UnpublishProjectAsync(project.Id);
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
            await _storyService.PublishProjectAsync(project.Id, finalClips);
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
