using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for organizing story points into categories: Active (Chronological/Misc), Partial, Pending, Possible, Archived.
/// Allows reordering within categories and exporting for discussion.
/// </summary>
public class StoryPointsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly StoryProject _project;
    private readonly IdeasService? _ideasService;
    private readonly IdeaLoggerService? _ideaLogger;

    private VerticalStackLayout _activeChronologicalStack;
    private VerticalStackLayout _activeMiscStack;
    private VerticalStackLayout _partialStack;
    private VerticalStackLayout _pendingStack;
    private VerticalStackLayout _possibleStack;
    private VerticalStackLayout _archivedStack;
    private Label _archivedHeaderLabel;
    private Grid _loadingOverlay;
    
    // Version management
    private int _currentVersion = 1;
    private List<StoryPointVersion> _versions = new();
    private Picker _versionPicker;
    private Label _versionInfoLabel;

    private List<StoryPoint> _points = new();

    public StoryPointsPage(AuthService auth, StoryProductionService storyService, StoryProject project, IdeasService? ideasService = null, IdeaLoggerService? ideaLogger = null)
    {
        _auth = auth;
        _storyService = storyService;
        _project = project;
        _ideasService = ideasService;
        _ideaLogger = ideaLogger;

        Title = "Story Points";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadVersionsAsync();
        await LoadPointsAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();

        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header with project name and export button
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var headerStack = new VerticalStackLayout();
        headerStack.Children.Add(new Label
        {
            Text = "📝 Story Points",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        headerStack.Children.Add(new Label
        {
            Text = _project.Name,
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });
        headerRow.Children.Add(headerStack);

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

        // Version selector row
        var versionRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };

        versionRow.Children.Add(new Label
        {
            Text = "Version:",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        _versionPicker = new Picker
        {
            FontSize = 14,
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill
        };
        _versionPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_versionPicker.SelectedIndex >= 0 && _versionPicker.SelectedIndex < _versions.Count)
            {
                _currentVersion = _versions[_versionPicker.SelectedIndex].Version;
                await LoadPointsAsync();
            }
        };
        Grid.SetColumn(_versionPicker, 1);
        versionRow.Children.Add(_versionPicker);

        _versionInfoLabel = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(_versionInfoLabel, 2);
        versionRow.Children.Add(_versionInfoLabel);

        var versionMenuBtn = new Button
        {
            Text = "⚙️",
            FontSize = 14,
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            TextColor = Color.FromArgb("#666"),
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            CornerRadius = 6
        };
        ToolTipProperties.SetText(versionMenuBtn, "Version options");
        versionMenuBtn.Clicked += OnVersionMenuClicked;
        Grid.SetColumn(versionMenuBtn, 3);
        versionRow.Children.Add(versionMenuBtn);

        mainStack.Children.Add(versionRow);

        // Active Points - Chronological Section (story order, start to end)
        mainStack.Children.Add(CreateSubSectionHeader("📖 Active: Chronological", "chronological", Color.FromArgb("#4CAF50")));
        _activeChronologicalStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_activeChronologicalStack);

        // Active Points - Misc Section (importance order)
        mainStack.Children.Add(CreateSubSectionHeader("📌 Active: Misc", "misc", Color.FromArgb("#8BC34A")));
        _activeMiscStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_activeMiscStack);

        // Partial Points Section (partially covered in draft)
        mainStack.Children.Add(CreateSectionHeader("🔶 Partial Points", "partial", Color.FromArgb("#E91E63")));
        _partialStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_partialStack);

        // Pending Points Section (want to include, not done yet)
        mainStack.Children.Add(CreateSectionHeader("⏳ Pending Points", "pending", Color.FromArgb("#9C27B0")));
        _pendingStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_pendingStack);

        // Possible Points Section (considering including)
        mainStack.Children.Add(CreateSectionHeader("💡 Possible Points", "possible", Color.FromArgb("#FF9800")));
        _possibleStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_possibleStack);

        // Archived Points Section (collapsible, collapsed by default)
        var archivedHeader = CreateCollapsibleSectionHeader("📦 Archived Points", "archived", Color.FromArgb("#9E9E9E"));
        mainStack.Children.Add(archivedHeader);
        _archivedStack = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        mainStack.Children.Add(_archivedStack);

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
            WidthRequest = 40,
            HeightRequest = 40,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        });
        mainGrid.Children.Add(_loadingOverlay);

        Content = mainGrid;
    }

    private View CreateSectionHeader(string title, string category, Color color)
    {
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6,
            Margin = new Thickness(0, 12, 0, 4)
        };

        headerRow.Children.Add(new Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = color,
            VerticalOptions = LayoutOptions.Center
        });

        var addBtn = new Button
        {
            Text = "+ Add",
            BackgroundColor = color.WithAlpha(0.15f),
            TextColor = color,
            FontSize = 12,
            HeightRequest = 30,
            CornerRadius = 6,
            Padding = new Thickness(10, 0)
        };
        addBtn.Clicked += async (s, e) => await AddPointAsync(category);
        Grid.SetColumn(addBtn, 1);
        headerRow.Children.Add(addBtn);

        var logIdeaBtn = new Button
        {
            Text = "💡",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#F57C00"),
            FontSize = 14,
            HeightRequest = 30,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0
        };
        logIdeaBtn.Clicked += async (s, e) => await LogIdeaAsync();
        Grid.SetColumn(logIdeaBtn, 2);
        headerRow.Children.Add(logIdeaBtn);

        return headerRow;
    }

    private View CreateSubSectionHeader(string title, string subcategory, Color color)
    {
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6,
            Margin = new Thickness(0, 12, 0, 4)
        };

        headerRow.Children.Add(new Label
        {
            Text = title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = color,
            VerticalOptions = LayoutOptions.Center
        });

        var addBtn = new Button
        {
            Text = "+ Add",
            BackgroundColor = color.WithAlpha(0.15f),
            TextColor = color,
            FontSize = 12,
            HeightRequest = 30,
            CornerRadius = 6,
            Padding = new Thickness(10, 0)
        };
        addBtn.Clicked += async (s, e) => await ShowAddPointOptionsAsync(subcategory);
        Grid.SetColumn(addBtn, 1);
        headerRow.Children.Add(addBtn);

        var logIdeaBtn = new Button
        {
            Text = "💡",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#F57C00"),
            FontSize = 14,
            HeightRequest = 30,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0
        };
        logIdeaBtn.Clicked += async (s, e) => await LogIdeaAsync();
        Grid.SetColumn(logIdeaBtn, 2);
        headerRow.Children.Add(logIdeaBtn);

        return headerRow;
    }

    private async Task ShowAddPointOptionsAsync(string subcategory)
    {
        // Get points in this subcategory
        var pointsInSubcategory = _points
            .Where(p => p.Category == "active" && 
                (subcategory == "chronological" 
                    ? (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory))
                    : p.Subcategory == "misc"))
            .OrderBy(p => p.DisplayOrder)
            .ToList();

        var options = new List<string> { "📥 Add at end" };
        
        if (pointsInSubcategory.Count > 0)
        {
            options.Add("📍 Add before a point...");
        }

        var result = await DisplayActionSheet("Add Point", "Cancel", null, options.ToArray());
        if (result == null || result == "Cancel") return;

        if (result == "📥 Add at end")
        {
            await AddPointAsync("active", subcategory);
        }
        else if (result == "📍 Add before a point...")
        {
            await AddPointBeforeAsync(subcategory, pointsInSubcategory);
        }
    }

    private async Task AddPointBeforeAsync(string subcategory, List<StoryPoint> pointsInSubcategory)
    {
        // Use a custom overlay to show full point text
        var tcs = new TaskCompletionSource<StoryPoint?>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            ZIndex = 1000
        };

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D30"),
            BorderColor = Color.FromArgb("#3E3E42"),
            CornerRadius = 12,
            Padding = new Thickness(20),
            MaximumWidthRequest = 900,
            MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = false
        };

        var mainLayout = new VerticalStackLayout { Spacing = 16 };

        // Header
        mainLayout.Children.Add(new Label
        {
            Text = "📍 Add Before Which Point?",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        mainLayout.Children.Add(new Label
        {
            Text = "Select a point. New point will be inserted above it.",
            FontSize = 13,
            TextColor = Color.FromArgb("#AAAAAA")
        });

        // Scrollable list of points
        var scrollView = new ScrollView
        {
            MaximumHeightRequest = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always
        };

        var pointsList = new VerticalStackLayout { Spacing = 8 };

        for (int i = 0; i < pointsInSubcategory.Count; i++)
        {
            var point = pointsInSubcategory[i];
            int index = i;

            var pointCard = new Frame
            {
                BackgroundColor = Color.FromArgb("#3E3E42"),
                BorderColor = Color.FromArgb("#4CAF50"),
                CornerRadius = 8,
                Padding = new Thickness(12, 10),
                HasShadow = false
            };

            var pointLayout = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 12
            };

            // Index number
            pointLayout.Children.Add(new Label
            {
                Text = $"{i + 1}.",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#4CAF50"),
                VerticalOptions = LayoutOptions.Start,
                WidthRequest = 30
            });

            // Full point text
            var textLabel = new Label
            {
                Text = point.Text,
                FontSize = 13,
                TextColor = Colors.White,
                LineBreakMode = LineBreakMode.WordWrap
            };
            Grid.SetColumn(textLabel, 1);
            pointLayout.Children.Add(textLabel);

            pointCard.Content = pointLayout;

            // Make clickable
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                CloseOverlay(overlay);
                tcs.TrySetResult(point);
            };
            pointCard.GestureRecognizers.Add(tapGesture);

            pointsList.Children.Add(pointCard);
        }

        scrollView.Content = pointsList;
        mainLayout.Children.Add(scrollView);

        // Cancel button
        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#4A4A4A"),
            TextColor = Colors.White,
            WidthRequest = 100,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.End
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(null);
        };
        mainLayout.Children.Add(cancelBtn);

        card.Content = mainLayout;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            pageGrid.Children.Add(overlay);
        }

        var selectedPoint = await tcs.Task;
        if (selectedPoint == null) return;

        // Get the new point text
        string subLabel = subcategory == "chronological" ? "chronological" : "misc";
        string? text = await ShowMultiLineInputAsync("Add Point", $"Enter new {subLabel} point (will be inserted before selected):", "");
        
        if (string.IsNullOrWhiteSpace(text)) return;

        _loadingOverlay.IsVisible = true;

        try
        {
            await _storyService.InsertStoryPointBeforeAsync(_project.Id, selectedPoint.Id, text.Trim(), "active", subcategory);
            await LoadPointsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    private View CreateCollapsibleSectionHeader(string title, string category, Color color)
    {
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(0, 12, 0, 4)
        };

        // Collapse/expand indicator
        var collapseLabel = new Label
        {
            Text = "▶",
            FontSize = 14,
            TextColor = color,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        headerRow.Children.Add(collapseLabel);

        // Title with count placeholder
        _archivedHeaderLabel = new Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = color,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(_archivedHeaderLabel, 1);
        headerRow.Children.Add(_archivedHeaderLabel);

        var addBtn = new Button
        {
            Text = "+ Add",
            BackgroundColor = color.WithAlpha(0.15f),
            TextColor = color,
            FontSize = 12,
            HeightRequest = 30,
            CornerRadius = 6,
            Padding = new Thickness(10, 0)
        };
        addBtn.Clicked += async (s, e) => await AddPointAsync(category);
        Grid.SetColumn(addBtn, 2);
        headerRow.Children.Add(addBtn);

        // Tap to expand/collapse
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            _archivedStack.IsVisible = !_archivedStack.IsVisible;
            collapseLabel.Text = _archivedStack.IsVisible ? "▼" : "▶";
        };
        headerRow.GestureRecognizers.Add(tapGesture);

        return headerRow;
    }

    private async Task LoadVersionsAsync()
    {
        _versions = await _storyService.GetStoryPointVersionsAsync(_project.Id);
        
        // If no versions exist, we'll create one when first point is added
        if (_versions.Count == 0)
        {
            _currentVersion = 1;
        }
        else
        {
            _currentVersion = _versions.Max(v => v.Version);
        }

        UpdateVersionPicker();
    }

    private void UpdateVersionPicker()
    {
        _versionPicker.Items.Clear();
        
        int maxVersion = _versions.Count > 0 ? _versions.Max(x => x.Version) : 1;
        
        foreach (var v in _versions.OrderByDescending(v => v.Version))
        {
            string label = string.IsNullOrEmpty(v.Name) ? $"Version {v.Version}" : $"v{v.Version}: {v.Name}";
            if (v.IsLatest)
                label += " ★";
            else if (v.Version == maxVersion)
                label += " (newest)";
            _versionPicker.Items.Add(label);
        }

        // Select current version
        int selectedIndex = _versions.OrderByDescending(v => v.Version).ToList().FindIndex(v => v.Version == _currentVersion);
        if (selectedIndex >= 0)
            _versionPicker.SelectedIndex = selectedIndex;

        // Update info label
        var currentVersionMeta = _versions.FirstOrDefault(v => v.Version == _currentVersion);
        if (currentVersionMeta != null)
        {
            string latestIndicator = currentVersionMeta.IsLatest ? " ★ Latest" : "";
            _versionInfoLabel.Text = $"Created {currentVersionMeta.CreatedAt:MMM d, yyyy}{latestIndicator}";
        }
        else
        {
            _versionInfoLabel.Text = "";
        }
    }

    private async void OnVersionMenuClicked(object? sender, EventArgs e)
    {
        var currentVersionMeta = _versions.FirstOrDefault(v => v.Version == _currentVersion);
        bool isNewest = _currentVersion == (_versions.Count > 0 ? _versions.Max(v => v.Version) : 1);
        bool isMarkedLatest = currentVersionMeta?.IsLatest ?? false;
        
        var options = new List<string>
        {
            "📸 New version (start fresh)",
            "📋 New version from this snapshot",
        };
        
        if (_versions.Count > 0)
        {
            options.Add("✏️ Rename this version");
        }
        
        // Add set as latest option if not already latest
        if (!isMarkedLatest)
        {
            options.Add("⭐ Set as Latest");
        }
        
        if (!isNewest && _versions.Count > 1)
        {
            options.Add("🗑️ Delete this version");
        }

        var result = await DisplayActionSheet("Version Options", "Cancel", null, options.ToArray());
        if (result == null || result == "Cancel") return;

        if (result == "📸 New version (start fresh)")
        {
            string? name = await DisplayPromptAsync("New Version", "Enter a name for the new version:", initialValue: "");
            if (name == null) return;
            
            int newVersion = await _storyService.CreateNewStoryPointVersionAsync(_project.Id, string.IsNullOrWhiteSpace(name) ? null : name.Trim());
            _currentVersion = newVersion;
            await LoadVersionsAsync();
            await LoadPointsAsync();
            await DisplayAlert("Created", $"Created new empty version {newVersion}.", "OK");
        }
        else if (result == "📋 New version from this snapshot")
        {
            string? name = await DisplayPromptAsync("New Version", "Enter a name for the new version:", initialValue: "");
            if (name == null) return;
            
            int sourceVersion = _currentVersion;
            int newVersion = await _storyService.CreateNewStoryPointVersionAsync(_project.Id, string.IsNullOrWhiteSpace(name) ? null : name.Trim());
            await _storyService.DuplicatePointsToVersionAsync(_project.Id, sourceVersion, newVersion);
            _currentVersion = newVersion;
            await LoadVersionsAsync();
            await LoadPointsAsync();
            await DisplayAlert("Created", $"Created version {newVersion} from snapshot of version {sourceVersion}.", "OK");
        }
        else if (result == "✏️ Rename this version")
        {
            var currentMeta = _versions.FirstOrDefault(v => v.Version == _currentVersion);
            string currentName = currentMeta?.Name ?? "";
            
            string? newName = await DisplayPromptAsync("Rename Version", "Enter new name:", initialValue: currentName);
            if (newName == null) return;
            
            await _storyService.RenameStoryPointVersionAsync(_project.Id, _currentVersion, newName.Trim());
            await LoadVersionsAsync();
        }
        else if (result == "⭐ Set as Latest")
        {
            await _storyService.SetStoryPointVersionAsLatestAsync(_project.Id, _currentVersion);
            await LoadVersionsAsync();
            await DisplayAlert("Done", $"Version {_currentVersion} is now marked as latest.", "OK");
        }
        else if (result == "🗑️ Delete this version")
        {
            bool confirm = await DisplayAlert("Delete Version?", 
                $"Delete version {_currentVersion} and all its points?\nThis cannot be undone.", 
                "Delete", "Cancel");
            if (!confirm) return;
            
            await _storyService.DeleteStoryPointVersionAsync(_project.Id, _currentVersion);
            await LoadVersionsAsync();
            
            // Switch to latest version
            _currentVersion = _versions.Count > 0 ? _versions.Max(v => v.Version) : 1;
            await LoadPointsAsync();
        }
    }

    private async Task LoadPointsAsync()
    {
        _loadingOverlay.IsVisible = true;

        try
        {
            _points = await _storyService.GetStoryPointsAsync(_project.Id, _currentVersion);
            RebuildLists();
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    private void RebuildLists()
    {
        _activeChronologicalStack.Children.Clear();
        _activeMiscStack.Children.Clear();
        _partialStack.Children.Clear();
        _pendingStack.Children.Clear();
        _possibleStack.Children.Clear();
        _archivedStack.Children.Clear();

        // Active split by subcategory
        var activeChronological = _points
            .Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory)))
            .OrderBy(p => p.DisplayOrder).ToList();
        var activeMisc = _points
            .Where(p => p.Category == "active" && p.Subcategory == "misc")
            .OrderBy(p => p.DisplayOrder).ToList();
        
        var partial = _points.Where(p => p.Category == "partial").OrderBy(p => p.DisplayOrder).ToList();
        var pending = _points.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();
        var possible = _points.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();
        // Support both "archived" and legacy "irrelevant" category
        var archived = _points.Where(p => p.Category == "archived" || p.Category == "irrelevant").OrderBy(p => p.DisplayOrder).ToList();

        for (int i = 0; i < activeChronological.Count; i++)
            _activeChronologicalStack.Children.Add(CreatePointCard(activeChronological[i], i, activeChronological.Count));

        for (int i = 0; i < activeMisc.Count; i++)
            _activeMiscStack.Children.Add(CreatePointCard(activeMisc[i], i, activeMisc.Count));

        for (int i = 0; i < partial.Count; i++)
            _partialStack.Children.Add(CreatePointCard(partial[i], i, partial.Count));

        for (int i = 0; i < pending.Count; i++)
            _pendingStack.Children.Add(CreatePointCard(pending[i], i, pending.Count));

        for (int i = 0; i < possible.Count; i++)
            _possibleStack.Children.Add(CreatePointCard(possible[i], i, possible.Count));

        for (int i = 0; i < archived.Count; i++)
            _archivedStack.Children.Add(CreatePointCard(archived[i], i, archived.Count));

        // Add empty state labels
        if (activeChronological.Count == 0)
            _activeChronologicalStack.Children.Add(CreateEmptyLabel("No chronological points yet"));
        if (activeMisc.Count == 0)
            _activeMiscStack.Children.Add(CreateEmptyLabel("No misc points yet"));
        if (partial.Count == 0)
            _partialStack.Children.Add(CreateEmptyLabel("No partial points yet"));
        if (pending.Count == 0)
            _pendingStack.Children.Add(CreateEmptyLabel("No pending points yet"));
        if (possible.Count == 0)
            _possibleStack.Children.Add(CreateEmptyLabel("No possible points yet"));
        if (archived.Count == 0)
            _archivedStack.Children.Add(CreateEmptyLabel("No archived points yet"));

        // Update archived header with count
        _archivedHeaderLabel.Text = $"📦 Archived Points ({archived.Count})";
    }

    private Label CreateEmptyLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8)
        };
    }

    private Frame CreatePointCard(StoryPoint point, int index, int total)
    {
        Color categoryColor = point.Category switch
        {
            "active" => Color.FromArgb("#4CAF50"),
            "partial" => Color.FromArgb("#E91E63"),
            "pending" => Color.FromArgb("#9C27B0"),
            "possible" => Color.FromArgb("#FF9800"),
            "archived" => Color.FromArgb("#9E9E9E"),
            "irrelevant" => Color.FromArgb("#9E9E9E"), // Legacy support
            _ => Color.FromArgb("#666")
        };

        // Determine background color based on lock status
        Color bgColor = Colors.White;
        Color borderColor = categoryColor.WithAlpha(0.3f);
        
        if (point.IsCategoryLocked && point.IsSubcategoryLocked)
        {
            bgColor = Color.FromArgb("#FFF3E0"); // Light orange - both locked
            borderColor = Color.FromArgb("#FF5722");
        }
        else if (point.IsCategoryLocked)
        {
            bgColor = Color.FromArgb("#FFEBEE"); // Light red - category locked
            borderColor = Color.FromArgb("#E91E63");
        }
        else if (point.IsSubcategoryLocked)
        {
            bgColor = Color.FromArgb("#FFFDE7"); // Light yellow - subcategory locked
            borderColor = Color.FromArgb("#FFC107");
        }

        var card = new Frame
        {
            Padding = 10,
            CornerRadius = 8,
            BackgroundColor = bgColor,
            BorderColor = borderColor,
            HasShadow = false
        };

        var mainRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),  // Lock indicators + Arrows
                new ColumnDefinition(GridLength.Star),  // Text
                new ColumnDefinition(GridLength.Auto)   // Quick actions + Menu
            },
            ColumnSpacing = 8
        };

        // Left side: Lock indicators + Up/Down arrows
        var leftStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        // Lock indicators
        var lockIndicators = new HorizontalStackLayout { Spacing = 2 };
        
        if (point.IsCategoryLocked)
        {
            var catLockIndicator = new Label
            {
                Text = "🔐",
                FontSize = 10,
                VerticalOptions = LayoutOptions.Center
            };
            ToolTipProperties.SetText(catLockIndicator, "Category locked - won't be moved between Active/Pending/etc");
            lockIndicators.Children.Add(catLockIndicator);
        }
        
        if (point.Category == "active" && point.IsSubcategoryLocked)
        {
            var subLockIndicator = new Label
            {
                Text = "🔒",
                FontSize = 10,
                VerticalOptions = LayoutOptions.Center
            };
            ToolTipProperties.SetText(subLockIndicator, "Subcategory locked - won't be moved between Chrono/Misc");
            lockIndicators.Children.Add(subLockIndicator);
        }
        
        if (lockIndicators.Children.Count > 0)
        {
            leftStack.Children.Add(lockIndicators);
        }

        // Up/Down arrows
        var arrowStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        
        var upBtn = new Button
        {
            Text = "▲",
            FontSize = 10,
            BackgroundColor = index > 0 ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#F5F5F5"),
            TextColor = index > 0 ? Color.FromArgb("#333") : Color.FromArgb("#CCC"),
            WidthRequest = 28,
            HeightRequest = 24,
            Padding = 0,
            CornerRadius = 4
        };
        ToolTipProperties.SetText(upBtn, "Move up");
        if (index > 0)
        {
            upBtn.Clicked += async (s, e) =>
            {
                await _storyService.ReorderStoryPointAsync(point.Id, moveUp: true);
                await LoadPointsAsync();
            };
        }
        arrowStack.Children.Add(upBtn);

        var downBtn = new Button
        {
            Text = "▼",
            FontSize = 10,
            BackgroundColor = index < total - 1 ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#F5F5F5"),
            TextColor = index < total - 1 ? Color.FromArgb("#333") : Color.FromArgb("#CCC"),
            WidthRequest = 28,
            HeightRequest = 24,
            Padding = 0,
            CornerRadius = 4
        };
        ToolTipProperties.SetText(downBtn, "Move down");
        if (index < total - 1)
        {
            downBtn.Clicked += async (s, e) =>
            {
                await _storyService.ReorderStoryPointAsync(point.Id, moveUp: false);
                await LoadPointsAsync();
            };
        }
        arrowStack.Children.Add(downBtn);
        leftStack.Children.Add(arrowStack);

        Grid.SetColumn(leftStack, 0);
        mainRow.Children.Add(leftStack);

        // Text
        var textLabel = new Label
        {
            Text = point.Text,
            FontSize = 14,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        Grid.SetColumn(textLabel, 1);
        mainRow.Children.Add(textLabel);

        // Right side: Quick action buttons + 3-dot menu
        var rightStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        // Quick action: Subcategory toggle for active points
        if (point.Category == "active")
        {
            bool isChronological = point.Subcategory != "misc";
            var toggleSubBtn = new Button
            {
                Text = isChronological ? "📌" : "📖",
                FontSize = 12,
                BackgroundColor = isChronological ? Color.FromArgb("#F1F8E9") : Color.FromArgb("#E8F5E9"),
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0,
                CornerRadius = 6
            };
            ToolTipProperties.SetText(toggleSubBtn, isChronological ? "Move to Misc" : "Move to Chronological");
            toggleSubBtn.Clicked += async (s, e) =>
            {
                string newSubcategory = isChronological ? "misc" : "chronological";
                await _storyService.UpdateStoryPointSubcategoryAsync(point.Id, newSubcategory);
                await LoadPointsAsync();
            };
            rightStack.Children.Add(toggleSubBtn);

            // Subcategory lock toggle button (only for active)
            var subLockBtn = new Button
            {
                Text = point.IsSubcategoryLocked ? "🔓" : "🔒",
                FontSize = 12,
                BackgroundColor = point.IsSubcategoryLocked ? Color.FromArgb("#FFF8E1") : Color.FromArgb("#F5F5F5"),
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0,
                CornerRadius = 6
            };
            ToolTipProperties.SetText(subLockBtn, point.IsSubcategoryLocked ? "Unlock subcategory" : "Lock subcategory (prevent LLM moving between Chrono/Misc)");
            subLockBtn.Clicked += async (s, e) =>
            {
                point.IsSubcategoryLocked = !point.IsSubcategoryLocked;
                await _storyService.UpdateStoryPointAsync(point);
                await LoadPointsAsync();
            };
            rightStack.Children.Add(subLockBtn);
        }

        // Category lock toggle button (for all points)
        var catLockBtn = new Button
        {
            Text = point.IsCategoryLocked ? "🔐" : "📍",
            FontSize = 12,
            BackgroundColor = point.IsCategoryLocked ? Color.FromArgb("#FFEBEE") : Color.FromArgb("#F5F5F5"),
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = 0,
            CornerRadius = 6
        };
        ToolTipProperties.SetText(catLockBtn, point.IsCategoryLocked 
            ? "Unlock category (allow moving between Active/Pending/etc)" 
            : "Lock category (prevent moving between Active/Pending/etc)");
        catLockBtn.Clicked += async (s, e) =>
        {
            point.IsCategoryLocked = !point.IsCategoryLocked;
            await _storyService.UpdateStoryPointAsync(point);
            await LoadPointsAsync();
        };
        rightStack.Children.Add(catLockBtn);

        // 3-dot menu button
        var menuBtn = new Button
        {
            Text = "⋮",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            TextColor = Color.FromArgb("#666"),
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = 0,
            CornerRadius = 6
        };
        ToolTipProperties.SetText(menuBtn, "More options");
        menuBtn.Clicked += async (s, e) => await ShowPointContextMenuAsync(point);
        rightStack.Children.Add(menuBtn);

        Grid.SetColumn(rightStack, 2);
        mainRow.Children.Add(rightStack);

        card.Content = mainRow;
        return card;
    }

    private async Task ShowPointContextMenuAsync(StoryPoint point)
    {
        var options = new List<string>();

        // Category moves
        if (point.Category != "active")
        {
            options.Add("✅ Move to Active: Chronological");
            options.Add("✅ Move to Active: Misc");
        }
        
        if (point.Category == "active")
        {
            bool isChronological = point.Subcategory != "misc";
            options.Add(isChronological ? "📌 Move to Misc" : "📖 Move to Chronological");
            options.Add(point.IsSubcategoryLocked ? "🔓 Unlock subcategory" : "🔒 Lock subcategory");
        }
        
        if (point.Category != "partial")
            options.Add("🔶 Move to Partial");
        if (point.Category != "pending")
            options.Add("⏳ Move to Pending");
        if (point.Category != "possible")
            options.Add("💡 Move to Possible");
        if (point.Category != "archived" && point.Category != "irrelevant")
            options.Add("📦 Move to Archived");
        
        options.Add("───────────");
        options.Add(point.IsCategoryLocked ? "🔓 Unlock category" : "🔐 Lock category");
        options.Add("✏️ Edit text");
        
        // Insert in Draft option (for active points)
        if (point.Category == "active")
        {
            options.Add("📝 Insert in Draft (LLM)");
        }
        
        // Bulk lock options
        options.Add("───────────");
        string categoryLabel = GetCategoryLabel(point.Category);
        options.Add($"🔐 Lock all in {categoryLabel}");
        options.Add($"🔓 Unlock all in {categoryLabel}");
        
        if (point.Category == "active")
        {
            string subLabel = point.Subcategory == "misc" ? "Misc" : "Chronological";
            options.Add($"🔒 Lock all in {subLabel}");
            options.Add($"🔓 Unlock all in {subLabel}");
        }

        var result = await DisplayActionSheet("Point Options", "Cancel", null, options.ToArray());
        if (result == null || result == "Cancel" || result.StartsWith("───")) return;

        if (result == "✅ Move to Active: Chronological")
        {
            await _storyService.MoveStoryPointToCategoryAsync(point.Id, "active", "chronological");
        }
        else if (result == "✅ Move to Active: Misc")
        {
            await _storyService.MoveStoryPointToCategoryAsync(point.Id, "active", "misc");
        }
        else if (result == "📌 Move to Misc")
        {
            await _storyService.UpdateStoryPointSubcategoryAsync(point.Id, "misc");
        }
        else if (result == "📖 Move to Chronological")
        {
            await _storyService.UpdateStoryPointSubcategoryAsync(point.Id, "chronological");
        }
        else if (result == "🔓 Unlock subcategory" || result == "🔒 Lock subcategory")
        {
            point.IsSubcategoryLocked = !point.IsSubcategoryLocked;
            await _storyService.UpdateStoryPointAsync(point);
        }
        else if (result == "🔓 Unlock category" || result == "🔐 Lock category")
        {
            point.IsCategoryLocked = !point.IsCategoryLocked;
            await _storyService.UpdateStoryPointAsync(point);
        }
        else if (result == "🔶 Move to Partial")
        {
            await _storyService.MoveStoryPointToCategoryAsync(point.Id, "partial");
        }
        else if (result == "⏳ Move to Pending")
        {
            await _storyService.MoveStoryPointToCategoryAsync(point.Id, "pending");
        }
        else if (result == "💡 Move to Possible")
        {
            await _storyService.MoveStoryPointToCategoryAsync(point.Id, "possible");
        }
        else if (result == "📦 Move to Archived")
        {
            await _storyService.MoveStoryPointToCategoryAsync(point.Id, "archived");
        }
        else if (result == "✏️ Edit text")
        {
            await EditPointAsync(point);
            return; // EditPointAsync handles reload
        }
        else if (result == "📝 Insert in Draft (LLM)")
        {
            await ExportInsertPointInDraftAsync(point);
            return; // Don't reload, this is an export operation
        }
        else if (result.StartsWith("🔐 Lock all in"))
        {
            await BulkSetCategoryLockAsync(point.Category, true);
        }
        else if (result.StartsWith("🔓 Unlock all in") && result.Contains(categoryLabel))
        {
            await BulkSetCategoryLockAsync(point.Category, false);
        }
        else if (result == "🔒 Lock all in Chronological")
        {
            await BulkSetSubcategoryLockAsync("chronological", true);
        }
        else if (result == "🔓 Unlock all in Chronological")
        {
            await BulkSetSubcategoryLockAsync("chronological", false);
        }
        else if (result == "🔒 Lock all in Misc")
        {
            await BulkSetSubcategoryLockAsync("misc", true);
        }
        else if (result == "🔓 Unlock all in Misc")
        {
            await BulkSetSubcategoryLockAsync("misc", false);
        }

        await LoadPointsAsync();
    }

    private string GetCategoryLabel(string category)
    {
        return category switch
        {
            "active" => "Active",
            "partial" => "Partial",
            "pending" => "Pending",
            "possible" => "Possible",
            "archived" => "Archived",
            "irrelevant" => "Archived",
            _ => category
        };
    }

    private async Task BulkSetCategoryLockAsync(string category, bool locked)
    {
        var pointsInCategory = _points.Where(p => p.Category == category || 
            (category == "archived" && p.Category == "irrelevant")).ToList();
        
        int count = 0;
        foreach (var p in pointsInCategory)
        {
            if (p.IsCategoryLocked != locked)
            {
                p.IsCategoryLocked = locked;
                await _storyService.UpdateStoryPointAsync(p);
                count++;
            }
        }
        
        string action = locked ? "locked" : "unlocked";
        await DisplayAlert("Done", $"{action.First().ToString().ToUpper() + action.Substring(1)} {count} point(s) in {GetCategoryLabel(category)}.", "OK");
    }

    private async Task BulkSetSubcategoryLockAsync(string subcategory, bool locked)
    {
        var pointsInSubcategory = _points.Where(p => 
            p.Category == "active" && 
            (p.Subcategory == subcategory || (subcategory == "chronological" && string.IsNullOrEmpty(p.Subcategory)))
        ).ToList();
        
        int count = 0;
        foreach (var p in pointsInSubcategory)
        {
            if (p.IsSubcategoryLocked != locked)
            {
                p.IsSubcategoryLocked = locked;
                await _storyService.UpdateStoryPointAsync(p);
                count++;
            }
        }
        
        string subLabel = subcategory == "misc" ? "Misc" : "Chronological";
        string action = locked ? "locked" : "unlocked";
        await DisplayAlert("Done", $"{action.First().ToString().ToUpper() + action.Substring(1)} {count} point(s) in {subLabel}.", "OK");
    }

    private async Task AddPointAsync(string category, string? subcategory = null)
    {
        string label = category;
        if (category == "active" && subcategory != null)
        {
            label = subcategory == "chronological" ? "chronological" : "misc";
        }
        
        string? text = await ShowMultiLineInputAsync("Add Point", $"Enter new {label} point:", "");
        
        if (string.IsNullOrWhiteSpace(text)) return;

        await _storyService.AddStoryPointAsync(_project.Id, text.Trim(), category, subcategory);

        // Also log to Ideas under "all_story_points" category
        if (_ideasService != null)
        {
            try
            {
                await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, text.Trim(), "all_story_points");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STORY POINTS] Failed to log idea: {ex.Message}");
            }
        }

        await LoadPointsAsync();
    }

    private async Task LogIdeaAsync()
    {
        if (_ideaLogger == null) return;
        await _ideaLogger.LogIdeaAsync(this, _auth.CurrentUsername);
    }

    private async Task EditPointAsync(StoryPoint point)
    {
        string? text = await ShowMultiLineInputAsync("Edit Point", "Update point text:", point.Text);

        if (string.IsNullOrWhiteSpace(text)) return;

        point.Text = text.Trim();
        await _storyService.UpdateStoryPointAsync(point);
        await LoadPointsAsync();
    }

    private Task<string?> ShowMultiLineInputAsync(string title, string message, string initialValue)
    {
        var tcs = new TaskCompletionSource<string?>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var card = new Frame
        {
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            Padding = 20,
            WidthRequest = 400,
            MaximumHeightRequest = 400,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            HasShadow = true
        };

        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Title
        mainStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Message
        mainStack.Children.Add(new Label
        {
            Text = message,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        // Multi-line editor
        var editor = new Editor
        {
            Text = initialValue,
            HeightRequest = 150,
            FontSize = 14,
            Placeholder = "Enter text...",
            AutoSize = EditorAutoSizeOption.Disabled
        };
        
        var editorFrame = new Frame
        {
            Padding = 8,
            CornerRadius = 8,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            HasShadow = false,
            Content = editor
        };
        mainStack.Children.Add(editorFrame);

        // Buttons
        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#666"),
            FontSize = 14,
            HeightRequest = 40,
            CornerRadius = 8,
            Padding = new Thickness(16, 0)
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(null);
        };
        btnRow.Children.Add(cancelBtn);

        var addBtn = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontSize = 14,
            HeightRequest = 40,
            CornerRadius = 8,
            Padding = new Thickness(20, 0)
        };
        addBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(editor.Text);
        };
        btnRow.Children.Add(addBtn);

        mainStack.Children.Add(btnRow);

        card.Content = mainStack;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            pageGrid.Children.Add(overlay);
        }

        // Focus the editor
        editor.Focus();

        return tcs.Task;
    }

    private void CloseOverlay(Grid overlay)
    {
        if (this.Content is Grid pageGrid && pageGrid.Children.Contains(overlay))
        {
            pageGrid.Children.Remove(overlay);
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        var options = new[]
        {
            "📋 Export All for Discussion",
            "🔢 Export Active for Reordering (LLM)",
            "📥 Import Reordered Active Points",
            "🎬 Export Points as Draft Prompt (LLM)",
            "🔍 Check Missing/Partial Points in Draft (LLM)",
            "📥 Import Missing/Partial Result",
            "🔶 Export Partial Points for Breakdown (LLM)",
            "📥 Import Partial Breakdown Result",
            "🧹 Export All Points for Cleanup (LLM)",
            "📥 Import Cleanup Result",
            "📝 Export Possible into New Draft (LLM)",
            "✅ Check Possible Now in Draft (LLM)",
            "📥 Import Possible Check Result",
            "───────────",
            "📄 Draft to Points (LLM)",
            "📥 Import Draft to Points Result",
            "───────────",
            "🔀 Compare with Other Version (LLM)",
            "📥 Import Version Comparison Result",
            "───────────",
            "📥 Import Insert Point in Draft Result",
            "───────────",
            "🔧 Surgical Edit (LLM)",
            "📥 Import Surgical Edit Result"
        };

        var result = await DisplayActionSheet("Export Options", "Cancel", null, options);
        if (result == null || result == "Cancel" || result.StartsWith("───")) return;

        if (result == "📋 Export All for Discussion")
        {
            await ExportAllForDiscussionAsync();
        }
        else if (result == "🔢 Export Active for Reordering (LLM)")
        {
            await ExportActiveForReorderingAsync();
        }
        else if (result == "📥 Import Reordered Active Points")
        {
            await ImportReorderedActivePointsAsync();
        }
        else if (result == "🎬 Export Points as Draft Prompt (LLM)")
        {
            await ExportPointsAsDraftPromptAsync();
        }
        else if (result == "🔍 Check Missing/Partial Points in Draft (LLM)")
        {
            await ExportMissingPointsCheckAsync();
        }
        else if (result == "📥 Import Missing/Partial Result")
        {
            await ImportMissingPointsResultAsync();
        }
        else if (result == "🔶 Export Partial Points for Breakdown (LLM)")
        {
            await ExportPartialBreakdownAsync();
        }
        else if (result == "📥 Import Partial Breakdown Result")
        {
            await ImportPartialBreakdownResultAsync();
        }
        else if (result == "🧹 Export All Points for Cleanup (LLM)")
        {
            await ExportPointsForCleanupAsync();
        }
        else if (result == "📥 Import Cleanup Result")
        {
            await ImportCleanupResultAsync();
        }
        else if (result == "📝 Export Possible into New Draft (LLM)")
        {
            await ExportPendingIntoDraftAsync();
        }
        else if (result == "✅ Check Possible Now in Draft (LLM)")
        {
            await ExportCheckPendingInDraftAsync();
        }
        else if (result == "📥 Import Possible Check Result")
        {
            await ImportPendingCheckResultAsync();
        }
        else if (result == "📄 Draft to Points (LLM)")
        {
            await ExportDraftToPointsAsync();
        }
        else if (result == "📥 Import Draft to Points Result")
        {
            await ImportDraftToPointsResultAsync();
        }
        else if (result == "🔀 Compare with Other Version (LLM)")
        {
            await ExportCompareVersionsAsync();
        }
        else if (result == "📥 Import Version Comparison Result")
        {
            await ImportVersionComparisonResultAsync();
        }
        else if (result == "📥 Import Insert Point in Draft Result")
        {
            await ImportInsertPointInDraftResultAsync();
        }
        else if (result == "🔧 Surgical Edit (LLM)")
        {
            await ExportPointsSurgicalEditAsync();
        }
        else if (result == "📥 Import Surgical Edit Result")
        {
            await ImportPointsSurgicalEditResultAsync();
        }
    }

    private async Task ExportAllForDiscussionAsync()
    {
        var chronological = _points
            .Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory)))
            .OrderBy(p => p.DisplayOrder).ToList();
        var misc = _points
            .Where(p => p.Category == "active" && p.Subcategory == "misc")
            .OrderBy(p => p.DisplayOrder).ToList();
        var possible = _points.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();
        var archived = _points.Where(p => p.Category == "archived" || p.Category == "irrelevant").OrderBy(p => p.DisplayOrder).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Story Points: {_project.Name}");
        sb.AppendLine($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        if (chronological.Count > 0)
        {
            sb.AppendLine("## 📖 ACTIVE: CHRONOLOGICAL (story order)");
            for (int i = 0; i < chronological.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {chronological[i].Text}");
            }
            sb.AppendLine();
        }

        if (misc.Count > 0)
        {
            sb.AppendLine("## 📌 ACTIVE: MISC (importance order)");
            for (int i = 0; i < misc.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {misc[i].Text}");
            }
            sb.AppendLine();
        }

        if (possible.Count > 0)
        {
            sb.AppendLine("## 💡 POSSIBLE POINTS (consider including)");
            for (int i = 0; i < possible.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {possible[i].Text}");
            }
            sb.AppendLine();
        }

        if (archived.Count > 0)
        {
            sb.AppendLine("## 📦 ARCHIVED POINTS (no longer relevant)");
            for (int i = 0; i < archived.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {archived[i].Text}");
            }
            sb.AppendLine();
        }

        string export = sb.ToString();

        if (string.IsNullOrWhiteSpace(export.Trim()))
        {
            await DisplayAlert("No Points", "No story points to export.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(export);
        int totalCount = chronological.Count + misc.Count + possible.Count + archived.Count;
        await DisplayAlert("Copied", $"Copied {totalCount} points to clipboard.", "OK");
    }

    private async Task ExportActiveForReorderingAsync()
    {
        var chronological = _points
            .Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory)))
            .OrderBy(p => p.DisplayOrder).ToList();
        var misc = _points
            .Where(p => p.Category == "active" && p.Subcategory == "misc")
            .OrderBy(p => p.DisplayOrder).ToList();

        if (chronological.Count == 0 && misc.Count == 0)
        {
            await DisplayAlert("No Active Points", "No active points to export for reordering.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Reorder and categorize these story points.");
        sb.AppendLine();
        sb.AppendLine("DEFINITIONS:");
        sb.AppendLine("- CHRONOLOGICAL = Points describing SPECIFIC STORY BEATS/SCENES/MOMENTS.");
        sb.AppendLine("  These happen at a particular point in the narrative timeline.");
        sb.AppendLine("  Examples: 'Hook scene with devil at bar', 'Couch scene where she cries',");
        sb.AppendLine("  'Captain replay to childhood', 'Final image with two boys'.");
        sb.AppendLine("  Order: beginning of story → end of story.");
        sb.AppendLine();
        sb.AppendLine("- MISC = META-POINTS that don't belong to a specific moment.");
        sb.AppendLine("  Examples: visual motifs used throughout, thematic explanations,");
        sb.AppendLine("  production notes, narration lines without scene context.");
        sb.AppendLine("  Order: most important → least important.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: Most story points ARE chronological. Only move to Misc if");
        sb.AppendLine("the point truly doesn't describe a specific scene or moment.");
        sb.AppendLine("When in doubt, keep in Chronological.");
        sb.AppendLine();
        
        int globalIndex = 1;
        
        if (chronological.Count > 0)
        {
            sb.AppendLine("📖 CHRONOLOGICAL (story order):");
            foreach (var p in chronological)
            {
                string lockMark = p.IsSubcategoryLocked ? " 🔒" : "";
                sb.AppendLine($"  {globalIndex}. {p.Text}{lockMark}");
                globalIndex++;
            }
            sb.AppendLine();
        }
        
        if (misc.Count > 0)
        {
            sb.AppendLine("📌 MISC (importance order):");
            foreach (var p in misc)
            {
                string lockMark = p.IsSubcategoryLocked ? " 🔒" : "";
                sb.AppendLine($"  {globalIndex}. {p.Text}{lockMark}");
                globalIndex++;
            }
            sb.AppendLine();
        }

        // Count locked points
        int lockedCount = chronological.Count(p => p.IsSubcategoryLocked) + misc.Count(p => p.IsSubcategoryLocked);
        if (lockedCount > 0)
        {
            sb.AppendLine($"NOTE: {lockedCount} point(s) marked with 🔒 are LOCKED in their subcategory.");
            sb.AppendLine("You can still reorder them within their category, but moves between");
            sb.AppendLine("Chronological and Misc will be ignored for locked points.");
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Output ONLY a C# code block:");
        sb.AppendLine("```csharp");
        sb.AppendLine("// Chronological points (story order, beginning to end)");
        sb.AppendLine("int[] chronological = { 1, 4, 7 };");
        sb.AppendLine();
        sb.AppendLine("// Misc points (importance order, most important first)");
        sb.AppendLine("int[] misc = { 2, 5, 3, 6 };");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine($"- Use ALL numbers from 1 to {chronological.Count + misc.Count}");
        sb.AppendLine("- Each number appears in exactly ONE array");
        sb.AppendLine("- KEEP MOST POINTS IN CHRONOLOGICAL - only move to Misc if truly not a scene/moment");
        sb.AppendLine("- Points marked 🔒 will stay in their current subcategory regardless of your output");
        sb.AppendLine("- No commentary outside the code block");

        await Clipboard.SetTextAsync(sb.ToString());
        
        string lockedNote = lockedCount > 0 ? $" ({lockedCount} locked)" : "";
        await DisplayAlert("Copied", 
            $"Copied {chronological.Count} chronological + {misc.Count} misc points{lockedNote}.\n\nPaste to LLM, copy its response, then use 'Import Reordered'.", "OK");
    }

    private async Task ExportPointsAsDraftPromptAsync()
    {
        var active = _points.Where(p => p.Category == "active").OrderBy(p => p.DisplayOrder).ToList();
        var possible = _points.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();
        var irrelevant = _points.Where(p => p.Category == "irrelevant").OrderBy(p => p.DisplayOrder).ToList();

        if (active.Count == 0 && possible.Count == 0)
        {
            await DisplayAlert("No Points", "Add some active or possible points first.", "OK");
            return;
        }

        string prompt = StoryPromptTemplates.BuildDraftFromPointsPrompt(
            _project.Name,
            active.Select(p => p.Text),
            possible.Select(p => p.Text),
            irrelevant.Select(p => p.Text));

        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert("Copied", 
            $"Copied draft prompt with {active.Count} active, {possible.Count} possible, {irrelevant.Count} irrelevant points.\n\nPaste to LLM, then import the response in Story Production.", "OK");
    }

    private async Task ImportReorderedActivePointsAsync()
    {
        var chronological = _points
            .Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory)))
            .OrderBy(p => p.DisplayOrder).ToList();
        var misc = _points
            .Where(p => p.Category == "active" && p.Subcategory == "misc")
            .OrderBy(p => p.DisplayOrder).ToList();
        
        // Build combined list in same order as export
        var allActive = new List<StoryPoint>();
        allActive.AddRange(chronological);
        allActive.AddRange(misc);

        if (allActive.Count == 0)
        {
            await DisplayAlert("No Active Points", "No active points to reorder.", "OK");
            return;
        }

        string? input = await ShowMultiLineInputAsync(
            "Import Reordered",
            "Paste the LLM response.\nExpecting:\nint[] chronological = { 1, 4, 7 };\nint[] misc = { 2, 5, 3 };",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse both arrays
        var chronoNumbers = ParseIntArrayFromInput(input, "chronological");
        var miscNumbers = ParseIntArrayFromInput(input, "misc");
        
        // Validate: all numbers should be accounted for
        var allNumbers = chronoNumbers.Concat(miscNumbers).ToList();
        var expectedNumbers = Enumerable.Range(1, allActive.Count).ToList();
        
        var missing = expectedNumbers.Except(allNumbers).ToList();
        var duplicates = allNumbers.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var outOfRange = allNumbers.Where(n => n < 1 || n > allActive.Count).ToList();
        
        if (missing.Any() || duplicates.Any() || outOfRange.Any())
        {
            var errors = new List<string>();
            if (missing.Any()) errors.Add($"Missing: {string.Join(", ", missing)}");
            if (duplicates.Any()) errors.Add($"Duplicates: {string.Join(", ", duplicates)}");
            if (outOfRange.Any()) errors.Add($"Out of range: {string.Join(", ", outOfRange)}");
            
            await DisplayAlert("Invalid Input", 
                $"Expected numbers 1-{allActive.Count} used exactly once.\n{string.Join("\n", errors)}", "OK");
            return;
        }

        _loadingOverlay.IsVisible = true;
        
        try
        {
            int lockedKept = 0;
            int movedToChronological = 0;
            int movedToMisc = 0;

            // Apply chronological ordering and subcategory
            for (int newOrder = 0; newOrder < chronoNumbers.Count; newOrder++)
            {
                int originalIndex = chronoNumbers[newOrder] - 1;
                var point = allActive[originalIndex];
                
                // Respect lock - only update order, not subcategory
                if (point.IsSubcategoryLocked && point.Subcategory != "chronological")
                {
                    // Point is locked in a different subcategory - skip subcategory change
                    lockedKept++;
                    continue;
                }
                
                if (point.Subcategory != "chronological") movedToChronological++;
                point.Subcategory = "chronological";
                point.DisplayOrder = newOrder;
                await _storyService.UpdateStoryPointAsync(point);
            }

            // Apply misc ordering and subcategory
            for (int newOrder = 0; newOrder < miscNumbers.Count; newOrder++)
            {
                int originalIndex = miscNumbers[newOrder] - 1;
                var point = allActive[originalIndex];
                
                // Respect lock - only update order, not subcategory
                if (point.IsSubcategoryLocked && point.Subcategory != "misc")
                {
                    // Point is locked in a different subcategory - skip subcategory change
                    lockedKept++;
                    continue;
                }
                
                if (point.Subcategory != "misc") movedToMisc++;
                point.Subcategory = "misc";
                point.DisplayOrder = newOrder;
                await _storyService.UpdateStoryPointAsync(point);
            }

            // Re-order locked points that were kept in their original subcategory
            // They need proper display order within their subcategory
            if (lockedKept > 0)
            {
                // Reload to get fresh data
                var freshPoints = await _storyService.GetStoryPointsAsync(_project.Id);
                var lockedChronoPoints = freshPoints
                    .Where(p => p.Category == "active" && p.Subcategory == "chronological" && p.IsSubcategoryLocked)
                    .OrderBy(p => p.DisplayOrder).ToList();
                var lockedMiscPoints = freshPoints
                    .Where(p => p.Category == "active" && p.Subcategory == "misc" && p.IsSubcategoryLocked)
                    .OrderBy(p => p.DisplayOrder).ToList();

                // Append locked points to end of their respective subcategories
                int maxChronoOrder = freshPoints
                    .Where(p => p.Category == "active" && p.Subcategory == "chronological" && !p.IsSubcategoryLocked)
                    .Select(p => p.DisplayOrder)
                    .DefaultIfEmpty(-1).Max() + 1;
                foreach (var lp in lockedChronoPoints)
                {
                    lp.DisplayOrder = maxChronoOrder++;
                    await _storyService.UpdateStoryPointAsync(lp);
                }

                int maxMiscOrder = freshPoints
                    .Where(p => p.Category == "active" && p.Subcategory == "misc" && !p.IsSubcategoryLocked)
                    .Select(p => p.DisplayOrder)
                    .DefaultIfEmpty(-1).Max() + 1;
                foreach (var lp in lockedMiscPoints)
                {
                    lp.DisplayOrder = maxMiscOrder++;
                    await _storyService.UpdateStoryPointAsync(lp);
                }
            }

            await LoadPointsAsync();
            _loadingOverlay.IsVisible = false;
            
            var summary = $"Reordered {chronoNumbers.Count} chronological + {miscNumbers.Count} misc points.";
            if (lockedKept > 0)
                summary += $"\n🔒 {lockedKept} locked point(s) kept in original subcategory.";
            
            await DisplayAlert("Reordered", summary, "OK");
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task ExportMissingPointsCheckAsync()
    {
        var active = _points.Where(p => p.Category == "active").OrderBy(p => p.DisplayOrder).ToList();

        if (active.Count == 0)
        {
            await DisplayAlert("No Active Points", "No active points to check.", "OK");
            return;
        }

        // Get all drafts for this project
        int rootProjectId = _project.ParentProjectId ?? _project.Id;
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var drafts = allProjects
            .Where(p => p.Id == rootProjectId || p.ParentProjectId == rootProjectId)
            .OrderByDescending(p => p.IsLatest)
            .ThenByDescending(p => p.DraftVersion)
            .ToList();

        if (drafts.Count == 0)
        {
            await DisplayAlert("No Drafts", "No drafts found for this project.", "OK");
            return;
        }

        // Let user pick a draft
        var draftOptions = drafts.Select(d => 
            $"{d.Name}{(d.IsLatest ? " ★" : "")}{(d.DraftVersion > 1 ? $" (v{d.DraftVersion})" : "")}").ToArray();
        
        var selected = await DisplayActionSheet("Select Draft to Check", "Cancel", null, draftOptions);
        if (selected == null || selected == "Cancel") return;

        int selectedIndex = Array.IndexOf(draftOptions, selected);
        var selectedDraft = drafts[selectedIndex];

        // Get all lines from the draft
        var lines = await _storyService.GetLinesAsync(selectedDraft.Id);
        
        // Build the prompt
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Review the following draft and categorize each story point. For PARTIAL points, explain what IS and ISN'T covered.");
        sb.AppendLine();
        sb.AppendLine($"DRAFT: {selectedDraft.Name}");
        sb.AppendLine($"LINES: {lines.Count}");
        sb.AppendLine();
        sb.AppendLine("DRAFT CONTENT:");
        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            string lineType = line.IsSilent ? "VISUAL" : "NARR";
            string text = line.IsSilent ? "(silent)" : line.LineText;
            sb.AppendLine($"[{line.LineOrder}] {lineType}: {text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("ACTIVE STORY POINTS TO CHECK:");
        for (int i = 0; i < active.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {active[i].Text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("DEFINITIONS:");
        sb.AppendLine("- COVERED: The draft fully addresses the core idea of this point (don't list these)");
        sb.AppendLine("- PARTIAL: The draft touches on this point but misses important aspects");
        sb.AppendLine("- MISSING: The draft does not address this point at all");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with C# code in this exact format:");
        sb.AppendLine("```csharp");
        sb.AppendLine("// MISSING POINTS (not in draft at all)");
        sb.AppendLine("int[] missing = { 3, 7, 12 };");
        sb.AppendLine();
        sb.AppendLine("// PARTIAL POINTS (with breakdown of what's covered vs not)");
        sb.AppendLine("// Point 2: [brief original description]");
        sb.AppendLine("partial[2].covered = \"What aspects ARE addressed in the draft\";");
        sb.AppendLine("partial[2].uncovered = \"What aspects are NOT addressed in the draft\";");
        sb.AppendLine();
        sb.AppendLine("// Point 5: [brief original description]");
        sb.AppendLine("partial[5].covered = \"...\";");
        sb.AppendLine("partial[5].uncovered = \"...\";");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- List ALL missing point numbers in the int[] missing array");
        sb.AppendLine("- For each PARTIAL point, provide both .covered and .uncovered strings");
        sb.AppendLine("- Write clear, specific descriptions of what's covered vs not");
        sb.AppendLine("- Don't list fully COVERED points - only missing and partial");
        sb.AppendLine("- The .covered and .uncovered should be standalone point descriptions");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", 
            $"Copied check prompt with {active.Count} points and {lines.Count} draft lines.\n\nPaste to LLM, copy its response, then use 'Import Missing/Partial Result'.", "OK");
    }

    private async Task ImportMissingPointsResultAsync()
    {
        var active = _points.Where(p => p.Category == "active").OrderBy(p => p.DisplayOrder).ToList();

        if (active.Count == 0)
        {
            await DisplayAlert("No Active Points", "No active points to process.", "OK");
            return;
        }

        string? input = await ShowMultiLineInputAsync(
            "Import Missing/Partial Result",
            "Paste the LLM response with missing array and partial[N].covered/.uncovered:",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse missing array
        var missingNumbers = ParseIntArrayFromInput(input, "missing");

        // Parse partial breakdowns: partial[N].covered = "..."; partial[N].uncovered = "...";
        var partialBreakdowns = new Dictionary<int, (string Covered, string Uncovered)>();
        for (int i = 1; i <= active.Count; i++)
        {
            string covered = ParsePartialField(input, i, "covered");
            string uncovered = ParsePartialField(input, i, "uncovered");
            
            if (!string.IsNullOrEmpty(covered) || !string.IsNullOrEmpty(uncovered))
            {
                partialBreakdowns[i] = (covered, uncovered);
            }
        }

        // Validate numbers
        missingNumbers = missingNumbers.Where(n => n >= 1 && n <= active.Count).Distinct().ToList();
        
        // Remove any that are in both (shouldn't happen but just in case)
        foreach (var key in partialBreakdowns.Keys.ToList())
        {
            if (missingNumbers.Contains(key))
                partialBreakdowns.Remove(key);
        }

        if (missingNumbers.Count == 0 && partialBreakdowns.Count == 0)
        {
            await DisplayAlert("All Covered! ✅", "All active points are fully covered in the draft. Nothing to process.", "OK");
            return;
        }

        // Show detailed UI for review
        await ShowMissingPartialReviewAsync(active, missingNumbers, partialBreakdowns);
    }

    private string ParsePartialField(string input, int index, string field)
    {
        // Match: partial[index].field = "value";
        var pattern = $@"partial\[{index}\]\.{field}\s*=\s*""((?:[^""\\]|\\.)*)""";
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (!match.Success) return "";
        
        string value = match.Groups[1].Value;
        value = value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
        return value.Trim();
    }

    private async Task ShowMissingPartialReviewAsync(
        List<StoryPoint> activePoints, 
        List<int> missingNumbers, 
        Dictionary<int, (string Covered, string Uncovered)> partialBreakdowns)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Track selections for missing points
        var missingActions = new Dictionary<int, string>(); // "pending" or "possible" or "archive" or "keep"
        foreach (var n in missingNumbers)
            missingActions[n] = "pending"; // Default: move to pending

        // Track selections for partial points
        var partialActions = new Dictionary<int, string>(); // "split", "keep", "archive"
        foreach (var n in partialBreakdowns.Keys)
            partialActions[n] = "split"; // Default: split into covered/uncovered

        // Create overlay
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            ZIndex = 1000
        };

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D30"),
            BorderColor = Color.FromArgb("#3E3E42"),
            CornerRadius = 12,
            Padding = new Thickness(20),
            MaximumWidthRequest = 900,
            MaximumHeightRequest = 700,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = false
        };

        var mainLayout = new VerticalStackLayout { Spacing = 16 };

        // Header
        mainLayout.Children.Add(new Label
        {
            Text = "🔍 Review Missing & Partial Points",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        // Stats
        int coveredCount = activePoints.Count - missingNumbers.Count - partialBreakdowns.Count;
        mainLayout.Children.Add(new Label
        {
            Text = $"📊 {coveredCount} fully covered • {partialBreakdowns.Count} partial • {missingNumbers.Count} missing",
            FontSize = 13,
            TextColor = Color.FromArgb("#AAAAAA")
        });

        // Scrollable content
        var scrollView = new ScrollView { MaximumHeightRequest = 480 };
        var itemsList = new VerticalStackLayout { Spacing = 16 };

        // === PARTIAL POINTS SECTION (with breakdown details) ===
        if (partialBreakdowns.Count > 0)
        {
            itemsList.Children.Add(new Label
            {
                Text = "🔶 PARTIAL POINTS",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E91E63"),
                Margin = new Thickness(0, 8, 0, 4)
            });

            foreach (var kvp in partialBreakdowns.OrderBy(k => k.Key))
            {
                int n = kvp.Key;
                int index = n; // Capture
                var point = activePoints[n - 1];
                var breakdown = kvp.Value;

                var itemCard = new Frame
                {
                    BackgroundColor = Color.FromArgb("#1E1E1E"),
                    BorderColor = Color.FromArgb("#E91E63"),
                    CornerRadius = 8,
                    Padding = new Thickness(12),
                    HasShadow = false
                };

                var itemLayout = new VerticalStackLayout { Spacing = 8 };

                // Original point
                itemLayout.Children.Add(new Label
                {
                    Text = $"#{n} ORIGINAL:",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#E91E63")
                });
                itemLayout.Children.Add(new Label
                {
                    Text = point.Text,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#888888"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    TextDecorations = TextDecorations.Strikethrough
                });

                // Covered part (green)
                if (!string.IsNullOrEmpty(breakdown.Covered))
                {
                    itemLayout.Children.Add(new Label
                    {
                        Text = "✅ IN DRAFT:",
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#27AE60"),
                        Margin = new Thickness(0, 6, 0, 0)
                    });
                    itemLayout.Children.Add(new Label
                    {
                        Text = breakdown.Covered,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#27AE60"),
                        LineBreakMode = LineBreakMode.WordWrap
                    });
                }

                // Uncovered part (orange)
                if (!string.IsNullOrEmpty(breakdown.Uncovered))
                {
                    itemLayout.Children.Add(new Label
                    {
                        Text = "❌ NOT IN DRAFT:",
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#FF9800"),
                        Margin = new Thickness(0, 6, 0, 0)
                    });
                    itemLayout.Children.Add(new Label
                    {
                        Text = breakdown.Uncovered,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#FF9800"),
                        LineBreakMode = LineBreakMode.WordWrap
                    });
                }

                // Action picker
                var actionRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
                
                actionRow.Children.Add(new Label
                {
                    Text = "Action:",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#AAAAAA"),
                    VerticalOptions = LayoutOptions.Center
                });

                var actionPicker = new Picker
                {
                    ItemsSource = new[] 
                    { 
                        "✂️ Split: ✅→Active, ❌→Pending", 
                        "✅ Keep original in Active",
                        "📦 Archive original"
                    },
                    SelectedIndex = 0,
                    BackgroundColor = Color.FromArgb("#3E3E42"),
                    TextColor = Colors.White,
                    WidthRequest = 300
                };
                actionPicker.SelectedIndexChanged += (s, e) =>
                {
                    partialActions[index] = actionPicker.SelectedIndex switch
                    {
                        0 => "split",
                        1 => "keep",
                        2 => "archive",
                        _ => "split"
                    };
                };
                actionRow.Children.Add(actionPicker);

                itemLayout.Children.Add(actionRow);
                itemCard.Content = itemLayout;
                itemsList.Children.Add(itemCard);
            }
        }

        // === MISSING POINTS SECTION ===
        if (missingNumbers.Count > 0)
        {
            itemsList.Children.Add(new Label
            {
                Text = "❌ MISSING POINTS (not in draft at all)",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E74C3C"),
                Margin = new Thickness(0, 16, 0, 4)
            });

            foreach (var n in missingNumbers)
            {
                int index = n; // Capture
                var point = activePoints[n - 1];

                var itemCard = new Frame
                {
                    BackgroundColor = Color.FromArgb("#1E1E1E"),
                    BorderColor = Color.FromArgb("#E74C3C"),
                    CornerRadius = 8,
                    Padding = new Thickness(12),
                    HasShadow = false
                };

                var itemLayout = new VerticalStackLayout { Spacing = 8 };

                // Point number and text
                itemLayout.Children.Add(new Label
                {
                    Text = $"#{n}",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#E74C3C")
                });

                itemLayout.Children.Add(new Label
                {
                    Text = point.Text,
                    FontSize = 12,
                    TextColor = Colors.White,
                    LineBreakMode = LineBreakMode.WordWrap
                });

                // Action picker
                var actionRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
                
                actionRow.Children.Add(new Label
                {
                    Text = "Action:",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#AAAAAA"),
                    VerticalOptions = LayoutOptions.Center
                });

                var actionPicker = new Picker
                {
                    ItemsSource = new[] 
                    { 
                        "⏳ Move to Pending", 
                        "💡 Move to Possible",
                        "✅ Keep in Active",
                        "📦 Archive"
                    },
                    SelectedIndex = 0,
                    BackgroundColor = Color.FromArgb("#3E3E42"),
                    TextColor = Colors.White,
                    WidthRequest = 220
                };
                actionPicker.SelectedIndexChanged += (s, e) =>
                {
                    missingActions[index] = actionPicker.SelectedIndex switch
                    {
                        0 => "pending",
                        1 => "possible",
                        2 => "keep",
                        3 => "archive",
                        _ => "pending"
                    };
                };
                actionRow.Children.Add(actionPicker);

                itemLayout.Children.Add(actionRow);
                itemCard.Content = itemLayout;
                itemsList.Children.Add(itemCard);
            }
        }

        scrollView.Content = itemsList;
        mainLayout.Children.Add(scrollView);

        // Button row
        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#4A4A4A"),
            TextColor = Colors.White,
            WidthRequest = 100,
            HeightRequest = 40
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(false);
        };
        buttonRow.Children.Add(cancelBtn);

        var applyBtn = new Button
        {
            Text = "✅ Apply Changes",
            BackgroundColor = Color.FromArgb("#27AE60"),
            TextColor = Colors.White,
            WidthRequest = 140,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        };
        applyBtn.Clicked += async (s, e) =>
        {
            CloseOverlay(overlay);
            _loadingOverlay.IsVisible = true;

            try
            {
                int archivedCount = 0;
                int pendingCount = 0;
                int possibleCount = 0;
                int activeCreated = 0;
                int keptCount = 0;

                // Process partial points
                foreach (var kvp in partialBreakdowns)
                {
                    int n = kvp.Key;
                    var point = activePoints[n - 1];
                    var breakdown = kvp.Value;
                    var action = partialActions[n];
                    
                    if (action == "split")
                    {
                        // Archive original
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, "archived");
                        archivedCount++;
                        
                        // Create new Active point for covered part
                        if (!string.IsNullOrWhiteSpace(breakdown.Covered))
                        {
                            await _storyService.AddStoryPointAsync(_project.Id, breakdown.Covered, "active");
                            activeCreated++;
                        }
                        
                        // Create new Pending point for uncovered part
                        if (!string.IsNullOrWhiteSpace(breakdown.Uncovered))
                        {
                            await _storyService.AddStoryPointAsync(_project.Id, breakdown.Uncovered, "pending");
                            pendingCount++;
                        }
                    }
                    else if (action == "archive")
                    {
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, "archived");
                        archivedCount++;
                    }
                    else // keep
                    {
                        keptCount++;
                    }
                }

                // Process missing points
                foreach (var n in missingNumbers)
                {
                    var point = activePoints[n - 1];
                    var action = missingActions[n];
                    
                    if (action == "pending")
                    {
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, "pending");
                        pendingCount++;
                    }
                    else if (action == "possible")
                    {
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, "possible");
                        possibleCount++;
                    }
                    else if (action == "archive")
                    {
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, "archived");
                        archivedCount++;
                    }
                    else // keep
                    {
                        keptCount++;
                    }
                }

                await LoadPointsAsync();
                _loadingOverlay.IsVisible = false;

                var summary = new System.Text.StringBuilder();
                if (activeCreated > 0) summary.AppendLine($"✅ Created Active: {activeCreated}");
                if (pendingCount > 0) summary.AppendLine($"⏳ Moved to Pending: {pendingCount}");
                if (possibleCount > 0) summary.AppendLine($"💡 Moved to Possible: {possibleCount}");
                if (archivedCount > 0) summary.AppendLine($"📦 Archived: {archivedCount}");
                if (keptCount > 0) summary.AppendLine($"📌 Kept unchanged: {keptCount}");

                await DisplayAlert("Done", summary.ToString(), "OK");
            }
            catch (Exception ex)
            {
                _loadingOverlay.IsVisible = false;
                await DisplayAlert("Error", ex.Message, "OK");
            }

            tcs.TrySetResult(true);
        };
        buttonRow.Children.Add(applyBtn);

        mainLayout.Children.Add(buttonRow);
        card.Content = mainLayout;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            pageGrid.Children.Add(overlay);
        }

        await tcs.Task;
    }

    private List<int> ParseIntArrayFromInput(string input, string arrayName)
    {
        var numbers = new List<int>();
        
        // Find the line with "int[] arrayName = { ... };"
        var pattern = $@"int\[\]\s*{arrayName}\s*=\s*\{{\s*([^}}]*)\s*\}}";
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (!match.Success) return numbers;
        
        string numSection = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(numSection)) return numbers;
        
        var parts = numSection.Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            string numStr = new string(part.Trim().Where(c => char.IsDigit(c)).ToArray());
            if (int.TryParse(numStr, out int num))
            {
                numbers.Add(num);
            }
        }
        
        return numbers;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    private async Task ExportPartialBreakdownAsync()
    {
        var partialPoints = _points.Where(p => p.Category == "partial").OrderBy(p => p.DisplayOrder).ToList();

        if (partialPoints.Count == 0)
        {
            await DisplayAlert("No Partial Points", "No partial points to break down.\n\nFirst use 'Check Missing/Partial Points' to identify partially covered points.", "OK");
            return;
        }

        // Get all drafts for this project
        int rootProjectId = _project.ParentProjectId ?? _project.Id;
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var drafts = allProjects
            .Where(p => p.Id == rootProjectId || p.ParentProjectId == rootProjectId)
            .OrderByDescending(p => p.IsLatest)
            .ThenByDescending(p => p.DraftVersion)
            .ToList();

        if (drafts.Count == 0)
        {
            await DisplayAlert("No Drafts", "No drafts found for this project.", "OK");
            return;
        }

        // Let user pick a draft
        var draftOptions = drafts.Select(d => 
            $"{d.Name}{(d.IsLatest ? " ★" : "")}{(d.DraftVersion > 1 ? $" (v{d.DraftVersion})" : "")}").ToArray();
        
        var selected = await DisplayActionSheet("Select Draft for Context", "Cancel", null, draftOptions);
        if (selected == null || selected == "Cancel") return;

        int selectedIndex = Array.IndexOf(draftOptions, selected);
        var selectedDraft = drafts[selectedIndex];

        // Get all lines from the draft
        var lines = await _storyService.GetLinesAsync(selectedDraft.Id);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Break down each PARTIAL story point into what IS covered and what IS NOT covered in the draft.");
        sb.AppendLine();
        sb.AppendLine($"DRAFT: {selectedDraft.Name}");
        sb.AppendLine();
        sb.AppendLine("DRAFT CONTENT:");
        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            string lineType = line.IsSilent ? "VISUAL" : "NARR";
            string text = line.IsSilent ? "(silent)" : line.LineText;
            sb.AppendLine($"[{line.LineOrder}] {lineType}: {text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("PARTIAL POINTS TO BREAK DOWN:");
        for (int i = 0; i < partialPoints.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {partialPoints[i].Text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("For each partial point, identify:");
        sb.AppendLine("- COVERED: The specific aspects that ARE addressed in the draft");
        sb.AppendLine("- UNCOVERED: The specific aspects that are NOT addressed in the draft");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with C# code in this exact format:");
        sb.AppendLine("```csharp");
        sb.AppendLine("// PARTIAL POINT BREAKDOWN");
        sb.AppendLine("");
        sb.AppendLine("// Point 1: [original point text]");
        sb.AppendLine("covered[1] = \"What aspects of point 1 ARE in the draft\";");
        sb.AppendLine("uncovered[1] = \"What aspects of point 1 are NOT in the draft\";");
        sb.AppendLine("");
        sb.AppendLine("// Point 2: [original point text]");
        sb.AppendLine("covered[2] = \"What aspects of point 2 ARE in the draft\";");
        sb.AppendLine("uncovered[2] = \"What aspects of point 2 are NOT in the draft\";");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Include ALL partial points");
        sb.AppendLine("- Write clear, standalone point descriptions (not 'the part about X')");
        sb.AppendLine("- Each covered/uncovered should be a complete story point on its own");
        sb.AppendLine("- If a point is actually fully covered, set uncovered to empty string");
        sb.AppendLine("- If a point is actually not covered at all, set covered to empty string");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", 
            $"Copied breakdown prompt for {partialPoints.Count} partial points.\n\nPaste to LLM, copy its response, then use 'Import Partial Breakdown Result'.", "OK");
    }

    private async Task ImportPartialBreakdownResultAsync()
    {
        var partialPoints = _points.Where(p => p.Category == "partial").OrderBy(p => p.DisplayOrder).ToList();

        if (partialPoints.Count == 0)
        {
            await DisplayAlert("No Partial Points", "No partial points to process.", "OK");
            return;
        }

        string? input = await ShowMultiLineInputAsync(
            "Import Partial Breakdown",
            "Paste the LLM response with covered[N] and uncovered[N] assignments:",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse covered and uncovered for each point
        var breakdowns = new Dictionary<int, (string Covered, string Uncovered)>();
        
        for (int i = 1; i <= partialPoints.Count; i++)
        {
            string covered = ParseStringAssignment(input, "covered", i);
            string uncovered = ParseStringAssignment(input, "uncovered", i);
            
            if (!string.IsNullOrEmpty(covered) || !string.IsNullOrEmpty(uncovered))
            {
                breakdowns[i] = (covered, uncovered);
            }
        }

        if (breakdowns.Count == 0)
        {
            await DisplayAlert("Parse Error", "Could not parse any covered[N] or uncovered[N] assignments.", "OK");
            return;
        }

        // Show confirmation with details
        var confirmMsg = new System.Text.StringBuilder();
        confirmMsg.AppendLine($"Found breakdowns for {breakdowns.Count} points:\n");

        foreach (var kvp in breakdowns.OrderBy(k => k.Key))
        {
            int idx = kvp.Key;
            var original = partialPoints[idx - 1];
            confirmMsg.AppendLine($"━━━ Point {idx} ━━━");
            confirmMsg.AppendLine($"📝 ORIGINAL: {Truncate(original.Text, 60)}");
            if (!string.IsNullOrEmpty(kvp.Value.Covered))
                confirmMsg.AppendLine($"✅ COVERED → Active: {Truncate(kvp.Value.Covered, 50)}");
            if (!string.IsNullOrEmpty(kvp.Value.Uncovered))
                confirmMsg.AppendLine($"⏳ UNCOVERED → Pending: {Truncate(kvp.Value.Uncovered, 50)}");
            confirmMsg.AppendLine();
        }

        confirmMsg.AppendLine("This will:");
        confirmMsg.AppendLine("• Create new Active points for covered aspects");
        confirmMsg.AppendLine("• Create new Pending points for uncovered aspects");

        bool confirm = await DisplayAlert("Confirm Breakdown", confirmMsg.ToString(), "Create Points", "Cancel");
        if (!confirm) return;

        // Create new points
        int activeCreated = 0;
        int pendingCreated = 0;
        var processedOriginals = new List<StoryPoint>();

        foreach (var kvp in breakdowns.OrderBy(k => k.Key))
        {
            int idx = kvp.Key;
            var original = partialPoints[idx - 1];
            processedOriginals.Add(original);

            if (!string.IsNullOrEmpty(kvp.Value.Covered))
            {
                await _storyService.AddStoryPointAsync(_project.Id, kvp.Value.Covered, "active");
                activeCreated++;
            }
            if (!string.IsNullOrEmpty(kvp.Value.Uncovered))
            {
                await _storyService.AddStoryPointAsync(_project.Id, kvp.Value.Uncovered, "pending");
                pendingCreated++;
            }
        }

        await LoadPointsAsync();

        // Ask whether to archive originals
        if (processedOriginals.Count > 0)
        {
            var archiveMsg = $"Created {activeCreated} Active and {pendingCreated} Pending points.\n\n";
            archiveMsg += $"Archive the {processedOriginals.Count} original Partial point(s) that were broken down?\n\n";
            foreach (var pt in processedOriginals)
            {
                archiveMsg += $"• {Truncate(pt.Text, 50)}\n";
            }

            bool archiveOriginals = await DisplayAlert("Archive Originals?", archiveMsg, "Yes, Archive", "No, Keep in Partial");
            
            if (archiveOriginals)
            {
                foreach (var pt in processedOriginals)
                {
                    await _storyService.MoveStoryPointToCategoryAsync(pt.Id, "archived");
                }
                await LoadPointsAsync();
                await DisplayAlert("Done", 
                    $"Created {activeCreated} Active, {pendingCreated} Pending points.\nArchived {processedOriginals.Count} original Partial points.", "OK");
            }
            else
            {
                await DisplayAlert("Done", 
                    $"Created {activeCreated} Active, {pendingCreated} Pending points.\nOriginal Partial points kept.", "OK");
            }
        }
    }

    private string ParseStringAssignment(string input, string varName, int index)
    {
        // Match: varName[index] = "value";
        var pattern = $@"{varName}\[{index}\]\s*=\s*""((?:[^""\\]|\\.)*)""";
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        
        if (!match.Success) return "";
        
        // Unescape the string
        string value = match.Groups[1].Value;
        value = value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
        
        return value.Trim();
    }

    private async Task ExportPointsForCleanupAsync()
    {
        var allPoints = _points.OrderBy(p => p.Category).ThenBy(p => p.DisplayOrder).ToList();

        if (allPoints.Count == 0)
        {
            await DisplayAlert("No Points", "No story points to clean up.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Review and reorganize these story points. Some may be messy, overlapping, or combining multiple ideas.");
        sb.AppendLine();
        sb.AppendLine($"PROJECT: {_project.Name}");
        sb.AppendLine($"TOTAL POINTS: {allPoints.Count}");
        sb.AppendLine();
        sb.AppendLine("CURRENT POINTS:");
        
        int globalIndex = 1;
        var activePoints = allPoints.Where(p => p.Category == "active").ToList();
        var partialPoints = allPoints.Where(p => p.Category == "partial").ToList();
        var possiblePoints = allPoints.Where(p => p.Category == "possible").ToList();
        var archivedPoints = allPoints.Where(p => p.Category == "archived" || p.Category == "irrelevant").ToList();

        if (activePoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// ACTIVE:");
            foreach (var p in activePoints)
            {
                sb.AppendLine($"// [{globalIndex}] {p.Text}");
                globalIndex++;
            }
        }
        if (partialPoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// PARTIAL:");
            foreach (var p in partialPoints)
            {
                sb.AppendLine($"// [{globalIndex}] {p.Text}");
                globalIndex++;
            }
        }
        if (possiblePoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// POSSIBLE:");
            foreach (var p in possiblePoints)
            {
                sb.AppendLine($"// [{globalIndex}] {p.Text}");
                globalIndex++;
            }
        }
        if (archivedPoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// ARCHIVED:");
            foreach (var p in archivedPoints)
            {
                sb.AppendLine($"// [{globalIndex}] {p.Text}");
                globalIndex++;
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Break down any points that combine multiple distinct ideas into separate points");
        sb.AppendLine("2. Merge any points that are essentially the same idea");
        sb.AppendLine("3. Clarify any vague or unclear points");
        sb.AppendLine("4. Keep points that are already clean and well-defined as-is");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with C# code in this exact format:");
        sb.AppendLine("```csharp");
        sb.AppendLine("// POINT CLEANUP");
        sb.AppendLine("");
        sb.AppendLine("// Original point 1 - keep as-is");
        sb.AppendLine("cleanup[1] = new[] { \"Same text if unchanged\" };");
        sb.AppendLine("");
        sb.AppendLine("// Original point 2 - break into multiple");
        sb.AppendLine("cleanup[2] = new[] { \"First distinct idea\", \"Second distinct idea\", \"Third idea\" };");
        sb.AppendLine("");
        sb.AppendLine("// Original point 3 - merge with point 4 (point 4 becomes empty)");
        sb.AppendLine("cleanup[3] = new[] { \"Combined and clarified version\" };");
        sb.AppendLine("cleanup[4] = new[] { };  // empty = archive, merged into 3");
        sb.AppendLine("");
        sb.AppendLine("// Original point 5 - clarify");
        sb.AppendLine("cleanup[5] = new[] { \"Clearer version of the same idea\" };");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Include ALL original point numbers");
        sb.AppendLine("- Use empty array new[] { } for points to archive (merged elsewhere)");
        sb.AppendLine("- Each new point should be atomic - one clear idea");
        sb.AppendLine("- Preserve the original meaning, just organize better");
        sb.AppendLine("- Add a comment before each showing what you did (keep/break/merge/clarify/archive)");;

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", 
            $"Copied {allPoints.Count} points for cleanup.\n\nPaste to LLM, copy its response, then use 'Import Cleanup Result'.", "OK");
    }

    private async Task ImportCleanupResultAsync()
    {
        var allPoints = new List<StoryPoint>();
        var activePoints = _points.Where(p => p.Category == "active").OrderBy(p => p.DisplayOrder).ToList();
        var partialPoints = _points.Where(p => p.Category == "partial").OrderBy(p => p.DisplayOrder).ToList();
        var possiblePoints = _points.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();
        var archivedPoints = _points.Where(p => p.Category == "archived" || p.Category == "irrelevant").OrderBy(p => p.DisplayOrder).ToList();
        
        // Build in same order as export
        allPoints.AddRange(activePoints);
        allPoints.AddRange(partialPoints);
        allPoints.AddRange(possiblePoints);
        allPoints.AddRange(archivedPoints);

        if (allPoints.Count == 0)
        {
            await DisplayAlert("No Points", "No points to process.", "OK");
            return;
        }

        string? input = await ShowMultiLineInputAsync(
            "Import Cleanup Result",
            "Paste the LLM response with cleanup[N] assignments:",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse cleanup arrays for each point
        var cleanups = new Dictionary<int, List<string>>();
        
        for (int i = 1; i <= allPoints.Count; i++)
        {
            var newPoints = ParseStringArrayAssignment(input, "cleanup", i);
            cleanups[i] = newPoints;
        }

        // Check if any were found
        int foundCount = cleanups.Count(c => c.Value.Count > 0 || IsExplicitEmptyArray(input, "cleanup", c.Key));
        if (foundCount == 0)
        {
            await DisplayAlert("Parse Error", "Could not parse any cleanup[N] assignments.", "OK");
            return;
        }

        // Show detailed preview UI
        await ShowCleanupPreviewAsync(allPoints, cleanups, input);
    }

    private async Task ShowCleanupPreviewAsync(List<StoryPoint> originalPoints, Dictionary<int, List<string>> cleanups, string rawInput)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Track which cleanups to apply
        var applyCleanup = new Dictionary<int, bool>();
        for (int i = 1; i <= originalPoints.Count; i++)
        {
            applyCleanup[i] = true;
        }

        // Create overlay
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            ZIndex = 1000
        };

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D30"),
            BorderColor = Color.FromArgb("#3E3E42"),
            CornerRadius = 12,
            Padding = new Thickness(20),
            MaximumWidthRequest = 800,
            MaximumHeightRequest = 650,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = false
        };

        var mainLayout = new VerticalStackLayout { Spacing = 16 };

        // Header
        mainLayout.Children.Add(new Label
        {
            Text = "🧹 Cleanup Preview",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        // Stats
        int keepCount = 0, breakCount = 0, archiveCount = 0, clarifyCount = 0;
        foreach (var kvp in cleanups)
        {
            bool isExplicitEmpty = IsExplicitEmptyArray(rawInput, "cleanup", kvp.Key);
            if (isExplicitEmpty || kvp.Value.Count == 0)
                archiveCount++;
            else if (kvp.Value.Count > 1)
                breakCount++;
            else if (kvp.Value.Count == 1 && kvp.Key <= originalPoints.Count && kvp.Value[0] != originalPoints[kvp.Key - 1].Text)
                clarifyCount++;
            else
                keepCount++;
        }

        mainLayout.Children.Add(new Label
        {
            Text = $"📊 {keepCount} keep • {breakCount} break down • {clarifyCount} clarify • {archiveCount} archive",
            FontSize = 13,
            TextColor = Color.FromArgb("#AAAAAA")
        });

        // Scrollable list
        var scrollView = new ScrollView { MaximumHeightRequest = 420 };
        var itemsList = new VerticalStackLayout { Spacing = 12 };

        for (int i = 1; i <= originalPoints.Count; i++)
        {
            int index = i; // Capture for closure
            var original = originalPoints[i - 1];
            var newPoints = cleanups.ContainsKey(i) ? cleanups[i] : new List<string>();
            bool isExplicitEmpty = IsExplicitEmptyArray(rawInput, "cleanup", i);

            // Determine action type
            string actionType;
            Color actionColor;
            string actionIcon;

            if (isExplicitEmpty || newPoints.Count == 0)
            {
                actionType = "ARCHIVE";
                actionColor = Color.FromArgb("#9E9E9E");
                actionIcon = "📦";
            }
            else if (newPoints.Count > 1)
            {
                actionType = $"BREAK INTO {newPoints.Count}";
                actionColor = Color.FromArgb("#9B59B6");
                actionIcon = "✂️";
            }
            else if (newPoints[0] != original.Text)
            {
                actionType = "CLARIFY";
                actionColor = Color.FromArgb("#3498DB");
                actionIcon = "✏️";
            }
            else
            {
                actionType = "KEEP";
                actionColor = Color.FromArgb("#27AE60");
                actionIcon = "✓";
            }

            // Item card
            var itemCard = new Frame
            {
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                BorderColor = actionColor,
                CornerRadius = 8,
                Padding = new Thickness(12),
                HasShadow = false
            };

            var itemLayout = new VerticalStackLayout { Spacing = 8 };

            // Header row with checkbox and action type
            var headerRow = new HorizontalStackLayout { Spacing = 10 };
            
            var checkBox = new CheckBox
            {
                IsChecked = true,
                Color = actionColor,
                VerticalOptions = LayoutOptions.Center
            };
            checkBox.CheckedChanged += (s, e) => applyCleanup[index] = e.Value;
            headerRow.Children.Add(checkBox);

            headerRow.Children.Add(new Label
            {
                Text = $"{actionIcon} {actionType}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = actionColor,
                VerticalOptions = LayoutOptions.Center
            });

            headerRow.Children.Add(new Label
            {
                Text = $"[{original.Category.ToUpper()}]",
                FontSize = 11,
                TextColor = Color.FromArgb("#888888"),
                VerticalOptions = LayoutOptions.Center
            });

            itemLayout.Children.Add(headerRow);

            // Original point (with strikethrough if being changed)
            var originalLabel = new Label
            {
                Text = $"📝 {original.Text}",
                FontSize = 12,
                TextColor = Color.FromArgb("#999999"),
                LineBreakMode = LineBreakMode.WordWrap
            };
            if (actionType != "KEEP")
                originalLabel.TextDecorations = TextDecorations.Strikethrough;
            itemLayout.Children.Add(originalLabel);

            // New points (if any changes)
            if (actionType != "KEEP" && actionType != "ARCHIVE" && newPoints.Count > 0)
            {
                foreach (var np in newPoints)
                {
                    itemLayout.Children.Add(new Label
                    {
                        Text = $"   → {np}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#27AE60"),
                        LineBreakMode = LineBreakMode.WordWrap
                    });
                }
            }

            itemCard.Content = itemLayout;
            itemsList.Children.Add(itemCard);
        }

        scrollView.Content = itemsList;
        mainLayout.Children.Add(scrollView);

        // Button row
        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#4A4A4A"),
            TextColor = Colors.White,
            WidthRequest = 100,
            HeightRequest = 40
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(false);
        };
        buttonRow.Children.Add(cancelBtn);

        var applyBtn = new Button
        {
            Text = "✅ Apply Cleanup",
            BackgroundColor = Color.FromArgb("#27AE60"),
            TextColor = Colors.White,
            WidthRequest = 140,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        };
        applyBtn.Clicked += async (s, e) =>
        {
            CloseOverlay(overlay);
            await ApplyCleanupAsync(originalPoints, cleanups, applyCleanup, rawInput);
            tcs.TrySetResult(true);
        };
        buttonRow.Children.Add(applyBtn);

        mainLayout.Children.Add(buttonRow);
        card.Content = mainLayout;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            pageGrid.Children.Add(overlay);
        }

        await tcs.Task;
    }

    private async Task ApplyCleanupAsync(
        List<StoryPoint> originalPoints, 
        Dictionary<int, List<string>> cleanups, 
        Dictionary<int, bool> applyCleanup,
        string rawInput)
    {
        _loadingOverlay.IsVisible = true;
        
        try
        {
            int created = 0;
            int archived = 0;
            int kept = 0;

            for (int i = 1; i <= originalPoints.Count; i++)
            {
                if (!applyCleanup[i])
                {
                    kept++;
                    continue;
                }

                var original = originalPoints[i - 1];
                var newPoints = cleanups.ContainsKey(i) ? cleanups[i] : new List<string>();
                bool isExplicitEmpty = IsExplicitEmptyArray(rawInput, "cleanup", i);

                if (isExplicitEmpty || newPoints.Count == 0)
                {
                    // Archive (was merged elsewhere)
                    await _storyService.MoveStoryPointToCategoryAsync(original.Id, "archived");
                    archived++;
                }
                else if (newPoints.Count == 1 && newPoints[0] == original.Text)
                {
                    // Keep as-is
                    kept++;
                }
                else
                {
                    // Replace: archive original and create new points
                    await _storyService.MoveStoryPointToCategoryAsync(original.Id, "archived");
                    archived++;

                    foreach (var np in newPoints)
                    {
                        if (!string.IsNullOrWhiteSpace(np))
                        {
                            await _storyService.AddStoryPointAsync(_project.Id, np, original.Category);
                            created++;
                        }
                    }
                }
            }

            await LoadPointsAsync();
            
            _loadingOverlay.IsVisible = false;
            
            await DisplayAlert("Cleanup Complete", 
                $"✅ Created: {created}\n📦 Archived: {archived}\n📌 Kept: {kept}", "OK");
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Error", $"Cleanup failed: {ex.Message}", "OK");
        }
    }

    private List<string> ParseStringArrayAssignment(string input, string varName, int index)
    {
        var results = new List<string>();
        
        // Match: varName[index] = new[] { "value1", "value2" };
        var pattern = $@"{varName}\[{index}\]\s*=\s*new\[\]\s*\{{\s*((?:[^}}]*)?)\s*\}}";
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (!match.Success) return results;
        
        string content = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(content)) return results;
        
        // Parse individual strings
        var stringPattern = @"""((?:[^""\\]|\\.)*)""";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, stringPattern);
        
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            string value = m.Groups[1].Value;
            value = value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(value))
            {
                results.Add(value.Trim());
            }
        }
        
        return results;
    }

    private bool IsExplicitEmptyArray(string input, string varName, int index)
    {
        // Check if cleanup[N] = new[] { }; (explicit empty)
        var pattern = $@"{varName}\[{index}\]\s*=\s*new\[\]\s*\{{\s*\}}";
        return System.Text.RegularExpressions.Regex.IsMatch(input, pattern);
    }

    private async Task ExportPendingIntoDraftAsync()
    {
        var pending = _points.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();
        var active = _points.Where(p => p.Category == "active").OrderBy(p => p.DisplayOrder).ToList();

        if (pending.Count == 0)
        {
            await DisplayAlert("No Pending Points", "No pending points to work into the draft.", "OK");
            return;
        }

        // Get all drafts for this project
        int rootProjectId = _project.ParentProjectId ?? _project.Id;
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var drafts = allProjects
            .Where(p => p.Id == rootProjectId || p.ParentProjectId == rootProjectId)
            .OrderByDescending(p => p.IsLatest)
            .ThenByDescending(p => p.DraftVersion)
            .ToList();

        if (drafts.Count == 0)
        {
            await DisplayAlert("No Drafts", "No drafts found for this project.", "OK");
            return;
        }

        // Let user pick a draft
        var draftOptions = drafts.Select(d => 
            $"{d.Name}{(d.IsLatest ? " ★" : "")}{(d.DraftVersion > 1 ? $" (v{d.DraftVersion})" : "")}").ToArray();
        
        var selected = await DisplayActionSheet("Select Draft to Expand", "Cancel", null, draftOptions);
        if (selected == null || selected == "Cancel") return;

        int selectedIndex = Array.IndexOf(draftOptions, selected);
        var selectedDraft = drafts[selectedIndex];

        // Get all lines from the draft
        var lines = await _storyService.GetLinesAsync(selectedDraft.Id);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Create a NEW DRAFT that incorporates the PENDING story points into the existing draft.");
        sb.AppendLine();
        sb.AppendLine($"CURRENT DRAFT: {selectedDraft.Name}");
        sb.AppendLine($"LINES: {lines.Count}");
        sb.AppendLine();
        sb.AppendLine("CURRENT DRAFT CONTENT:");
        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            string lineType = line.IsSilent ? "VISUAL" : "NARR";
            string text = line.IsSilent ? "(silent)" : line.LineText;
            sb.AppendLine($"[{line.LineOrder}] {lineType}: {text}");
            if (!string.IsNullOrWhiteSpace(line.VisualDescription))
                sb.AppendLine($"       Visual: {line.VisualDescription}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("ACTIVE STORY POINTS (already covered - for context):");
        for (int i = 0; i < active.Count; i++)
        {
            sb.AppendLine($"  {i + 1}. {active[i].Text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("PENDING STORY POINTS (MUST be worked into the new draft):");
        for (int i = 0; i < pending.Count; i++)
        {
            sb.AppendLine($"  {i + 1}. {pending[i].Text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Create a new draft that includes ALL pending points");
        sb.AppendLine("2. Integrate them naturally - add new lines, expand existing lines, or restructure as needed");
        sb.AppendLine("3. Maintain the narrative flow and tone of the original draft");
        sb.AppendLine("4. Each pending point should be clearly addressed somewhere in the new draft");
        sb.AppendLine();
        sb.AppendLine("Output the new draft using the standard draft import format (lines with Script and Visual).");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", 
            $"Copied prompt with {lines.Count} draft lines, {active.Count} active points, and {pending.Count} pending points.\n\nPaste to LLM, then import the response in Story Production page.", "OK");
    }

    private async Task ExportCheckPendingInDraftAsync()
    {
        var pending = _points.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();

        if (pending.Count == 0)
        {
            await DisplayAlert("No Pending Points", "No pending points to check.", "OK");
            return;
        }

        // Get all drafts for this project
        int rootProjectId = _project.ParentProjectId ?? _project.Id;
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var drafts = allProjects
            .Where(p => p.Id == rootProjectId || p.ParentProjectId == rootProjectId)
            .OrderByDescending(p => p.IsLatest)
            .ThenByDescending(p => p.DraftVersion)
            .ToList();

        if (drafts.Count == 0)
        {
            await DisplayAlert("No Drafts", "No drafts found for this project.", "OK");
            return;
        }

        // Let user pick a draft
        var draftOptions = drafts.Select(d => 
            $"{d.Name}{(d.IsLatest ? " ★" : "")}{(d.DraftVersion > 1 ? $" (v{d.DraftVersion})" : "")}").ToArray();
        
        var selected = await DisplayActionSheet("Select Draft to Check Against", "Cancel", null, draftOptions);
        if (selected == null || selected == "Cancel") return;

        int selectedIndex = Array.IndexOf(draftOptions, selected);
        var selectedDraft = drafts[selectedIndex];

        // Get all lines from the draft
        var lines = await _storyService.GetLinesAsync(selectedDraft.Id);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Check which PENDING story points are now COVERED in this draft.");
        sb.AppendLine();
        sb.AppendLine($"DRAFT: {selectedDraft.Name}");
        sb.AppendLine($"LINES: {lines.Count}");
        sb.AppendLine();
        sb.AppendLine("DRAFT CONTENT:");
        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            string lineType = line.IsSilent ? "VISUAL" : "NARR";
            string text = line.IsSilent ? "(silent)" : line.LineText;
            sb.AppendLine($"[{line.LineOrder}] {lineType}: {text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("PENDING STORY POINTS TO CHECK:");
        for (int i = 0; i < pending.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {pending[i].Text}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("For each pending point, determine if it is now COVERED in the draft.");
        sb.AppendLine("A point is COVERED if the draft adequately addresses its core idea.");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with a C# array of the point numbers that ARE NOW COVERED:");
        sb.AppendLine("```csharp");
        sb.AppendLine("int[] nowCovered = { };  // empty if none are covered");
        sb.AppendLine("// or");
        sb.AppendLine("int[] nowCovered = { 1, 3, 5 };  // point numbers now covered");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("DO NOT include commentary. Only output the C# array.");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", 
            $"Copied check prompt with {pending.Count} pending points and {lines.Count} draft lines.\n\nPaste to LLM, copy its response, then use 'Import Pending Check Result'.", "OK");
    }

    private async Task ImportPendingCheckResultAsync()
    {
        var pending = _points.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();

        if (pending.Count == 0)
        {
            await DisplayAlert("No Pending Points", "No pending points to process.", "OK");
            return;
        }

        string? input = await ShowMultiLineInputAsync(
            "Import Pending Check Result",
            "Paste the LLM response.\nExpecting: int[] nowCovered = { 1, 3, 5 };",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse nowCovered array
        var coveredNumbers = ParseIntArrayFromInput(input, "nowCovered");

        // Validate numbers
        coveredNumbers = coveredNumbers.Where(n => n >= 1 && n <= pending.Count).Distinct().ToList();

        if (coveredNumbers.Count == 0)
        {
            await DisplayAlert("None Covered", "No pending points are covered in the draft yet.", "OK");
            return;
        }

        // Show confirmation UI
        await ShowPendingCoveredReviewAsync(pending, coveredNumbers);
    }

    private async Task ShowPendingCoveredReviewAsync(List<StoryPoint> pendingPoints, List<int> coveredNumbers)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Track which to move to active
        var moveToActive = new Dictionary<int, bool>();
        foreach (var n in coveredNumbers)
            moveToActive[n] = true; // Default: move to active

        // Create overlay
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            ZIndex = 1000
        };

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D30"),
            BorderColor = Color.FromArgb("#3E3E42"),
            CornerRadius = 12,
            Padding = new Thickness(20),
            MaximumWidthRequest = 800,
            MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = false
        };

        var mainLayout = new VerticalStackLayout { Spacing = 16 };

        // Header
        mainLayout.Children.Add(new Label
        {
            Text = "✅ Pending Points Now Covered",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        // Stats
        int stillPending = pendingPoints.Count - coveredNumbers.Count;
        mainLayout.Children.Add(new Label
        {
            Text = $"📊 {coveredNumbers.Count} now covered • {stillPending} still pending",
            FontSize = 13,
            TextColor = Color.FromArgb("#AAAAAA")
        });

        mainLayout.Children.Add(new Label
        {
            Text = "Select which points to move to Active:",
            FontSize = 13,
            TextColor = Color.FromArgb("#CCCCCC")
        });

        // Scrollable content
        var scrollView = new ScrollView { MaximumHeightRequest = 380 };
        var itemsList = new VerticalStackLayout { Spacing = 10 };

        foreach (var n in coveredNumbers)
        {
            int index = n; // Capture
            var point = pendingPoints[n - 1];

            var itemCard = new Frame
            {
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                BorderColor = Color.FromArgb("#27AE60"),
                CornerRadius = 8,
                Padding = new Thickness(12),
                HasShadow = false
            };

            var itemLayout = new HorizontalStackLayout { Spacing = 12 };

            var checkBox = new CheckBox
            {
                IsChecked = true,
                Color = Color.FromArgb("#27AE60"),
                VerticalOptions = LayoutOptions.Center
            };
            checkBox.CheckedChanged += (s, e) => moveToActive[index] = e.Value;
            itemLayout.Children.Add(checkBox);

            var textLayout = new VerticalStackLayout { Spacing = 4 };
            textLayout.Children.Add(new Label
            {
                Text = $"#{n}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#27AE60")
            });
            textLayout.Children.Add(new Label
            {
                Text = point.Text,
                FontSize = 12,
                TextColor = Colors.White,
                LineBreakMode = LineBreakMode.WordWrap
            });

            itemLayout.Children.Add(textLayout);
            itemCard.Content = itemLayout;
            itemsList.Children.Add(itemCard);
        }

        scrollView.Content = itemsList;
        mainLayout.Children.Add(scrollView);

        // Button row
        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#4A4A4A"),
            TextColor = Colors.White,
            WidthRequest = 100,
            HeightRequest = 40
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(false);
        };
        buttonRow.Children.Add(cancelBtn);

        var applyBtn = new Button
        {
            Text = "✅ Move to Active",
            BackgroundColor = Color.FromArgb("#27AE60"),
            TextColor = Colors.White,
            WidthRequest = 150,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        };
        applyBtn.Clicked += async (s, e) =>
        {
            CloseOverlay(overlay);
            _loadingOverlay.IsVisible = true;

            try
            {
                int movedCount = 0;

                foreach (var n in coveredNumbers)
                {
                    if (moveToActive[n])
                    {
                        var point = pendingPoints[n - 1];
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, "active");
                        movedCount++;
                    }
                }

                await LoadPointsAsync();
                _loadingOverlay.IsVisible = false;

                await DisplayAlert("Done", $"Moved {movedCount} points from Pending to Active.", "OK");
            }
            catch (Exception ex)
            {
                _loadingOverlay.IsVisible = false;
                await DisplayAlert("Error", ex.Message, "OK");
            }

            tcs.TrySetResult(true);
        };
        buttonRow.Children.Add(applyBtn);

        mainLayout.Children.Add(buttonRow);
        card.Content = mainLayout;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            pageGrid.Children.Add(overlay);
        }

        await tcs.Task;
    }

    private async Task ExportDraftToPointsAsync()
    {
        // Get all drafts for this project
        int rootProjectId = _project.ParentProjectId ?? _project.Id;
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var drafts = allProjects
            .Where(p => p.Id == rootProjectId || p.ParentProjectId == rootProjectId)
            .OrderByDescending(p => p.IsLatest)
            .ThenByDescending(p => p.DraftVersion)
            .ToList();

        if (drafts.Count == 0)
        {
            await DisplayAlert("No Drafts", "No drafts found for this project.", "OK");
            return;
        }

        // Build picker options
        var draftOptions = drafts.Select(d =>
        {
            string label = d.ParentProjectId == null ? $"Root: {d.Name}" : $"v{d.DraftVersion}: {d.Name}";
            if (d.IsLatest) label += " (latest)";
            return label;
        }).ToArray();

        var selectedDraft = await DisplayActionSheet("Select Draft to Extract Points From", "Cancel", null, draftOptions);
        if (selectedDraft == null || selectedDraft == "Cancel") return;

        int selectedIndex = Array.IndexOf(draftOptions, selectedDraft);
        if (selectedIndex < 0) return;

        var draft = drafts[selectedIndex];

        // Get the draft content
        var lines = await _storyService.GetLinesAsync(draft.Id);
        if (lines.Count == 0)
        {
            await DisplayAlert("Empty Draft", "This draft has no content.", "OK");
            return;
        }

        // Build draft text
        var draftContent = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!string.IsNullOrWhiteSpace(line.LineText))
            {
                draftContent.AppendLine($"[{i + 1}] {line.LineText}");
            }
            if (!string.IsNullOrWhiteSpace(line.VisualDescription))
            {
                draftContent.AppendLine($"    Visual: {line.VisualDescription}");
            }
        }

        // Build the prompt
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Extract story points from this draft script.");
        sb.AppendLine();
        sb.AppendLine("DEFINITIONS:");
        sb.AppendLine("- CHRONOLOGICAL = Points describing specific STORY BEATS/SCENES/MOMENTS.");
        sb.AppendLine("  These happen at a particular point in the narrative timeline.");
        sb.AppendLine("  Examples: 'Hook scene at bar', 'Revelation moment', 'Final image'.");
        sb.AppendLine("  Order: beginning of story → end of story.");
        sb.AppendLine();
        sb.AppendLine("- MISC = Meta-points that don't belong to a specific moment.");
        sb.AppendLine("  Examples: visual motifs, thematic elements, production notes.");
        sb.AppendLine("  Order: most important → least important.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: Most story points ARE chronological. Only put in Misc if");
        sb.AppendLine("the point truly doesn't describe a specific scene or moment.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"DRAFT: {draft.Name}");
        sb.AppendLine();
        sb.AppendLine(draftContent.ToString());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Output ONLY a C# code block with string arrays:");
        sb.AppendLine("```csharp");
        sb.AppendLine("// Chronological points (story order, beginning to end)");
        sb.AppendLine("string[] chronological = {");
        sb.AppendLine("    \"Hook: description of opening scene\",");
        sb.AppendLine("    \"Next beat: what happens next\",");
        sb.AppendLine("    \"Climax: the turning point\",");
        sb.AppendLine("    \"Ending: final image or line\"");
        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("// Misc points (importance order, most important first)");
        sb.AppendLine("string[] misc = {");
        sb.AppendLine("    \"Visual motif: description\",");
        sb.AppendLine("    \"Thematic element: description\"");
        sb.AppendLine("};");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Extract ALL meaningful story beats and elements from the draft");
        sb.AppendLine("- Each point should be a concise description (1-2 sentences max)");
        sb.AppendLine("- Use format 'Label: description' for clarity");
        sb.AppendLine("- Put MOST points in chronological - only use misc for true meta-points");
        sb.AppendLine("- No commentary outside the code block");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", 
            $"Copied draft '{draft.Name}' ({lines.Count} lines) with extraction prompt.\n\nPaste to LLM, copy its response, then use 'Import Draft to Points Result'.", "OK");
    }

    private async Task ImportDraftToPointsResultAsync()
    {
        string? input = await ShowMultiLineInputAsync(
            "Import Draft to Points Result",
            "Paste the LLM response.\nExpecting:\nstring[] chronological = { \"...\", \"...\" };\nstring[] misc = { \"...\", \"...\" };",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse both arrays
        var chronoStrings = ParseStringArrayFromInput(input, "chronological");
        var miscStrings = ParseStringArrayFromInput(input, "misc");

        if (chronoStrings.Count == 0 && miscStrings.Count == 0)
        {
            await DisplayAlert("No Points Found", "Could not parse any points from the input.", "OK");
            return;
        }

        // Show confirmation
        bool confirm = await DisplayAlert("Import Points?",
            $"Found {chronoStrings.Count} chronological + {miscStrings.Count} misc points.\n\nAdd these to current version?",
            "Import", "Cancel");
        if (!confirm) return;

        _loadingOverlay.IsVisible = true;

        try
        {
            // Add chronological points
            foreach (var text in chronoStrings)
            {
                await _storyService.AddStoryPointAsync(_project.Id, text, "active", "chronological");
            }

            // Add misc points
            foreach (var text in miscStrings)
            {
                await _storyService.AddStoryPointAsync(_project.Id, text, "active", "misc");
            }

            await LoadPointsAsync();
            _loadingOverlay.IsVisible = false;

            await DisplayAlert("Imported",
                $"Added {chronoStrings.Count} chronological + {miscStrings.Count} misc points.",
                "OK");
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private List<string> ParseStringArrayFromInput(string input, string arrayName)
    {
        var result = new List<string>();

        // Find the array by name: string[] arrayName = { "...", "..." };
        // Also handle variations like: var arrayName = new[] { "...", "..." };
        
        // Look for the array name followed by = and {
        int nameIndex = input.IndexOf(arrayName, StringComparison.OrdinalIgnoreCase);
        if (nameIndex < 0) return result;

        int braceStart = input.IndexOf('{', nameIndex);
        if (braceStart < 0) return result;

        // Find matching closing brace
        int braceDepth = 1;
        int braceEnd = -1;
        for (int i = braceStart + 1; i < input.Length; i++)
        {
            if (input[i] == '{') braceDepth++;
            else if (input[i] == '}')
            {
                braceDepth--;
                if (braceDepth == 0)
                {
                    braceEnd = i;
                    break;
                }
            }
        }

        if (braceEnd < 0) return result;

        string content = input.Substring(braceStart + 1, braceEnd - braceStart - 1);

        // Parse quoted strings
        bool inString = false;
        bool escaped = false;
        var currentString = new System.Text.StringBuilder();

        foreach (char c in content)
        {
            if (escaped)
            {
                currentString.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                if (inString)
                {
                    // End of string
                    string parsed = currentString.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        result.Add(parsed);
                    }
                    currentString.Clear();
                }
                inString = !inString;
                continue;
            }

            if (inString)
            {
                currentString.Append(c);
            }
        }

        return result;
    }

    private async Task ExportCompareVersionsAsync()
    {
        // Need at least 2 versions to compare
        if (_versions.Count < 2)
        {
            await DisplayAlert("Not Enough Versions", "You need at least 2 versions to compare.", "OK");
            return;
        }

        // Build picker options for other versions (exclude current)
        var otherVersions = _versions.Where(v => v.Version != _currentVersion).OrderByDescending(v => v.Version).ToList();
        
        var versionOptions = otherVersions.Select(v =>
        {
            string label = string.IsNullOrEmpty(v.Name) ? $"Version {v.Version}" : $"v{v.Version}: {v.Name}";
            return label;
        }).ToArray();

        var selectedVersion = await DisplayActionSheet("Compare Current with Which Version?", "Cancel", null, versionOptions);
        if (selectedVersion == null || selectedVersion == "Cancel") return;

        int selectedIndex = Array.IndexOf(versionOptions, selectedVersion);
        if (selectedIndex < 0) return;

        var compareVersion = otherVersions[selectedIndex];

        // Get points from both versions
        var currentPoints = await _storyService.GetStoryPointsAsync(_project.Id, _currentVersion);
        var comparePoints = await _storyService.GetStoryPointsAsync(_project.Id, compareVersion.Version);

        if (comparePoints.Count == 0)
        {
            await DisplayAlert("Empty Version", "The selected version has no points.", "OK");
            return;
        }

        // Build the prompt
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Compare two versions of story points and find valuable points that were lost.");
        sb.AppendLine();
        sb.AppendLine("TASK: Identify points from the OLD VERSION that:");
        sb.AppendLine("1. Are NOT present (or not adequately covered) in the CURRENT VERSION");
        sb.AppendLine("2. Should be reconsidered for inclusion (valuable ideas that may have been dropped)");
        sb.AppendLine();
        sb.AppendLine("Ignore points that were intentionally removed, consolidated, or are redundant.");
        sb.AppendLine("Only return points that represent genuinely valuable ideas worth revisiting.");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"CURRENT VERSION (v{_currentVersion}) - {currentPoints.Count} points:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Current version points by category
        var currentChrono = currentPoints.Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory))).OrderBy(p => p.DisplayOrder).ToList();
        var currentMisc = currentPoints.Where(p => p.Category == "active" && p.Subcategory == "misc").OrderBy(p => p.DisplayOrder).ToList();
        var currentPending = currentPoints.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();
        var currentPossible = currentPoints.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();

        if (currentChrono.Count > 0)
        {
            sb.AppendLine("📖 ACTIVE: CHRONOLOGICAL:");
            foreach (var p in currentChrono)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }
        if (currentMisc.Count > 0)
        {
            sb.AppendLine("📌 ACTIVE: MISC:");
            foreach (var p in currentMisc)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }
        if (currentPending.Count > 0)
        {
            sb.AppendLine("⏳ PENDING:");
            foreach (var p in currentPending)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }
        if (currentPossible.Count > 0)
        {
            sb.AppendLine("💡 POSSIBLE:");
            foreach (var p in currentPossible)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"OLD VERSION (v{compareVersion.Version}: {compareVersion.Name}) - {comparePoints.Count} points:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Old version points by category
        var oldChrono = comparePoints.Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory))).OrderBy(p => p.DisplayOrder).ToList();
        var oldMisc = comparePoints.Where(p => p.Category == "active" && p.Subcategory == "misc").OrderBy(p => p.DisplayOrder).ToList();
        var oldPending = comparePoints.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();
        var oldPossible = comparePoints.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();

        if (oldChrono.Count > 0)
        {
            sb.AppendLine("📖 ACTIVE: CHRONOLOGICAL:");
            foreach (var p in oldChrono)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }
        if (oldMisc.Count > 0)
        {
            sb.AppendLine("📌 ACTIVE: MISC:");
            foreach (var p in oldMisc)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }
        if (oldPending.Count > 0)
        {
            sb.AppendLine("⏳ PENDING:");
            foreach (var p in oldPending)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }
        if (oldPossible.Count > 0)
        {
            sb.AppendLine("💡 POSSIBLE:");
            foreach (var p in oldPossible)
                sb.AppendLine($"  - {p.Text}");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("Output ONLY a C# code block with points to reconsider:");
        sb.AppendLine("```csharp");
        sb.AppendLine("// Points from old version worth reconsidering (will be added to Pending)");
        sb.AppendLine("string[] reconsider = {");
        sb.AppendLine("    \"Point text from old version that's valuable\",");
        sb.AppendLine("    \"Another point that shouldn't have been dropped\"");
        sb.AppendLine("};");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Only include points that are genuinely missing AND valuable");
        sb.AppendLine("- Don't include points that are covered by similar/equivalent points in current");
        sb.AppendLine("- Don't include points that were clearly intentionally removed or consolidated");
        sb.AppendLine("- Copy the exact text from the old version");
        sb.AppendLine("- If nothing valuable is missing, return an empty array: string[] reconsider = { };");
        sb.AppendLine("- No commentary outside the code block");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied",
            $"Comparing v{_currentVersion} ({currentPoints.Count} pts) with v{compareVersion.Version} ({comparePoints.Count} pts).\n\nPaste to LLM, copy its response, then use 'Import Version Comparison Result'.", "OK");
    }

    private async Task ImportVersionComparisonResultAsync()
    {
        string? input = await ShowMultiLineInputAsync(
            "Import Version Comparison Result",
            "Paste the LLM response.\nExpecting:\nstring[] reconsider = { \"...\", \"...\" };",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse the reconsider array
        var reconsiderStrings = ParseStringArrayFromInput(input, "reconsider");

        if (reconsiderStrings.Count == 0)
        {
            await DisplayAlert("No Points", "No points to reconsider were found in the response.\n\nThis could mean nothing valuable was missing from the current version.", "OK");
            return;
        }

        // Show confirmation
        bool confirm = await DisplayAlert("Import to Pending?",
            $"Found {reconsiderStrings.Count} point(s) to reconsider.\n\nAdd these to Pending category?",
            "Import", "Cancel");
        if (!confirm) return;

        _loadingOverlay.IsVisible = true;

        try
        {
            // Add all points to Pending category
            foreach (var text in reconsiderStrings)
            {
                await _storyService.AddStoryPointAsync(_project.Id, text, "pending");
            }

            await LoadPointsAsync();
            _loadingOverlay.IsVisible = false;

            await DisplayAlert("Imported",
                $"Added {reconsiderStrings.Count} point(s) to Pending for reconsideration.",
                "OK");
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // Store the last exported point and draft for import
    private int _lastInsertPointId = 0;
    private int _lastInsertDraftId = 0;

    private async Task ExportInsertPointInDraftAsync(StoryPoint point)
    {
        // Get all drafts for this project
        int rootProjectId = _project.ParentProjectId ?? _project.Id;
        var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var drafts = allProjects
            .Where(p => p.Id == rootProjectId || p.ParentProjectId == rootProjectId)
            .OrderByDescending(p => p.IsLatest)
            .ThenByDescending(p => p.DraftVersion)
            .ToList();

        if (drafts.Count == 0)
        {
            await DisplayAlert("No Drafts", "No drafts found for this project.", "OK");
            return;
        }

        // Build picker options
        var draftOptions = drafts.Select(d =>
        {
            string label = d.ParentProjectId == null ? $"Root: {d.Name}" : $"v{d.DraftVersion}: {d.Name}";
            if (d.IsLatest) label += " (latest)";
            return label;
        }).ToArray();

        var selectedDraft = await DisplayActionSheet("Insert Point in Which Draft?", "Cancel", null, draftOptions);
        if (selectedDraft == null || selectedDraft == "Cancel") return;

        int selectedIndex = Array.IndexOf(draftOptions, selectedDraft);
        if (selectedIndex < 0) return;

        var draft = drafts[selectedIndex];

        // Get the draft content
        var lines = await _storyService.GetLinesAsync(draft.Id);
        if (lines.Count == 0)
        {
            await DisplayAlert("Empty Draft", "This draft has no content to modify.", "OK");
            return;
        }

        // Store for import
        _lastInsertPointId = point.Id;
        _lastInsertDraftId = draft.Id;

        // Build draft text with line numbers
        var draftContent = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            draftContent.AppendLine($"[{i + 1}] {line.LineText}");
            if (!string.IsNullOrWhiteSpace(line.VisualDescription))
            {
                draftContent.AppendLine($"    Visual: {line.VisualDescription}");
            }
        }

        // Build the prompt
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Integrate this story point into the draft script.");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("STORY POINT TO INSERT:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine(point.Text);
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"CURRENT DRAFT: {draft.Name} ({lines.Count} lines)");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine(draftContent.ToString());
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("Analyze where and how this point should be integrated. Output commands:");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine("// Commands to apply (use only the ones needed)");
        sb.AppendLine();
        sb.AppendLine("// INSERT: Add new line(s) at a position");
        sb.AppendLine("// afterLine = line number to insert after (0 = at start)");
        sb.AppendLine("var insert1 = new { afterLine = 5, script = \"New narration text\", visual = \"Visual description\" };");
        sb.AppendLine("var insert2 = new { afterLine = 5, script = \"Another new line\", visual = \"\" }; // Multiple inserts OK");
        sb.AppendLine();
        sb.AppendLine("// UPDATE: Modify existing line(s)");
        sb.AppendLine("var update1 = new { line = 3, script = \"Updated narration\", visual = \"Updated visual\" };");
        sb.AppendLine("var update2 = new { line = 7, script = \"Another update\", visual = \"\" }; // Empty = keep original");
        sb.AppendLine();
        sb.AppendLine("// DELETE: Remove line(s) that become redundant");
        sb.AppendLine("int[] deleteLines = { 4, 8 }; // Lines made irrelevant by this insertion");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Use INSERT to add new content where the point should appear");
        sb.AppendLine("- Use UPDATE if existing lines need modification to accommodate the point");
        sb.AppendLine("- Use DELETE only if lines become truly redundant (not just similar)");
        sb.AppendLine("- For visual: provide description or leave empty string to keep original");
        sb.AppendLine("- afterLine = 0 means insert at the very beginning");
        sb.AppendLine("- You can have multiple inserts, updates, and deletes");
        sb.AppendLine("- Keep the story flow natural and coherent");
        sb.AppendLine("- No commentary outside the code block");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied",
            $"Point: \"{(point.Text.Length > 50 ? point.Text.Substring(0, 47) + "..." : point.Text)}\"\n\n" +
            $"Draft: {draft.Name} ({lines.Count} lines)\n\n" +
            "Paste to LLM, copy its response, then use Export → 'Import Insert Point in Draft Result'.", "OK");
    }

    private async Task ImportInsertPointInDraftResultAsync()
    {
        if (_lastInsertDraftId == 0)
        {
            await DisplayAlert("No Pending Insert", "Use 'Insert in Draft (LLM)' from a point's context menu first.", "OK");
            return;
        }

        // Get the source draft
        var sourceDraft = await _storyService.GetProjectByIdAsync(_lastInsertDraftId);
        if (sourceDraft == null)
        {
            await DisplayAlert("Error", "Source draft not found.", "OK");
            return;
        }

        string? input = await ShowMultiLineInputAsync(
            "Import Insert Result",
            "Paste the LLM response with insert/update/delete commands:",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse commands
        var inserts = ParseInsertCommands(input);
        var updates = ParseUpdateCommands(input);
        var deletes = ParseDeleteArray(input);

        if (inserts.Count == 0 && updates.Count == 0 && deletes.Count == 0)
        {
            await DisplayAlert("No Commands", "Could not parse any insert, update, or delete commands.", "OK");
            return;
        }

        // Show confirmation
        var summary = new List<string>();
        if (inserts.Count > 0) summary.Add($"{inserts.Count} insert(s)");
        if (updates.Count > 0) summary.Add($"{updates.Count} update(s)");
        if (deletes.Count > 0) summary.Add($"{deletes.Count} delete(s)");

        // Ask for new draft name
        string? newDraftName = await DisplayPromptAsync(
            "New Draft Name",
            $"Found: {string.Join(", ", summary)}\n\nEnter name for the new draft version:",
            initialValue: sourceDraft.Name,
            accept: "Create",
            cancel: "Cancel");
        
        if (string.IsNullOrWhiteSpace(newDraftName)) return;

        _loadingOverlay.IsVisible = true;

        try
        {
            // Create new draft version
            var newDraft = await _storyService.CreateDraftVersionAsync(sourceDraft.Id, newDraftName.Trim());

            // Get lines from new draft
            var lines = await _storyService.GetLinesAsync(newDraft.Id);

            // Apply deletes first (in reverse order to maintain line numbers)
            foreach (var lineNum in deletes.OrderByDescending(x => x))
            {
                if (lineNum >= 1 && lineNum <= lines.Count)
                {
                    var lineToDelete = lines[lineNum - 1];
                    await _storyService.DeleteLineAsync(lineToDelete.Id);
                }
            }

            // Refresh lines after deletes
            lines = await _storyService.GetLinesAsync(newDraft.Id);

            // Apply updates
            foreach (var upd in updates)
            {
                int adjustedLine = upd.Line;
                // Adjust for deleted lines before this one
                foreach (var del in deletes.Where(d => d < upd.Line).OrderBy(x => x))
                {
                    adjustedLine--;
                }

                if (adjustedLine >= 1 && adjustedLine <= lines.Count)
                {
                    var lineToUpdate = lines[adjustedLine - 1];
                    if (!string.IsNullOrEmpty(upd.Script))
                        lineToUpdate.LineText = upd.Script;
                    if (!string.IsNullOrEmpty(upd.Visual))
                        lineToUpdate.VisualDescription = upd.Visual;
                    await _storyService.UpdateLineAsync(lineToUpdate);
                }
            }

            // Apply inserts (in order, adjusting for previous inserts)
            int insertOffset = 0;
            foreach (var ins in inserts.OrderBy(i => i.AfterLine))
            {
                int adjustedAfter = ins.AfterLine + insertOffset;
                // Adjust for deleted lines before this position
                foreach (var del in deletes.Where(d => d <= ins.AfterLine).OrderBy(x => x))
                {
                    adjustedAfter--;
                }

                await _storyService.InsertLineBeforeAsync(
                    newDraft.Id,
                    adjustedAfter + 1, // InsertLineBefore uses beforeOrder
                    ins.Script ?? "",
                    ins.Visual ?? "",
                    false // not silent
                );
                insertOffset++;
            }

            _loadingOverlay.IsVisible = false;

            await DisplayAlert("Done",
                $"Created new draft 'v{newDraft.DraftVersion}: {newDraft.Name}'\n\n" +
                $"Applied: {string.Join(", ", summary)}",
                "OK");

            // Clear the pending insert
            _lastInsertPointId = 0;
            _lastInsertDraftId = 0;
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private List<(int AfterLine, string? Script, string? Visual)> ParseInsertCommands(string input)
    {
        var result = new List<(int, string?, string?)>();
        
        // Match: var insertN = new { afterLine = X, script = "...", visual = "..." };
        var pattern = @"var\s+insert\d*\s*=\s*new\s*\{\s*afterLine\s*=\s*(\d+)\s*,\s*script\s*=\s*""([^""]*)""\s*,\s*visual\s*=\s*""([^""]*)""";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int afterLine))
            {
                result.Add((afterLine, match.Groups[2].Value, match.Groups[3].Value));
            }
        }
        
        return result;
    }

    private List<(int Line, string? Script, string? Visual)> ParseUpdateCommands(string input)
    {
        var result = new List<(int, string?, string?)>();
        
        // Match: var updateN = new { line = X, script = "...", visual = "..." };
        var pattern = @"var\s+update\d*\s*=\s*new\s*\{\s*line\s*=\s*(\d+)\s*,\s*script\s*=\s*""([^""]*)""\s*,\s*visual\s*=\s*""([^""]*)""";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int line))
            {
                result.Add((line, match.Groups[2].Value, match.Groups[3].Value));
            }
        }
        
        return result;
    }

    private List<int> ParseDeleteArray(string input)
    {
        var result = new List<int>();
        
        // Match: int[] deleteLines = { 4, 8 };
        var pattern = @"int\[\]\s+deleteLines\s*=\s*\{\s*([^}]*)\s*\}";
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        
        if (match.Success)
        {
            var numbers = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var num in numbers)
            {
                if (int.TryParse(num.Trim(), out int lineNum))
                {
                    result.Add(lineNum);
                }
            }
        }
        
        return result;
    }

    private async Task ExportPointsSurgicalEditAsync()
    {
        // Ask user what changes they want
        string? instructions = await ShowMultiLineInputAsync(
            "Surgical Edit",
            "Describe the changes you want to make to story points:\n(e.g., \"Delete points 5-7\", \"Add a point after #3\", \"Update point 10 text\", \"Move point 5 to active\")",
            "");

        if (string.IsNullOrWhiteSpace(instructions)) return;

        // Build summary of current points
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Apply surgical edits to story points.");
        sb.AppendLine();
        sb.AppendLine($"USER REQUEST: {instructions}");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"CURRENT POINTS (Version {_currentVersion}):");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Group by category/subcategory
        var activeChronological = _points.Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory))).OrderBy(p => p.DisplayOrder).ToList();
        var activeMisc = _points.Where(p => p.Category == "active" && p.Subcategory == "misc").OrderBy(p => p.DisplayOrder).ToList();
        var partial = _points.Where(p => p.Category == "partial").OrderBy(p => p.DisplayOrder).ToList();
        var pending = _points.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();
        var possible = _points.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();
        var archived = _points.Where(p => p.Category == "archived" || p.Category == "irrelevant").OrderBy(p => p.DisplayOrder).ToList();

        int globalIndex = 1;
        var pointIndexMap = new Dictionary<int, StoryPoint>(); // index -> point

        void AddSection(string title, List<StoryPoint> points, string category, string? subcategory = null)
        {
            if (points.Count == 0) return;
            sb.AppendLine($"--- {title} ---");
            foreach (var p in points)
            {
                pointIndexMap[globalIndex] = p;
                string lockIndicator = "";
                if (p.IsCategoryLocked && p.IsSubcategoryLocked) lockIndicator = " 🔐🔒";
                else if (p.IsCategoryLocked) lockIndicator = " 🔐";
                else if (p.IsSubcategoryLocked) lockIndicator = " 🔒";
                sb.AppendLine($"[{globalIndex}] {p.Text}{lockIndicator}");
                globalIndex++;
            }
            sb.AppendLine();
        }

        AddSection("ACTIVE: CHRONOLOGICAL", activeChronological, "active", "chronological");
        AddSection("ACTIVE: MISC", activeMisc, "active", "misc");
        AddSection("PARTIAL", partial, "partial");
        AddSection("PENDING", pending, "pending");
        AddSection("POSSIBLE", possible, "possible");
        AddSection("ARCHIVED", archived, "archived");

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("Output a C# code block with commands to apply:");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine("// Surgical edit commands for story points");
        sb.AppendLine();
        sb.AppendLine("// DELETE: Remove point(s)");
        sb.AppendLine("delete[5]; // Delete point #5");
        sb.AppendLine();
        sb.AppendLine("// UPDATE: Modify point text");
        sb.AppendLine("update[3] = \"New text for point 3\";");
        sb.AppendLine();
        sb.AppendLine("// INSERT: Add new point after specified index (0 = at start of section)");
        sb.AppendLine("insert[5] = new { category = \"active\", subcategory = \"chronological\", text = \"New point text\" };");
        sb.AppendLine();
        sb.AppendLine("// MOVE: Change point's category/subcategory");
        sb.AppendLine("move[7] = new { category = \"pending\" }; // Move to pending");
        sb.AppendLine("move[8] = new { category = \"active\", subcategory = \"misc\" }; // Move to active misc");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("CATEGORIES: active, partial, pending, possible, archived");
        sb.AppendLine("SUBCATEGORIES (active only): chronological, misc");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Use point numbers [N] as shown above");
        sb.AppendLine("- Points marked 🔐 have category locked, 🔒 have subcategory locked");
        sb.AppendLine("- For INSERT, specify category and subcategory (if active)");
        sb.AppendLine("- For MOVE, only specify what changes");
        sb.AppendLine("- No commentary outside the code block");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied",
            $"Surgical edit prompt ({_points.Count} points) copied to clipboard.\n\nPaste to LLM, copy its response, then use 'Import Surgical Edit Result'.", "OK");
    }

    private async Task ImportPointsSurgicalEditResultAsync()
    {
        string? input = await ShowMultiLineInputAsync(
            "Import Surgical Edit Result",
            "Paste the LLM response with surgical edit commands:",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse commands
        var deleteCommands = ParsePointsDeleteCommands(input);
        var updateCommands = ParsePointsUpdateCommands(input);
        var insertCommands = ParsePointsInsertCommands(input);
        var moveCommands = ParsePointsMoveCommands(input);

        int totalCommands = deleteCommands.Count + updateCommands.Count + insertCommands.Count + moveCommands.Count;

        if (totalCommands == 0)
        {
            await DisplayAlert("No Commands", "Could not parse any surgical edit commands.", "OK");
            return;
        }

        // Show confirmation dialog
        var (confirmed, newVersionName) = await ShowPointsSurgicalEditConfirmationAsync(
            deleteCommands, updateCommands, insertCommands, moveCommands);

        if (confirmed != true || string.IsNullOrWhiteSpace(newVersionName)) return;

        _loadingOverlay.IsVisible = true;

        try
        {
            // Create new version
            int newVersion = await _storyService.CreateNewStoryPointVersionAsync(_project.Id, newVersionName);
            
            // Duplicate current points to new version
            await _storyService.DuplicatePointsToVersionAsync(_project.Id, _currentVersion, newVersion);

            // Get the new version's points
            var newPoints = await _storyService.GetStoryPointsAsync(_project.Id, newVersion);

            // Build index map (1-based index from export -> point in new version)
            var pointIndexMap = BuildPointIndexMap(newPoints);

            // Apply deletes first (won't affect other indices since we're using original indices)
            foreach (var idx in deleteCommands)
            {
                if (pointIndexMap.TryGetValue(idx, out var point))
                {
                    await _storyService.DeleteStoryPointAsync(point.Id);
                }
            }

            // Apply updates
            foreach (var (idx, newText) in updateCommands)
            {
                if (pointIndexMap.TryGetValue(idx, out var point))
                {
                    point.Text = newText;
                    await _storyService.UpdateStoryPointAsync(point);
                }
            }

            // Apply moves
            foreach (var (idx, category, subcategory) in moveCommands)
            {
                if (pointIndexMap.TryGetValue(idx, out var point))
                {
                    if (!string.IsNullOrEmpty(category))
                    {
                        await _storyService.MoveStoryPointToCategoryAsync(point.Id, category, subcategory);
                    }
                    else if (!string.IsNullOrEmpty(subcategory) && point.Category == "active")
                    {
                        await _storyService.UpdateStoryPointSubcategoryAsync(point.Id, subcategory);
                    }
                }
            }

            // Apply inserts
            foreach (var (afterIdx, category, subcategory, text) in insertCommands)
            {
                await _storyService.AddStoryPointAsync(_project.Id, text, category, subcategory);
            }

            // Switch to new version
            _currentVersion = newVersion;
            await LoadVersionsAsync();
            await LoadPointsAsync();

            _loadingOverlay.IsVisible = false;

            // Ask if should set as latest
            bool setAsLatest = await DisplayAlert("Set as Latest?",
                $"Created version v{newVersion}: {newVersionName}\n\n" +
                $"Applied: {deleteCommands.Count} delete(s), {updateCommands.Count} update(s), " +
                $"{moveCommands.Count} move(s), {insertCommands.Count} insert(s)\n\n" +
                "Set this as the 'latest' working version?",
                "Yes, Set as Latest", "No");
            
            if (setAsLatest)
            {
                await _storyService.SetStoryPointVersionAsLatestAsync(_project.Id, newVersion);
                await LoadVersionsAsync(); // Refresh to show latest indicator
            }
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private Dictionary<int, StoryPoint> BuildPointIndexMap(List<StoryPoint> points)
    {
        var map = new Dictionary<int, StoryPoint>();
        int globalIndex = 1;

        // Same order as export
        var activeChronological = points.Where(p => p.Category == "active" && (p.Subcategory == "chronological" || string.IsNullOrEmpty(p.Subcategory))).OrderBy(p => p.DisplayOrder).ToList();
        var activeMisc = points.Where(p => p.Category == "active" && p.Subcategory == "misc").OrderBy(p => p.DisplayOrder).ToList();
        var partial = points.Where(p => p.Category == "partial").OrderBy(p => p.DisplayOrder).ToList();
        var pending = points.Where(p => p.Category == "pending").OrderBy(p => p.DisplayOrder).ToList();
        var possible = points.Where(p => p.Category == "possible").OrderBy(p => p.DisplayOrder).ToList();
        var archived = points.Where(p => p.Category == "archived" || p.Category == "irrelevant").OrderBy(p => p.DisplayOrder).ToList();

        foreach (var list in new[] { activeChronological, activeMisc, partial, pending, possible, archived })
        {
            foreach (var p in list)
            {
                map[globalIndex] = p;
                globalIndex++;
            }
        }

        return map;
    }

    private async Task<(bool? Confirmed, string? VersionName)> ShowPointsSurgicalEditConfirmationAsync(
        List<int> deletes,
        List<(int Index, string Text)> updates,
        List<(int AfterIndex, string Category, string? Subcategory, string Text)> inserts,
        List<(int Index, string Category, string? Subcategory)> moves)
    {
        var tcs = new TaskCompletionSource<(bool?, string?)>();

        // Build point index map to show original text
        var pointIndexMap = BuildPointIndexMap(_points);

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            ZIndex = 1000
        };

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D30"),
            BorderColor = Color.FromArgb("#3E3E42"),
            CornerRadius = 12,
            Padding = new Thickness(20),
            MaximumWidthRequest = 800,
            MaximumHeightRequest = 650,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = false
        };

        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),   // Header
                new RowDefinition(GridLength.Auto),   // Summary
                new RowDefinition(GridLength.Star),   // Commands list
                new RowDefinition(GridLength.Auto),   // Version name
                new RowDefinition(GridLength.Auto)    // Buttons
            },
            RowSpacing = 12
        };

        // Header
        var header = new Label
        {
            Text = "🔧 Confirm Surgical Edit",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        Grid.SetRow(header, 0);
        mainGrid.Children.Add(header);

        // Summary
        var summaryParts = new List<string>();
        if (deletes.Count > 0) summaryParts.Add($"{deletes.Count} delete(s)");
        if (updates.Count > 0) summaryParts.Add($"{updates.Count} update(s)");
        if (moves.Count > 0) summaryParts.Add($"{moves.Count} move(s)");
        if (inserts.Count > 0) summaryParts.Add($"{inserts.Count} insert(s)");

        var summaryLabel = new Label
        {
            Text = $"Changes: {string.Join(", ", summaryParts)}",
            FontSize = 14,
            TextColor = Color.FromArgb("#AAAAAA")
        };
        Grid.SetRow(summaryLabel, 1);
        mainGrid.Children.Add(summaryLabel);

        // Commands list
        var scrollView = new ScrollView { VerticalScrollBarVisibility = ScrollBarVisibility.Always };
        var commandsList = new VerticalStackLayout { Spacing = 8 };

        // Add delete commands
        foreach (var idx in deletes)
        {
            string originalText = pointIndexMap.TryGetValue(idx, out var p) ? p.Text : "(unknown)";
            string originalCategory = pointIndexMap.TryGetValue(idx, out var pc) ? $"{pc.Category}" + (pc.Category == "active" ? $": {pc.Subcategory ?? "chronological"}" : "") : "";
            
            commandsList.Children.Add(CreateExpandableCommandCard(
                "DELETE", 
                $"Point #{idx}", 
                Color.FromArgb("#F44336"),
                new Dictionary<string, string>
                {
                    { "Current text", originalText },
                    { "Category", originalCategory }
                }));
        }

        // Add update commands
        foreach (var (idx, newText) in updates)
        {
            string originalText = pointIndexMap.TryGetValue(idx, out var p) ? p.Text : "(unknown)";
            
            commandsList.Children.Add(CreateExpandableCommandCard(
                "UPDATE", 
                $"Point #{idx}", 
                Color.FromArgb("#FF9800"),
                new Dictionary<string, string>
                {
                    { "Current", originalText },
                    { "New", newText }
                }));
        }

        // Add move commands
        foreach (var (idx, cat, subcat) in moves)
        {
            string originalCategory = pointIndexMap.TryGetValue(idx, out var pc) ? $"{pc.Category}" + (pc.Category == "active" ? $": {pc.Subcategory ?? "chronological"}" : "") : "";
            string targetCategory = string.IsNullOrEmpty(subcat) ? cat : $"{cat}: {subcat}";
            string originalText = pointIndexMap.TryGetValue(idx, out var p) ? p.Text : "(unknown)";
            
            commandsList.Children.Add(CreateExpandableCommandCard(
                "MOVE", 
                $"Point #{idx} → {targetCategory}", 
                Color.FromArgb("#9C27B0"),
                new Dictionary<string, string>
                {
                    { "Text", originalText },
                    { "From", originalCategory },
                    { "To", targetCategory }
                }));
        }

        // Add insert commands
        foreach (var (afterIdx, cat, subcat, text) in inserts)
        {
            string targetCategory = string.IsNullOrEmpty(subcat) ? cat : $"{cat}: {subcat}";
            string afterText = afterIdx > 0 && pointIndexMap.TryGetValue(afterIdx, out var p) ? p.Text : "(at start)";
            
            commandsList.Children.Add(CreateExpandableCommandCard(
                "INSERT", 
                $"After #{afterIdx} in {targetCategory}", 
                Color.FromArgb("#4CAF50"),
                new Dictionary<string, string>
                {
                    { "New text", text },
                    { "Insert after", afterIdx > 0 ? $"#{afterIdx}: {(afterText.Length > 60 ? afterText.Substring(0, 57) + "..." : afterText)}" : "(at start of section)" },
                    { "Category", targetCategory }
                }));
        }

        scrollView.Content = commandsList;
        Grid.SetRow(scrollView, 2);
        mainGrid.Children.Add(scrollView);

        // Version name input
        var nameRow = new HorizontalStackLayout { Spacing = 10 };
        nameRow.Children.Add(new Label
        {
            Text = "New version name:",
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        });
        var nameEntry = new Entry
        {
            Text = "Surgical Edit",
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#3E3E42"),
            WidthRequest = 300
        };
        nameRow.Children.Add(nameEntry);
        Grid.SetRow(nameRow, 3);
        mainGrid.Children.Add(nameRow);

        // Buttons
        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#4A4A4A"),
            TextColor = Colors.White,
            WidthRequest = 100,
            HeightRequest = 40
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult((null, null));
        };
        buttonRow.Children.Add(cancelBtn);

        var applyBtn = new Button
        {
            Text = "🚀 Create New Version",
            BackgroundColor = Color.FromArgb("#0E639C"),
            TextColor = Colors.White,
            WidthRequest = 180,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        };
        applyBtn.Clicked += (s, e) =>
        {
            string versionName = nameEntry.Text?.Trim() ?? "Surgical Edit";
            if (string.IsNullOrWhiteSpace(versionName)) versionName = "Surgical Edit";
            CloseOverlay(overlay);
            tcs.TrySetResult((true, versionName));
        };
        buttonRow.Children.Add(applyBtn);

        Grid.SetRow(buttonRow, 4);
        mainGrid.Children.Add(buttonRow);

        card.Content = mainGrid;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            pageGrid.Children.Add(overlay);
        }

        return await tcs.Task;
    }

    private Frame CreateExpandableCommandCard(string type, string summary, Color color, Dictionary<string, string> details)
    {
        var cmdCard = new Frame
        {
            BackgroundColor = Color.FromArgb("#1E1E1E"),
            BorderColor = color,
            CornerRadius = 8,
            Padding = new Thickness(12),
            HasShadow = false
        };

        var cmdLayout = new VerticalStackLayout { Spacing = 8 };

        // Header row
        var headerRow = new HorizontalStackLayout { Spacing = 12 };

        // Type label
        headerRow.Children.Add(new Label
        {
            Text = type,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = color,
            WidthRequest = 70,
            VerticalOptions = LayoutOptions.Center
        });

        // Summary
        headerRow.Children.Add(new Label
        {
            Text = summary,
            FontSize = 12,
            TextColor = Color.FromArgb("#AAAAAA"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            HorizontalOptions = LayoutOptions.FillAndExpand
        });

        // Expand indicator
        var expandLabel = new Label
        {
            Text = "▼",
            FontSize = 12,
            TextColor = Color.FromArgb("#888888"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        headerRow.Children.Add(expandLabel);

        cmdLayout.Children.Add(headerRow);

        // Details section (initially collapsed)
        var detailsSection = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = false,
            Padding = new Thickness(0, 8, 0, 0)
        };

        foreach (var kvp in details)
        {
            var detailRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(80)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };

            detailRow.Children.Add(new Label
            {
                Text = kvp.Key + ":",
                FontSize = 11,
                TextColor = Color.FromArgb("#888888"),
                FontAttributes = FontAttributes.Bold
            });

            var valueLabel = new Label
            {
                Text = kvp.Value,
                FontSize = 11,
                TextColor = Colors.White,
                LineBreakMode = LineBreakMode.WordWrap
            };
            Grid.SetColumn(valueLabel, 1);
            detailRow.Children.Add(valueLabel);

            detailsSection.Children.Add(detailRow);
        }

        cmdLayout.Children.Add(detailsSection);

        // Tap to expand/collapse
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            detailsSection.IsVisible = !detailsSection.IsVisible;
            expandLabel.Text = detailsSection.IsVisible ? "▲" : "▼";
        };
        cmdCard.GestureRecognizers.Add(tapGesture);

        cmdCard.Content = cmdLayout;
        return cmdCard;
    }

    // Parse delete[N]; commands
    private List<int> ParsePointsDeleteCommands(string input)
    {
        var result = new List<int>();
        var pattern = @"delete\[(\d+)\]\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int idx))
            {
                result.Add(idx);
            }
        }
        return result;
    }

    // Parse update[N] = "text"; commands
    private List<(int Index, string Text)> ParsePointsUpdateCommands(string input)
    {
        var result = new List<(int, string)>();
        var pattern = @"update\[(\d+)\]\s*=\s*""([^""]*)""\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int idx))
            {
                result.Add((idx, match.Groups[2].Value));
            }
        }
        return result;
    }

    // Parse insert[N] = new { category = "...", subcategory = "...", text = "..." }; commands
    private List<(int AfterIndex, string Category, string? Subcategory, string Text)> ParsePointsInsertCommands(string input)
    {
        var result = new List<(int, string, string?, string)>();
        var pattern = @"insert\[(\d+)\]\s*=\s*new\s*\{\s*category\s*=\s*""([^""]*)""\s*(?:,\s*subcategory\s*=\s*""([^""]*)"")?\s*,\s*text\s*=\s*""([^""]*)""\s*\}\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int idx))
            {
                string category = match.Groups[2].Value;
                string? subcategory = string.IsNullOrEmpty(match.Groups[3].Value) ? null : match.Groups[3].Value;
                string text = match.Groups[4].Value;
                result.Add((idx, category, subcategory, text));
            }
        }
        return result;
    }

    // Parse move[N] = new { category = "...", subcategory = "..." }; commands
    private List<(int Index, string Category, string? Subcategory)> ParsePointsMoveCommands(string input)
    {
        var result = new List<(int, string, string?)>();
        // Match with optional subcategory
        var pattern = @"move\[(\d+)\]\s*=\s*new\s*\{\s*category\s*=\s*""([^""]*)""\s*(?:,\s*subcategory\s*=\s*""([^""]*)"")?\s*\}\s*;";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int idx))
            {
                string category = match.Groups[2].Value;
                string? subcategory = string.IsNullOrEmpty(match.Groups[3].Value) ? null : match.Groups[3].Value;
                result.Add((idx, category, subcategory));
            }
        }
        return result;
    }
}
