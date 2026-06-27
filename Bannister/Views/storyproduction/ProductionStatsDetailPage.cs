using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class ProductionStatsDetailPage : ContentPage
{
    private readonly StoryProductionService _storyService;
    private readonly AuthService _auth;
    private readonly int _projectId;

    private Grid _mainGrid;
    private VerticalStackLayout _contentStack;
    private Grid _loadingOverlay;

    public ProductionStatsDetailPage(StoryProductionService storyService, AuthService auth, int projectId)
    {
        _storyService = storyService;
        _auth = auth;
        _projectId = projectId;

        Title = "Production Stats Detail";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    private void BuildUI()
    {
        _mainGrid = new Grid();

        var scrollView = new ScrollView();
        _contentStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 14
        };

        scrollView.Content = _contentStack;
        _mainGrid.Children.Add(scrollView);

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
        _mainGrid.Children.Add(_loadingOverlay);

        Content = _mainGrid;
    }

    private async Task LoadDetailAsync()
    {
        _loadingOverlay.IsVisible = true;
        try
        {
            _contentStack.Children.Clear();
            var project = await _storyService.GetProjectByIdAsync(_projectId);
            if (project == null)
            {
                _contentStack.Children.Add(new Label
                {
                    Text = "Project no longer exists.",
                    TextColor = Colors.Red,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40)
                });
                await Task.Delay(900);
                await Navigation.PopAsync();
                return;
            }

            _contentStack.Children.Add(BuildHeader(project));
            _contentStack.Children.Add(await BuildMetricsCardAsync(project));

            if (project.IsPublished)
            {
                _contentStack.Children.Add(ProjectStatsRendering.BuildYouTubeFeedbackSection(
                    project,
                    () => _storyService.IsReadOnly,
                    ShowReadOnlyAlertAsync,
                    ShowEditYouTubeStatsAsync));
                _contentStack.Children.Add(ProjectStatsRendering.BuildFacebookFeedbackSection(
                    project,
                    () => _storyService.IsReadOnly,
                    ShowReadOnlyAlertAsync,
                    ShowEditFacebookStatsAsync));
                _contentStack.Children.Add(ProjectStatsRendering.BuildTikTokFeedbackSection(
                    project,
                    () => _storyService.IsReadOnly,
                    ShowReadOnlyAlertAsync,
                    ShowEditTikTokStatsAsync));
            }
        }
        catch (Exception ex)
        {
            _contentStack.Children.Add(new Label
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

    private View BuildHeader(StoryProject project)
    {
        var frame = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var titleStack = new VerticalStackLayout { Spacing = 8 };
        titleStack.Children.Add(new Label
        {
            Text = project.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            MaxLines = 2
        });

        var pillRow = new HorizontalStackLayout { Spacing = 8 };
        pillRow.Children.Add(CreatePill(project.IsPublished ? "Published" : "Unpublished", project.IsPublished ? "#E8F5E9" : "#ECEFF1", project.IsPublished ? "#2E7D32" : "#546E7A"));
        pillRow.Children.Add(CreatePill(project.IsProduced ? "Produced" : "Not Produced", project.IsProduced ? "#E3F2FD" : "#ECEFF1", project.IsProduced ? "#1565C0" : "#546E7A"));
        if (!string.IsNullOrWhiteSpace(project.ProjectCategory))
            pillRow.Children.Add(CreatePill(project.ProjectCategory.Trim(), "#E0F7FA", "#00838F"));
        titleStack.Children.Add(pillRow);

        grid.Children.Add(titleStack);

        var settingsBtn = new Button
        {
            Text = "⚙️ Settings",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            FontSize = 13,
            Padding = new Thickness(12, 8)
        };
        settingsBtn.Clicked += async (s, e) => await ShowProjectSettingsAsync(project);
        Grid.SetColumn(settingsBtn, 1);
        grid.Children.Add(settingsBtn);

        frame.Content = grid;
        return frame;
    }

    private View CreatePill(string text, string background, string foreground)
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
                Text = text,
                FontSize = 11,
                TextColor = Color.FromArgb(foreground),
                FontAttributes = FontAttributes.Bold
            }
        };
    }

    private string GetStatsSourceDraftLabel(StoryProject project, StoryProject resolvedProject)
    {
        if (resolvedProject.Id == project.Id || resolvedProject.ParentProjectId == null && resolvedProject.Id == (project.ParentProjectId ?? project.Id))
            return "Original";

        return $"Draft {resolvedProject.DraftVersion}: \"{resolvedProject.Name}\"";
    }

    private async Task<View> BuildMetricsCardAsync(StoryProject project)
    {
        var lines = await _storyService.GetLinesAsync(project.Id);
        int clipCount = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            clipCount += shots.Count > 0 ? shots.Count : 1;
        }

        var frame = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(new Label
        {
            Text = "Production Metrics",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

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
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 4,
            ColumnSpacing = 12
        };

        ProjectStatsRendering.AddStatCell(statsGrid, 0, 0, "Started", project.CreatedAt.ToString("MMM d, yyyy"));
        ProjectStatsRendering.AddStatCell(statsGrid, 0, 1, "Published", project.IsPublished && project.PublishedAt.HasValue ? project.PublishedAt.Value.ToString("MMM d, yyyy") : "—");

        int actualDays = project.ActualDays;
        string daysText = project.IsProduced ? $"{actualDays} days" : $"{actualDays} days (ongoing)";
        ProjectStatsRendering.AddStatCell(statsGrid, 1, 0, "Duration", daysText);

        string projDaysText = "—";
        if (project.ProjectedDays > 0)
        {
            projDaysText = $"{project.ProjectedDays} days";
            if (project.IsProduced)
            {
                int diff = actualDays - project.ProjectedDays;
                if (diff > 0) projDaysText += $" (+{diff})";
                else if (diff < 0) projDaysText += $" ({diff})";
            }
        }
        ProjectStatsRendering.AddStatCell(statsGrid, 1, 1, "Projected", projDaysText);

        string clipsText = project.IsProduced ? $"{project.FinalClipCount} clips" : $"{clipCount} clips";
        ProjectStatsRendering.AddStatCell(statsGrid, 2, 0, "Clips", clipsText);

        string projClipsText = "—";
        if (project.ProjectedClipCount > 0)
        {
            projClipsText = $"{project.ProjectedClipCount} clips";
            if (project.IsProduced && project.FinalClipCount > 0)
            {
                int diff = project.FinalClipCount - project.ProjectedClipCount;
                if (diff > 0) projClipsText += $" (+{diff})";
                else if (diff < 0) projClipsText += $" ({diff})";
            }
        }
        ProjectStatsRendering.AddStatCell(statsGrid, 2, 1, "Projected", projClipsText);

        var statsSourceDraft = await _storyService.ResolveStatsSourceDraftAsync(project) ?? project;
        ProjectStatsRendering.AddStatCell(statsGrid, 3, 0, "Stats source draft", GetStatsSourceDraftLabel(project, statsSourceDraft));

        stack.Children.Add(statsGrid);
        frame.Content = stack;
        return frame;
    }

    private async Task ShowReadOnlyAlertAsync()
    {
        await DisplayAlert("Read-only", "Read-only on this device. Sync from master to modify Story Production data.", "OK");
    }

    private Entry CreateStatsEntry(string initialValue, string placeholder, Keyboard keyboard)
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

    private View CreateStatsInputRow(string label, Entry entry)
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

        if (!string.IsNullOrEmpty(minutesText) && !int.TryParse(minutesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
            return false;

        if (!string.IsNullOrEmpty(secondsText) && !int.TryParse(secondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            return false;

        if (minutes < 0 || seconds < 0 || seconds > 59)
            return false;

        totalSeconds = minutes * 60 + seconds;
        return true;
    }

    private Task ShowEditYouTubeStatsAsync(StoryProject project)
    {
        var overlay = CreateOverlay(out var stack, "Edit YouTube Stats", "#C62828", project.Name);
        bool hasStats = project.YouTubeStatsCapturedAt.HasValue;
        var viewsEntry = CreateStatsEntry(hasStats ? project.YouTubeViews.ToString(CultureInfo.InvariantCulture) : "0", "Views", Keyboard.Numeric);
        var likesEntry = CreateStatsEntry(hasStats ? project.YouTubeLikes.ToString(CultureInfo.InvariantCulture) : "0", "Likes", Keyboard.Numeric);
        var commentsEntry = CreateStatsEntry(hasStats ? project.YouTubeComments.ToString(CultureInfo.InvariantCulture) : "0", "Comments", Keyboard.Numeric);

        stack.Children.Add(CreateStatsInputRow("Views", viewsEntry));
        stack.Children.Add(CreateStatsInputRow("Likes", likesEntry));
        stack.Children.Add(CreateStatsInputRow("Comments", commentsEntry));
        stack.Children.Add(CreateDurationInputRow("Average view duration", hasStats ? project.YouTubeAverageViewDurationSeconds : 0, out var minutesEntry, out var secondsEntry));

        AddOverlayFooter(overlay, stack, "#C62828", "Clear YouTube stats?", "All four values will be reset to empty.",
            async () => await _storyService.ClearYouTubeStatsAsync(project.Id),
            async () =>
            {
                int views = ParseNonNegativeIntegerOrZero(viewsEntry.Text);
                int likes = ParseNonNegativeIntegerOrZero(likesEntry.Text);
                int comments = ParseNonNegativeIntegerOrZero(commentsEntry.Text);
                if (!TryReadMinutesSeconds(minutesEntry, secondsEntry, out int durationSeconds))
                {
                    await DisplayAlert("Invalid Duration", "Minutes and seconds must be non-negative numbers; seconds must be 0-59.", "OK");
                    return false;
                }
                await _storyService.SetYouTubeStatsAsync(project.Id, views, likes, comments, durationSeconds);
                return true;
            });
        return Task.CompletedTask;
    }

    private Task ShowEditFacebookStatsAsync(StoryProject project)
    {
        var overlay = CreateOverlay(out var stack, "Edit Facebook Stats", "#1877F2", project.Name);
        bool hasStats = project.FacebookStatsCapturedAt.HasValue;
        var viewsEntry = CreateStatsEntry(hasStats ? project.FacebookViews.ToString(CultureInfo.InvariantCulture) : "0", "Views", Keyboard.Numeric);
        var likesEntry = CreateStatsEntry(hasStats ? project.FacebookLikes.ToString(CultureInfo.InvariantCulture) : "0", "Likes", Keyboard.Numeric);
        var commentsEntry = CreateStatsEntry(hasStats ? project.FacebookComments.ToString(CultureInfo.InvariantCulture) : "0", "Comments", Keyboard.Numeric);
        var threeSecondViewsEntry = CreateStatsEntry(hasStats ? project.FacebookThreeSecondViews.ToString(CultureInfo.InvariantCulture) : "0", "3-second views", Keyboard.Numeric);
        var oneMinuteViewsEntry = CreateStatsEntry(hasStats ? project.FacebookOneMinuteViews.ToString(CultureInfo.InvariantCulture) : "0", "1-minute views", Keyboard.Numeric);

        stack.Children.Add(CreateStatsInputRow("Views", viewsEntry));
        stack.Children.Add(CreateStatsInputRow("Likes", likesEntry));
        stack.Children.Add(CreateStatsInputRow("Comments", commentsEntry));
        stack.Children.Add(CreateDurationInputRow("Average view duration", hasStats ? project.FacebookAverageViewDurationSeconds : 0, out var minutesEntry, out var secondsEntry));
        stack.Children.Add(CreateStatsInputRow("3-second views", threeSecondViewsEntry));
        stack.Children.Add(CreateStatsInputRow("1-minute views", oneMinuteViewsEntry));

        AddOverlayFooter(overlay, stack, "#1877F2", "Clear Facebook stats?", "All six values will be reset to empty.",
            async () => await _storyService.ClearFacebookStatsAsync(project.Id),
            async () =>
            {
                int views = ParseNonNegativeIntegerOrZero(viewsEntry.Text);
                int likes = ParseNonNegativeIntegerOrZero(likesEntry.Text);
                int comments = ParseNonNegativeIntegerOrZero(commentsEntry.Text);
                int threeSecondViews = ParseNonNegativeIntegerOrZero(threeSecondViewsEntry.Text);
                int oneMinuteViews = ParseNonNegativeIntegerOrZero(oneMinuteViewsEntry.Text);
                if (!TryReadMinutesSeconds(minutesEntry, secondsEntry, out int durationSeconds))
                {
                    await DisplayAlert("Invalid Duration", "Minutes and seconds must be non-negative numbers; seconds must be 0-59.", "OK");
                    return false;
                }
                await _storyService.SetFacebookStatsAsync(project.Id, views, likes, comments, durationSeconds, threeSecondViews, oneMinuteViews);
                return true;
            });
        return Task.CompletedTask;
    }

    private Task ShowEditTikTokStatsAsync(StoryProject project)
    {
        var overlay = CreateOverlay(out var stack, "Edit TikTok Stats", "#FE2C55", project.Name);
        bool hasStats = project.TikTokStatsCapturedAt.HasValue;
        var viewsEntry = CreateStatsEntry(hasStats ? project.TikTokViews.ToString(CultureInfo.InvariantCulture) : "0", "Views", Keyboard.Numeric);
        var likesEntry = CreateStatsEntry(hasStats ? project.TikTokLikes.ToString(CultureInfo.InvariantCulture) : "0", "Likes", Keyboard.Numeric);
        var commentsEntry = CreateStatsEntry(hasStats ? project.TikTokComments.ToString(CultureInfo.InvariantCulture) : "0", "Comments", Keyboard.Numeric);
        var percentEntry = CreateStatsEntry(hasStats ? project.TikTokPercentWatchedFullVideo.ToString("0.0", CultureInfo.InvariantCulture) : "0.0", "0-100", Keyboard.Numeric);

        stack.Children.Add(CreateStatsInputRow("Views", viewsEntry));
        stack.Children.Add(CreateStatsInputRow("Likes", likesEntry));
        stack.Children.Add(CreateStatsInputRow("Comments", commentsEntry));
        stack.Children.Add(CreateDurationInputRow("Average watch time", hasStats ? project.TikTokAverageWatchTimeSeconds : 0, out var minutesEntry, out var secondsEntry));
        stack.Children.Add(CreateStatsInputRow("Percent watched full video", percentEntry));

        AddOverlayFooter(overlay, stack, "#FE2C55", "Clear TikTok stats?", "All five values will be reset to empty.",
            async () => await _storyService.ClearTikTokStatsAsync(project.Id),
            async () =>
            {
                int views = ParseNonNegativeIntegerOrZero(viewsEntry.Text);
                int likes = ParseNonNegativeIntegerOrZero(likesEntry.Text);
                int comments = ParseNonNegativeIntegerOrZero(commentsEntry.Text);
                if (!TryReadMinutesSeconds(minutesEntry, secondsEntry, out int averageWatchTimeSeconds))
                {
                    await DisplayAlert("Invalid Duration", "Minutes and seconds must be non-negative numbers; seconds must be 0-59.", "OK");
                    return false;
                }
                string percentText = (percentEntry.Text ?? "").Trim();
                double percentWatchedFullVideo = 0.0;
                if (!string.IsNullOrEmpty(percentText)
                    && !double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out percentWatchedFullVideo))
                {
                    await DisplayAlert("Invalid Percent", "Percent must be a number between 0 and 100.", "OK");
                    return false;
                }
                if (percentWatchedFullVideo < 0.0 || percentWatchedFullVideo > 100.0)
                {
                    await DisplayAlert("Invalid Percent", "Percent must be a number between 0 and 100.", "OK");
                    return false;
                }
                await _storyService.SetTikTokStatsAsync(project.Id, views, likes, comments, averageWatchTimeSeconds, percentWatchedFullVideo);
                return true;
            });
        return Task.CompletedTask;
    }

    private Grid CreateOverlay(out VerticalStackLayout stack, string title, string color, string subtitle)
    {
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000"), InputTransparent = false };
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
        stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) });
        stack.Children.Add(new Label { Text = subtitle, FontSize = 13, TextColor = Color.FromArgb("#666") });
        card.Content = stack;
        overlay.Children.Add(card);
        _mainGrid.Children.Add(overlay);
        return overlay;
    }

    private void AddOverlayFooter(Grid overlay, VerticalStackLayout stack, string color, string clearTitle, string clearMessage, Func<Task> clearAction, Func<Task<bool>> saveAction)
    {
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

        var clearBtn = new Button { Text = "Clear stats", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb(color), FontSize = 12, Padding = new Thickness(8, 0) };
        var cancelBtn = new Button { Text = "Cancel", BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#333"), CornerRadius = 8, Padding = new Thickness(18, 8) };
        var saveBtn = new Button { Text = "Save", BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, CornerRadius = 8, Padding = new Thickness(22, 8) };

        Grid.SetColumn(clearBtn, 0);
        Grid.SetColumn(cancelBtn, 2);
        Grid.SetColumn(saveBtn, 3);
        footer.Children.Add(clearBtn);
        footer.Children.Add(cancelBtn);
        footer.Children.Add(saveBtn);
        stack.Children.Add(footer);

        void CloseOverlay() => _mainGrid.Children.Remove(overlay);
        cancelBtn.Clicked += (s, e) => CloseOverlay();
        clearBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert(clearTitle, clearMessage, "Clear", "Cancel");
            if (!confirm) return;
            try
            {
                await clearAction();
                CloseOverlay();
                await LoadDetailAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };
        saveBtn.Clicked += async (s, e) =>
        {
            try
            {
                bool saved = await saveAction();
                if (!saved) return;
                CloseOverlay();
                await LoadDetailAsync();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };
    }

    private async Task ShowProjectSettingsAsync(StoryProject project)
    {
        var options = new List<string> { "📅 Edit Start Date" };
        options.Add("🔁 Toggle Published");

        if (!project.IsProduced)
        {
            options.Add("✅ Mark as Produced");
        }
        else
        {
            options.Add("📅 Edit Produced Date");
            options.Add("↩️ Unmark Produced");
        }

        if (project.IsPublished)
            options.Add("📅 Edit Published Date");
        options.Add("📐 Set Projections");
        options.Add("🧬 Set Stats Source Draft");

        if (project.IsProduced)
        {
            options.Add("✏️ Edit Clip Count");
        }

        var action = await DisplayActionSheet(project.Name, "Cancel", null, options.ToArray());
        if (action == null || action == "Cancel") return;

        if (action.Contains("Edit Start Date")) await EditStartDateAsync(project);
        else if (action.Contains("Edit Published Date")) await EditPublishedDateAsync(project);
        else if (action.Contains("Edit Produced Date")) await EditProducedDateAsync(project);
        else if (action.Contains("Mark as Produced")) await MarkProducedAsync(project);
        else if (action.Contains("Toggle Published")) await TogglePublishedAsync(project);
        else if (action.Contains("Set Projections")) await SetProjectionsAsync(project);
        else if (action.Contains("Set Stats Source Draft")) await SetStatsSourceDraftAsync(project);
        else if (action.Contains("Unmark Produced"))
        {
            bool confirm = await DisplayAlert("Unmark Produced", $"Revert '{project.Name}' to not produced? Published state will be preserved.", "Yes", "No");
            if (!confirm) return;
            try { await _storyService.UnmarkProducedAsync(project.Id); await LoadDetailAsync(); }
            catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
        }
        else if (action.Contains("Edit Clip Count"))
        {
            string? clipStr = await DisplayPromptAsync("Edit Clip Count", "Final clip count:", "Save", "Cancel", initialValue: project.FinalClipCount.ToString(), keyboard: Keyboard.Numeric);
            if (!string.IsNullOrEmpty(clipStr) && int.TryParse(clipStr, out int clips))
            {
                try { project.FinalClipCount = clips; await _storyService.UpdateProjectAsync(project); await LoadDetailAsync(); }
                catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
            }
        }
    }

    private async Task EditStartDateAsync(StoryProject project)
    {
        string? newDateStr = await DisplayPromptAsync("Edit Start Date", $"Current: {project.CreatedAt:MMM d, yyyy}\n\nEnter new date (YYYY-MM-DD):", "Save", "Cancel", initialValue: project.CreatedAt.ToString("yyyy-MM-dd"));
        if (string.IsNullOrEmpty(newDateStr)) return;
        if (!DateTime.TryParse(newDateStr, out DateTime newDate))
        {
            await DisplayAlert("Invalid Date", "Please use format YYYY-MM-DD (e.g., 2026-04-01)", "OK");
            return;
        }
        try { project.CreatedAt = newDate; await _storyService.UpdateProjectAsync(project); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }

    private async Task EditPublishedDateAsync(StoryProject project)
    {
        if (!project.PublishedAt.HasValue) return;
        string? newDateStr = await DisplayPromptAsync("Edit Published Date", $"Current: {project.PublishedAt.Value:MMM d, yyyy}\n\nEnter new date (YYYY-MM-DD):", "Save", "Cancel", initialValue: project.PublishedAt.Value.ToString("yyyy-MM-dd"));
        if (string.IsNullOrEmpty(newDateStr)) return;
        if (!DateTime.TryParse(newDateStr, out DateTime newDate))
        {
            await DisplayAlert("Invalid Date", "Please use format YYYY-MM-DD (e.g., 2026-04-08)", "OK");
            return;
        }
        try { project.PublishedAt = newDate; project.CompletedAt = newDate; await _storyService.UpdateProjectAsync(project); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }

    private async Task EditProducedDateAsync(StoryProject project)
    {
        if (!project.ProducedAt.HasValue) return;
        string? newDateStr = await DisplayPromptAsync("Edit Produced Date", $"Current: {project.ProducedAt.Value:MMM d, yyyy}\n\nEnter new date (YYYY-MM-DD):", "Save", "Cancel", initialValue: project.ProducedAt.Value.ToString("yyyy-MM-dd"));
        if (string.IsNullOrEmpty(newDateStr)) return;
        if (!DateTime.TryParse(newDateStr, out DateTime newDate))
        {
            await DisplayAlert("Invalid Date", "Please use format YYYY-MM-DD (e.g., 2026-04-08)", "OK");
            return;
        }
        try { project.ProducedAt = newDate; project.CompletedAt = newDate; await _storyService.UpdateProjectAsync(project); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }

    private async Task MarkProducedAsync(StoryProject project)
    {
        var lines = await _storyService.GetLinesAsync(project.Id);
        int clipCount = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            clipCount += shots.Count > 0 ? shots.Count : 1;
        }

        string? clipStr = await DisplayPromptAsync("Mark as Produced", "Final clip count:", "Save", "Cancel", initialValue: clipCount.ToString(), keyboard: Keyboard.Numeric);
        if (string.IsNullOrEmpty(clipStr) || !int.TryParse(clipStr, out int finalClips)) return;
        try { await _storyService.MarkProducedAsync(project.Id, finalClips); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }

    private async Task TogglePublishedAsync(StoryProject project)
    {
        bool newState = !project.IsPublished;
        string message = newState
            ? $"Mark '{project.Name}' as published?"
            : $"Mark '{project.Name}' as unpublished? Published date will be preserved.";
        bool confirm = await DisplayAlert("Toggle Published", message, "Yes", "No");
        if (!confirm) return;

        try { await _storyService.TogglePublishedAsync(project.Id, newState); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }

    private async Task SetStatsSourceDraftAsync(StoryProject project)
    {
        var drafts = await _storyService.GetProjectDraftsAsync(project.Id);
        var resolved = await _storyService.ResolveStatsSourceDraftAsync(project) ?? project;

        var options = new List<string>();
        options.Add(project.StatsSourceDraftProjectId == null ? "✓ Auto (latest)" : "Auto (latest)");

        foreach (var draft in drafts.OrderBy(d => d.DraftVersion))
        {
            string label = draft.ParentProjectId == null
                ? "Original"
                : $"Draft {draft.DraftVersion}: {draft.Name}";
            if (draft.Id == resolved.Id)
                label = "✓ " + label;
            options.Add(label);
        }

        var action = await DisplayActionSheet("Stats Source Draft", "Cancel", null, options.ToArray());
        if (action == null || action == "Cancel") return;

        int? selectedId = null;
        if (!action.Contains("Auto"))
        {
            string normalized = action.StartsWith("✓ ") ? action.Substring(2) : action;
            var selected = drafts.FirstOrDefault(d =>
            {
                string label = d.ParentProjectId == null
                    ? "Original"
                    : $"Draft {d.DraftVersion}: {d.Name}";
                return label == normalized;
            });
            if (selected == null) return;
            selectedId = selected.Id;
        }

        try { await _storyService.SetStatsSourceDraftAsync(project.Id, selectedId); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }

    private async Task SetProjectionsAsync(StoryProject project)
    {
        string? clipsStr = await DisplayPromptAsync("Projected Clips", "Expected number of clips:", "Next", "Cancel", initialValue: project.ProjectedClipCount > 0 ? project.ProjectedClipCount.ToString() : "", keyboard: Keyboard.Numeric);
        if (string.IsNullOrEmpty(clipsStr) || !int.TryParse(clipsStr, out int projectedClips)) return;
        string? daysStr = await DisplayPromptAsync("Projected Days", "Expected production days:", "Save", "Cancel", initialValue: project.ProjectedDays > 0 ? project.ProjectedDays.ToString() : "", keyboard: Keyboard.Numeric);
        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int projectedDays)) return;
        try { await _storyService.SetProjectionsAsync(project.Id, projectedClips, projectedDays); await LoadDetailAsync(); }
        catch (ReadOnlyDatabaseException) { await ShowReadOnlyAlertAsync(); }
    }
}
