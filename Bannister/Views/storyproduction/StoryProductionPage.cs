using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class StoryProductionPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly IdeasService? _ideasService;
    private readonly IdeaLoggerService? _ideaLogger;
    private readonly SubActivityService? _subActivityService;
    private FloatingChecklist? _checklist;
    
    private Picker _projectPicker;
    private Picker _draftPicker;
    private Label _draftLabel;
    private Label _currentDraftLabel;
    private Button _renameDraftBtn;
    private Button _setLatestBtn;
    private Button _deleteDraftBtn;
    private Button _compareToBtn;
    private Button _addProjectBtn;
    private VerticalStackLayout _linesContainer;
    private Label _statsLabel;
    private Label _projectionLabel;
    private Button _addLineBtn;
    private Button _addVisualBtn;
    private Button _insertLineBtn;
    private Button _deleteProjectBtn;
    private Button _expandAllBtn;
    private Button _exportPromptBtn;
    private Button _importDraftBtn;
    private Button _importVisualsBtn;
    private Button _storyPointsBtn;
    private Grid _loadingOverlay;
    private Label _loadingLabel;
    private bool _allExpanded = false;
    private List<(Label collapsed, Label expanded, Button expandBtn)> _expandableCards = new();
    private ScrollView _mainScrollView;
    
    private List<StoryProject> _projects = new();        // Original projects only
    private List<StoryProject> _drafts = new();          // Drafts of current project
    private StoryProject? _currentProject;
    private StoryProject? _compareToProject;             // Project being compared against
    private HashSet<int> _changedLineOrders = new();     // Line orders that differ from comparison

    public StoryProductionPage(AuthService auth, StoryProductionService storyService, IdeasService? ideasService = null, IdeaLoggerService? ideaLogger = null, SubActivityService? subActivityService = null)
    {
        _auth = auth;
        _storyService = storyService;
        _ideasService = ideasService;
        _ideaLogger = ideaLogger;
        _subActivityService = subActivityService;
        
        Title = "Story Production";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();

        // Attach floating process checklist
        if (_subActivityService != null)
        {
            _checklist = new FloatingChecklist(_auth, _subActivityService);
            _checklist.AttachTo(this);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProjectsAsync();
    }

    private void BuildUI()
    {
        // Main layout: fixed top section + scrollable cards
        var pageGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),   // header + project picker + buttons
                new RowDefinition(GridLength.Star)     // scrollable cards
            },
            Padding = 16,
            RowSpacing = 8
        };

        // === TOP SECTION (fixed, never scrolls) ===
        var topStack = new VerticalStackLayout { Spacing = 12 };

        // Header
        var headerStack = new HorizontalStackLayout { Spacing = 12 };
        
        headerStack.Children.Add(new Label
        {
            Text = "🎬",
            FontSize = 28,
            VerticalOptions = LayoutOptions.Center
        });
        
        headerStack.Children.Add(new Label
        {
            Text = "Story Production",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2"),
            VerticalOptions = LayoutOptions.Center
        });
        
        topStack.Children.Add(headerStack);

        // Project selection frame
        var projectFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var projectStack = new VerticalStackLayout { Spacing = 12 };
        
        projectStack.Children.Add(new Label
        {
            Text = "Select Project",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666")
        });

        var pickerRow = new HorizontalStackLayout { Spacing = 8 };
        
        _projectPicker = new Picker
        {
            Title = "Choose a project...",
            HorizontalOptions = LayoutOptions.FillAndExpand,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        _projectPicker.SelectedIndexChanged += OnProjectSelected;
        pickerRow.Children.Add(_projectPicker);
        
        _addProjectBtn = new Button
        {
            Text = "+ New",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(16, 8),
            FontSize = 14
        };
        _addProjectBtn.Clicked += OnAddProjectClicked;
        pickerRow.Children.Add(_addProjectBtn);

        _deleteProjectBtn = new Button
        {
            Text = "🗑️",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            FontSize = 14,
            IsVisible = false
        };
        _deleteProjectBtn.Clicked += OnDeleteProjectClicked;
        pickerRow.Children.Add(_deleteProjectBtn);
        
        projectStack.Children.Add(pickerRow);

        // Draft picker row
        _draftLabel = new Label
        {
            Text = "Draft Version",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666"),
            IsVisible = false
        };
        projectStack.Children.Add(_draftLabel);

        var draftRow = new HorizontalStackLayout { Spacing = 8 };
        
        _draftPicker = new Picker
        {
            Title = "Select draft...",
            HorizontalOptions = LayoutOptions.Start,
            WidthRequest = 150,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            IsVisible = false
        };
        _draftPicker.SelectedIndexChanged += OnDraftSelected;
        draftRow.Children.Add(_draftPicker);

        _renameDraftBtn = new Button
        {
            Text = "✏️",
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#E65100"),
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            FontSize = 14,
            IsVisible = false
        };
        _renameDraftBtn.Clicked += OnRenameDraftClicked;
        draftRow.Children.Add(_renameDraftBtn);

        _setLatestBtn = new Button
        {
            Text = "⭐ Set Latest",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#F57F17"),
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            FontSize = 12,
            IsVisible = false
        };
        _setLatestBtn.Clicked += OnSetLatestClicked;
        draftRow.Children.Add(_setLatestBtn);

        _deleteDraftBtn = new Button
        {
            Text = "🗑️",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            FontSize = 14,
            IsVisible = false
        };
        _deleteDraftBtn.Clicked += OnDeleteDraftClicked;
        draftRow.Children.Add(_deleteDraftBtn);

        _compareToBtn = new Button
        {
            Text = "🔀 Compare",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            FontSize = 12,
            IsVisible = false
        };
        _compareToBtn.Clicked += OnCompareToClicked;
        draftRow.Children.Add(_compareToBtn);
        
        projectStack.Children.Add(draftRow);

        // Current draft name display (shows full name + latest indicator + comparison info)
        _currentDraftLabel = new Label
        {
            Text = "",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2"),
            IsVisible = false
        };
        projectStack.Children.Add(_currentDraftLabel);

        // Stats label
        _statsLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            IsVisible = false
        };
        projectStack.Children.Add(_statsLabel);

        // Time projection label
        _projectionLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#1565C0"),
            IsVisible = false
        };
        projectStack.Children.Add(_projectionLabel);

        projectFrame.Content = projectStack;
        topStack.Children.Add(projectFrame);

        // Lines section header with action buttons
        var linesHeaderStack = new HorizontalStackLayout 
        { 
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };
        
        linesHeaderStack.Children.Add(new Label
        {
            Text = "Script Lines",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.StartAndExpand
        });
        
        // Hidden buttons for compatibility - we use them from the menu
        _addLineBtn = new Button { IsVisible = false };
        _addVisualBtn = new Button { IsVisible = false };

        // + Add menu button
        _insertLineBtn = new Button
        {
            Text = "+ Add",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsVisible = false
        };
        _insertLineBtn.Clicked += OnAddMenuClicked;
        linesHeaderStack.Children.Add(_insertLineBtn);

        _expandAllBtn = new Button
        {
            Text = "▶ Expand All",
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            TextColor = Color.FromArgb("#7B1FA2"),
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsVisible = false
        };
        _expandAllBtn.Clicked += OnExpandAllClicked;
        linesHeaderStack.Children.Add(_expandAllBtn);

        _exportPromptBtn = new Button
        {
            Text = "📋 Export Prompt",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsVisible = false
        };
        _exportPromptBtn.Clicked += OnExportMenuClicked;
        linesHeaderStack.Children.Add(_exportPromptBtn);

        _importDraftBtn = new Button
        {
            Text = "📥 Import Draft",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsVisible = false
        };
        _importDraftBtn.Clicked += OnImportDraftClicked;
        linesHeaderStack.Children.Add(_importDraftBtn);

        _importVisualsBtn = new Button
        {
            Text = "🖼️ Import Visuals",
            BackgroundColor = Color.FromArgb("#FCE4EC"),
            TextColor = Color.FromArgb("#C2185B"),
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsVisible = false
        };
        _importVisualsBtn.Clicked += OnImportVisualsClicked;
        linesHeaderStack.Children.Add(_importVisualsBtn);

        _storyPointsBtn = new Button
        {
            Text = "📝 Points",
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            TextColor = Color.FromArgb("#3F51B5"),
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsVisible = false
        };
        _storyPointsBtn.Clicked += OnStoryPointsClicked;
        linesHeaderStack.Children.Add(_storyPointsBtn);
        
        topStack.Children.Add(linesHeaderStack);

        Grid.SetRow(topStack, 0);
        pageGrid.Children.Add(topStack);

        // === BOTTOM SECTION (scrollable cards) ===
        _linesContainer = new VerticalStackLayout { Spacing = 8 };
        
        _mainScrollView = new ScrollView
        {
            Content = _linesContainer,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        // Info when no project selected
        _linesContainer.Children.Add(new Label
        {
            Text = "Select or create a project to start outlining your story.",
            FontSize = 14,
            TextColor = Color.FromArgb("#999"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0)
        });

        Grid.SetRow(_mainScrollView, 1);
        pageGrid.Children.Add(_mainScrollView);

        // Loading overlay
        _loadingOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var loadingStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 12
        };

        loadingStack.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            WidthRequest = 40,
            HeightRequest = 40
        });

        _loadingLabel = new Label
        {
            Text = "Importing...",
            TextColor = Colors.White,
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center
        };
        loadingStack.Children.Add(_loadingLabel);

        _loadingOverlay.Children.Add(loadingStack);
        Grid.SetRowSpan(_loadingOverlay, 2);
        pageGrid.Children.Add(_loadingOverlay);

        Content = pageGrid;
    }

    private async Task LoadProjectsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[STORY] LoadProjectsAsync START");
        _loadingLabel.Text = "Loading projects...";
        _loadingOverlay.IsVisible = true;
        
        try
        {
            System.Diagnostics.Debug.WriteLine("[STORY] Fetching all projects...");
            
            // Get ALL projects (including completed/published) - not just active
            var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
            
            // Filter to original projects only (not drafts), but include completed ones
            _projects = allProjects.Where(p => p.ParentProjectId == null).ToList();
            System.Diagnostics.Debug.WriteLine($"[STORY] Found {_projects.Count} original projects");
            
            _projectPicker.Items.Clear();
            foreach (var project in _projects)
            {
                // Show status indicator for completed/published projects
                string name = project.Name;
                if (project.IsPublished) name += " ✓";
                else if (project.Status == "completed") name += " (done)";
                _projectPicker.Items.Add(name);
            }
            
            // Restore previous selection, or fall back to last used project
            int targetId = _currentProject?.Id ?? 
                Preferences.Get($"StoryProd_LastProject_{_auth.CurrentUsername}", -1);
            System.Diagnostics.Debug.WriteLine($"[STORY] Target project ID: {targetId}");

            // If target was a draft, find its parent
            if (targetId > 0)
            {
                var targetProject = allProjects.FirstOrDefault(p => p.Id == targetId);
                System.Diagnostics.Debug.WriteLine($"[STORY] Target project found: {targetProject?.Name ?? "NULL"}");
                
                if (targetProject?.ParentProjectId != null)
                {
                    targetId = targetProject.ParentProjectId.Value;
                    System.Diagnostics.Debug.WriteLine($"[STORY] Using parent ID: {targetId}");
                }
                
                var index = _projects.FindIndex(p => p.Id == targetId);
                System.Diagnostics.Debug.WriteLine($"[STORY] Index in picker: {index}");
                
                if (index >= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[STORY] Setting picker index to {index}, this will trigger OnProjectSelected");
                    _projectPicker.SelectedIndex = index;
                    System.Diagnostics.Debug.WriteLine("[STORY] LoadProjectsAsync returning (OnProjectSelected will hide overlay)");
                    return; // OnProjectSelected will hide the overlay
                }
            }
            
            // No project to select - hide overlay now
            System.Diagnostics.Debug.WriteLine("[STORY] No project to select, hiding overlay");
            _loadingOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STORY] LoadProjects ERROR: {ex.Message}\n{ex.StackTrace}");
            _loadingOverlay.IsVisible = false;
        }
        System.Diagnostics.Debug.WriteLine("[STORY] LoadProjectsAsync END");
    }

    private async void OnProjectSelected(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[STORY] OnProjectSelected START - SelectedIndex: {_projectPicker.SelectedIndex}");
        
        if (_projectPicker.SelectedIndex < 0 || _projectPicker.SelectedIndex >= _projects.Count)
        {
            System.Diagnostics.Debug.WriteLine("[STORY] Invalid index, hiding controls");
            _currentProject = null;
            HideProjectControls();
            _loadingOverlay.IsVisible = false;
            return;
        }
        
        _loadingLabel.Text = "Loading draft...";
        _loadingOverlay.IsVisible = true;
        
        try
        {
            var selectedProject = _projects[_projectPicker.SelectedIndex];
            System.Diagnostics.Debug.WriteLine($"[STORY] Selected project: {selectedProject.Name} (ID: {selectedProject.Id})");
            
            // Load drafts for this project
            System.Diagnostics.Debug.WriteLine("[STORY] Loading drafts...");
            await LoadDraftsAsync(selectedProject.Id);
            System.Diagnostics.Debug.WriteLine($"[STORY] Drafts loaded, _currentProject: {_currentProject?.Name ?? "NULL"}");
            
            // LoadDraftsAsync will select latest and set _currentProject
            ShowProjectControls();

            // Remember last selected project
            if (_currentProject != null)
                Preferences.Set($"StoryProd_LastProject_{_auth.CurrentUsername}", _currentProject.Id);
            
            _loadingLabel.Text = "Loading lines...";
            System.Diagnostics.Debug.WriteLine("[STORY] Loading lines...");
            await LoadLinesAsync();
            System.Diagnostics.Debug.WriteLine("[STORY] Lines loaded");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STORY] OnProjectSelected ERROR: {ex.Message}\n{ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to load project: {ex.Message}", "OK");
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine("[STORY] OnProjectSelected hiding overlay");
            _loadingOverlay.IsVisible = false;
        }
        System.Diagnostics.Debug.WriteLine("[STORY] OnProjectSelected END");
    }

    private async Task LoadDraftsAsync(int projectId, int? selectDraftId = null)
    {
        System.Diagnostics.Debug.WriteLine($"[STORY] LoadDraftsAsync START - projectId: {projectId}, selectDraftId: {selectDraftId}");
        
        _drafts = await _storyService.GetProjectDraftsAsync(projectId);
        System.Diagnostics.Debug.WriteLine($"[STORY] Found {_drafts.Count} drafts");
        
        _draftPicker.Items.Clear();
        foreach (var draft in _drafts)
        {
            string label;
            if (draft.DraftVersion == 1)
            {
                label = "Original" + (draft.IsLatest ? " ⭐" : "");
            }
            else
            {
                label = draft.Name + (draft.IsLatest ? " ⭐" : "");
            }
            _draftPicker.Items.Add(label);
        }
        
        // Show draft picker only if there are multiple versions
        bool showDrafts = _drafts.Count > 1;
        _draftLabel.IsVisible = showDrafts;
        _draftPicker.IsVisible = showDrafts;
        
        // Determine which draft to select
        int selectIndex = 0;
        if (selectDraftId.HasValue)
        {
            // Select specific draft
            selectIndex = _drafts.FindIndex(d => d.Id == selectDraftId.Value);
            if (selectIndex < 0) selectIndex = 0;
        }
        else
        {
            // Select the latest draft by default
            selectIndex = _drafts.FindIndex(d => d.IsLatest);
            if (selectIndex < 0) selectIndex = 0; // Fall back to original
        }
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Selecting draft index: {selectIndex}");
        
        if (_drafts.Count > 0)
        {
            _draftPicker.SelectedIndex = selectIndex;
            _currentProject = _drafts[selectIndex];
            System.Diagnostics.Debug.WriteLine($"[STORY] Set _currentProject to: {_currentProject.Name} (ID: {_currentProject.Id})");
            UpdateCurrentDraftDisplay();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[STORY] WARNING: No drafts found!");
        }
        
        System.Diagnostics.Debug.WriteLine("[STORY] LoadDraftsAsync END");
    }

    private void UpdateCurrentDraftDisplay()
    {
        if (_currentProject == null)
        {
            _currentDraftLabel.IsVisible = false;
            _renameDraftBtn.IsVisible = false;
            _setLatestBtn.IsVisible = false;
            _deleteDraftBtn.IsVisible = false;
            _compareToBtn.IsVisible = false;
            return;
        }

        // Show full draft name with latest indicator
        string displayName = _currentProject.DraftVersion == 1 
            ? "Original" 
            : _currentProject.Name;
        
        if (_currentProject.IsLatest)
            displayName += " ⭐ (Latest)";

        // Add comparison info
        if (_compareToProject != null)
        {
            string compareName = _compareToProject.DraftVersion == 1 
                ? "Original" 
                : _compareToProject.Name;
            displayName += $"\n🔀 Comparing to: {compareName}";
            if (_changedLineOrders.Count > 0)
                displayName += $" ({_changedLineOrders.Count} changes)";
        }
        
        _currentDraftLabel.Text = $"📄 {displayName}";
        _currentDraftLabel.IsVisible = _drafts.Count > 1;
        
        // Show rename and delete only for non-original drafts
        bool isDraft = _currentProject.DraftVersion > 1;
        _renameDraftBtn.IsVisible = isDraft;
        _deleteDraftBtn.IsVisible = isDraft;
        
        // Show "Set Latest" only if this isn't already latest and there are drafts
        _setLatestBtn.IsVisible = _drafts.Count > 1 && !_currentProject.IsLatest;
        
        // Show Compare button when there are multiple drafts (can compare any version)
        _compareToBtn.IsVisible = _drafts.Count > 1;
    }

    private async void OnDraftSelected(object? sender, EventArgs e)
    {
        if (_draftPicker.SelectedIndex < 0 || _draftPicker.SelectedIndex >= _drafts.Count)
            return;
        
        _currentProject = _drafts[_draftPicker.SelectedIndex];
        UpdateCurrentDraftDisplay();
        
        // Remember selected draft
        Preferences.Set($"StoryProd_LastProject_{_auth.CurrentUsername}", _currentProject.Id);
        
        _loadingLabel.Text = "Loading lines...";
        _loadingOverlay.IsVisible = true;
        
        try
        {
            await LoadLinesAsync();
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    private async void OnSetLatestClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        await _storyService.SetAsLatestAsync(_currentProject.Id);
        
        // Reload to show updated star indicators
        var parentId = _currentProject.ParentProjectId ?? _currentProject.Id;
        await LoadDraftsAsync(parentId, _currentProject.Id);
    }

    private async void OnRenameDraftClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null || _currentProject.DraftVersion <= 1) return;

        string newName = await DisplayPromptAsync(
            "Rename Draft",
            "Enter a new name for this draft:",
            accept: "Save",
            cancel: "Cancel",
            initialValue: _currentProject.Name,
            placeholder: "Draft name");

        if (string.IsNullOrWhiteSpace(newName)) return;

        await _storyService.RenameDraftAsync(_currentProject.Id, newName.Trim());
        
        // Reload drafts to show updated name
        var parentId = _currentProject.ParentProjectId ?? _currentProject.Id;
        await LoadDraftsAsync(parentId, _currentProject.Id);
    }

    private async void OnDeleteDraftClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null || _currentProject.DraftVersion <= 1) return;

        bool confirm = await DisplayAlert(
            "Delete Draft?",
            $"Are you sure you want to delete \"{_currentProject.Name}\"?\n\nThis will permanently delete this draft and all its lines.",
            "Delete",
            "Cancel");

        if (!confirm) return;

        var parentId = _currentProject.ParentProjectId ?? _currentProject.Id;
        
        // Delete the draft
        await _storyService.DeleteDraftAsync(_currentProject.Id);
        
        // Reload drafts - will select latest or original
        await LoadDraftsAsync(parentId);
        await LoadLinesAsync();
    }

    private async void OnCompareToClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null || _drafts.Count < 2) return;

        // Build options list
        var options = new List<string>();
        
        // First option: Auto (previous version)
        options.Add("🔄 Auto (previous version)");
        
        // Add all other drafts as options
        foreach (var draft in _drafts)
        {
            if (draft.Id == _currentProject.Id) continue; // Skip current
            
            string name = draft.DraftVersion == 1 ? "Original" : draft.Name;
            options.Add(name);
        }

        string result = await DisplayActionSheet(
            "Compare to which version?",
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        if (result == "🔄 Auto (previous version)")
        {
            // Clear manual override
            await _storyService.SetCompareToAsync(_currentProject.Id, null);
        }
        else
        {
            // Find the selected draft
            var selectedDraft = _drafts.FirstOrDefault(d => 
                (d.DraftVersion == 1 ? "Original" : d.Name) == result && 
                d.Id != _currentProject.Id);
            
            if (selectedDraft != null)
            {
                await _storyService.SetCompareToAsync(_currentProject.Id, selectedDraft.Id);
            }
        }

        // Reload to show updated comparison
        await LoadLinesAsync();
    }

    private async void OnImportVisualsClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null || _drafts.Count < 2) return;

        // Build options list of other drafts that have prepared visuals
        var options = new List<string>();
        var optionDrafts = new List<StoryProject>();
        
        foreach (var draft in _drafts)
        {
            if (draft.Id == _currentProject.Id) continue; // Skip current
            
            // Check if this draft has any prepared visuals
            var (total, prepared) = await _storyService.GetProjectStatsAsync(draft.Id);
            if (prepared > 0)
            {
                string name = draft.DraftVersion == 1 ? "Original" : draft.Name;
                options.Add($"{name} ({prepared} visuals)");
                optionDrafts.Add(draft);
            }
        }

        if (options.Count == 0)
        {
            await DisplayAlert("No Visuals", "No other drafts have prepared visuals to import.", "OK");
            return;
        }

        string result = await DisplayActionSheet(
            "Import visuals from which draft?",
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(result) || result == "Cancel") return;

        // Find selected draft
        int selectedIndex = options.IndexOf(result);
        if (selectedIndex < 0 || selectedIndex >= optionDrafts.Count) return;
        
        var sourceDraft = optionDrafts[selectedIndex];

        _loadingLabel.Text = "Importing visuals...";
        _loadingOverlay.IsVisible = true;

        int imported = await _storyService.ImportVisualsFromDraftAsync(_currentProject.Id, sourceDraft.Id);

        _loadingOverlay.IsVisible = false;

        if (imported > 0)
        {
            await DisplayAlert("Visuals Imported", 
                $"Imported {imported} visual(s) from matching lines.", "OK");
            await LoadLinesAsync();
        }
        else
        {
            await DisplayAlert("No Matches", 
                "No matching lines found with prepared visuals.\n\nVisuals are imported for lines that have the same script text.", "OK");
        }
    }

    private async void OnStoryPointsClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        // Get the original project (parent) for story points
        var originalProject = _currentProject;
        if (_currentProject.ParentProjectId != null)
        {
            originalProject = await _storyService.GetProjectByIdAsync(_currentProject.ParentProjectId.Value);
            if (originalProject == null) originalProject = _currentProject;
        }

        var page = new StoryPointsPage(_auth, _storyService, originalProject, _ideasService, _ideaLogger);
        await Navigation.PushAsync(page);
    }

    private void HideProjectControls()
    {
        _insertLineBtn.IsVisible = false;
        _deleteProjectBtn.IsVisible = false;
        _expandAllBtn.IsVisible = false;
        _exportPromptBtn.IsVisible = false;
        _importDraftBtn.IsVisible = false;
        _importVisualsBtn.IsVisible = false;
        _storyPointsBtn.IsVisible = false;
        _statsLabel.IsVisible = false;
        _projectionLabel.IsVisible = false;
        _draftLabel.IsVisible = false;
        _draftPicker.IsVisible = false;
        _renameDraftBtn.IsVisible = false;
        _setLatestBtn.IsVisible = false;
        _deleteDraftBtn.IsVisible = false;
        _compareToBtn.IsVisible = false;
        _currentDraftLabel.IsVisible = false;
    }

    private void ShowProjectControls()
    {
        _insertLineBtn.IsVisible = true;
        _deleteProjectBtn.IsVisible = true;
        _expandAllBtn.IsVisible = true;
        _exportPromptBtn.IsVisible = true;
        _importDraftBtn.IsVisible = true;
        _importVisualsBtn.IsVisible = _drafts.Count > 1; // Only show if there are other drafts to import from
        _storyPointsBtn.IsVisible = true;
    }

    private async Task LoadLinesAsync(bool preserveScroll = false)
    {
        System.Diagnostics.Debug.WriteLine($"[STORY] LoadLinesAsync START - preserveScroll: {preserveScroll}");
        
        if (_currentProject == null) 
        {
            System.Diagnostics.Debug.WriteLine("[STORY] LoadLinesAsync - no current project, returning");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Loading lines for project: {_currentProject.Name} (ID: {_currentProject.Id})");

        // Save scroll position before rebuilding
        double savedScrollY = 0;
        if (preserveScroll && _mainScrollView != null)
        {
            savedScrollY = _mainScrollView.ScrollY;
        }

        _expandableCards.Clear();
        _allExpanded = false;
        _expandAllBtn.Text = "▶ Expand All";
        
        var lines = await _storyService.GetLinesAsync(_currentProject.Id);
        var (totalLines, preparedLines) = await _storyService.GetProjectStatsAsync(_currentProject.Id);

        // Load comparison data
        _compareToProject = await _storyService.GetComparisonProjectAsync(_currentProject);
        _changedLineOrders.Clear();
        
        if (_compareToProject != null)
        {
            _changedLineOrders = await _storyService.GetChangedLineOrdersAsync(
                _currentProject.Id, _compareToProject.Id);
        }
        
        // Update the display to show comparison info
        UpdateCurrentDraftDisplay();
        
        // Update stats
        if (totalLines > 0)
        {
            int percent = (int)(preparedLines * 100.0 / totalLines);
            _statsLabel.Text = $"📊 {preparedLines}/{totalLines} visuals prepared ({percent}%)";
            _statsLabel.IsVisible = true;
            
            // Update time projection (don't let this fail the whole load)
            try
            {
                await UpdateTimeProjectionAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STORY] Projection error: {ex.Message}");
                _projectionLabel.IsVisible = false;
            }
        }
        else
        {
            _statsLabel.Text = "No lines yet. Add your first line below.";
            _statsLabel.IsVisible = true;
            _projectionLabel.IsVisible = false;
        }

        // Build all new cards first before touching the UI
        var newCards = new List<View>();
        
        if (lines.Count == 0)
        {
            newCards.Add(new Label
            {
                Text = "No lines yet.\nTap '+ Add Line' to add script lines.",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
        }
        else
        {
            foreach (var line in lines)
            {
                bool isChanged = _changedLineOrders.Contains(line.LineOrder);
                newCards.Add(CreateLineCard(line, lines.Count, isChanged));
            }
        }

        // Swap all at once — clear and add in one batch
        _linesContainer.Children.Clear();
        foreach (var card in newCards)
        {
            _linesContainer.Children.Add(card);
        }

        // Restore scroll position after layout
        if (preserveScroll && savedScrollY > 0 && _mainScrollView != null)
        {
            Dispatcher.Dispatch(async () =>
            {
                await Task.Delay(50);
                await _mainScrollView.ScrollToAsync(0, savedScrollY, false);
            });
        }
        
        System.Diagnostics.Debug.WriteLine("[STORY] LoadLinesAsync END");
    }

    // ============================================================
    // TWO CARDS PER ROW: Script card (left) | Visual card (right)
    //
    // ┌──────────────────────────┐  ┌──────────────────────────┐
    // │ [#1] Script text...   ✏️│  │ 🎨 Visual desc...     ✏️│
    // │      ▶ (expand)         │  │ 📁 asset.mp4            │
    // │ [▲][▼][🗑️]             │  │              ☑ ✓ Done   │
    // └──────────────────────────┘  └──────────────────────────┘
    // ============================================================
    private View CreateLineCard(StoryLine line, int totalLines, bool isChanged = false)
    {
        bool isPrepared = line.VisualPrepared;

        // Changed highlight color (yellow/orange tint)
        Color changedBgColor = Color.FromArgb("#FFF8E1");
        Color changedBorderColor = Color.FromArgb("#FFB300");

        var rowGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(120))
            },
            ColumnSpacing = 10,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // ========================
        // LEFT CARD: Script
        // ========================
        Color scriptBgColor = line.IsSilent ? Color.FromArgb("#F0F0F0") : Colors.White;
        Color scriptBorderColor = isPrepared ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
        
        // Apply changed highlight
        if (isChanged)
        {
            scriptBgColor = changedBgColor;
            scriptBorderColor = changedBorderColor;
        }

        var scriptFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = scriptBgColor,
            HasShadow = !line.IsSilent && !isChanged,
            BorderColor = scriptBorderColor
        };

        var scriptStack = new VerticalStackLayout { Spacing = 8 };

        // Badge + text row
        var scriptHeaderRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        // Badge
        var badge = new Frame
        {
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            CornerRadius = 14,
            Padding = new Thickness(8, 4),
            HasShadow = false,
            BorderColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.Start
        };
        badge.Content = new Label
        {
            Text = $"#{line.LineOrder}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(badge, 0);
        scriptHeaderRow.Children.Add(badge);

        // Script text: collapsed by default (single line), expandable via button
        bool isSilent = line.IsSilent;
        string scriptDisplayText;
        bool scriptEmpty;
        
        if (isSilent)
        {
            scriptDisplayText = "— silent —";
            scriptEmpty = true;
        }
        else
        {
            scriptDisplayText = string.IsNullOrEmpty(line.LineText) ? "(tap to add text)" : line.LineText;
            scriptEmpty = string.IsNullOrEmpty(line.LineText);
        }
        
        bool textNeedsExpand = !isSilent && !scriptEmpty &&
            (line.LineText.Contains('\n') || line.LineText.Contains('\r') || line.LineText.Length > 40);

        var collapsedScriptLabel = new Label
        {
            Text = scriptDisplayText,
            FontSize = 14,
            TextColor = isSilent ? Color.FromArgb("#BBB") : (scriptEmpty ? Color.FromArgb("#999") : Color.FromArgb("#333")),
            FontAttributes = isSilent ? FontAttributes.Italic : FontAttributes.None,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            IsVisible = true
        };
        if (!isSilent)
        {
            var collapsedScriptTap = new TapGestureRecognizer();
            collapsedScriptTap.Tapped += async (s, e) => await EditLineTextAsync(line);
            collapsedScriptLabel.GestureRecognizers.Add(collapsedScriptTap);
        }

        var expandedScriptLabel = new Label
        {
            Text = scriptDisplayText,
            FontSize = 14,
            TextColor = isSilent ? Color.FromArgb("#BBB") : (scriptEmpty ? Color.FromArgb("#999") : Color.FromArgb("#333")),
            FontAttributes = isSilent ? FontAttributes.Italic : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap,
            IsVisible = false
        };
        if (!isSilent)
        {
            var expandedScriptTap = new TapGestureRecognizer();
            expandedScriptTap.Tapped += async (s, e) => await EditLineTextAsync(line);
            expandedScriptLabel.GestureRecognizers.Add(expandedScriptTap);
        }

        var scriptTextContainer = new VerticalStackLayout();
        scriptTextContainer.Children.Add(collapsedScriptLabel);
        scriptTextContainer.Children.Add(expandedScriptLabel);

        Grid.SetColumn(scriptTextContainer, 1);
        scriptHeaderRow.Children.Add(scriptTextContainer);

        // Edit icon
        var editScriptBtn = new Label
        {
            Text = "✏️",
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        var editScriptTap = new TapGestureRecognizer();
        editScriptTap.Tapped += async (s, e) => await EditLineTextAsync(line);
        editScriptBtn.GestureRecognizers.Add(editScriptTap);
        Grid.SetColumn(editScriptBtn, 2);
        scriptHeaderRow.Children.Add(editScriptBtn);

        scriptStack.Children.Add(scriptHeaderRow);

        // Reorder + delete buttons
        var btnStack = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        if (line.LineOrder > 1)
        {
            var upBtn = new Button
            {
                Text = "▲",
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#333"),
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                FontSize = 12
            };
            upBtn.Clicked += async (s, e) =>
            {
                await _storyService.MoveLineUpAsync(line.Id);
                await LoadLinesAsync(preserveScroll: true);
            };
            btnStack.Children.Add(upBtn);
        }

        if (line.LineOrder < totalLines)
        {
            var downBtn = new Button
            {
                Text = "▼",
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#333"),
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                FontSize = 12
            };
            downBtn.Clicked += async (s, e) =>
            {
                await _storyService.MoveLineDownAsync(line.Id);
                await LoadLinesAsync(preserveScroll: true);
            };
            btnStack.Children.Add(downBtn);
        }

        var deleteBtn = new Button
        {
            Text = "🗑️",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = 0,
            FontSize = 12
        };
        deleteBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Delete Line",
                $"Delete line #{line.LineOrder}?", "Delete", "Cancel");
            if (!confirm) return;
            await _storyService.DeleteLineAsync(line.Id);
            await LoadLinesAsync(preserveScroll: true);
        };
        btnStack.Children.Add(deleteBtn);

        // Silent toggle button (visual-only, no narration)
        var silentBtn = new Button
        {
            Text = line.IsSilent ? "🔇" : "🔈",
            BackgroundColor = line.IsSilent ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#333"),
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = 0,
            FontSize = 12
        };
        silentBtn.Clicked += async (s, e) =>
        {
            line.IsSilent = !line.IsSilent;
            await _storyService.UpdateLineAsync(line);
            await LoadLinesAsync(preserveScroll: true);
        };
        btnStack.Children.Add(silentBtn);

        // Compare button - only show for changed lines when comparing
        if (isChanged && _compareToProject != null)
        {
            var compareLineBtn = new Button
            {
                Text = "🔍",
                BackgroundColor = Color.FromArgb("#FFF8E1"),
                TextColor = Color.FromArgb("#F57F17"),
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                FontSize = 12
            };
            compareLineBtn.Clicked += async (s, e) =>
            {
                await ShowLineComparisonAsync(line.LineOrder);
            };
            btnStack.Children.Add(compareLineBtn);
        }

        // Show expand button only if text likely overflows one line
        if (textNeedsExpand)
        {
            var expandBtn = new Button
            {
                Text = "▶",
                BackgroundColor = Color.FromArgb("#E8EAF6"),
                TextColor = Color.FromArgb("#7B1FA2"),
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                FontSize = 12
            };
            var capturedCollapsed = collapsedScriptLabel;
            var capturedExpanded = expandedScriptLabel;
            expandBtn.Clicked += (s, e) =>
            {
                bool isExpanding = !capturedExpanded.IsVisible;
                capturedExpanded.IsVisible = isExpanding;
                capturedCollapsed.IsVisible = !isExpanding;
                expandBtn.Text = isExpanding ? "▼" : "▶";
            };
            btnStack.Children.Add(expandBtn);
            _expandableCards.Add((collapsedScriptLabel, expandedScriptLabel, expandBtn));
        }

        scriptStack.Children.Add(btnStack);
        scriptFrame.Content = scriptStack;

        Grid.SetColumn(scriptFrame, 0);
        rowGrid.Children.Add(scriptFrame);

        // ========================
        // RIGHT CARD: Visual
        // ========================
        Color visualBgColor = Colors.White;
        Color visualBorderColor = isPrepared ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
        
        // Apply changed highlight
        if (isChanged)
        {
            visualBgColor = changedBgColor;
            visualBorderColor = changedBorderColor;
        }

        var visualFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = visualBgColor,
            HasShadow = !isChanged,
            BorderColor = visualBorderColor
        };

        var visualStack = new VerticalStackLayout { Spacing = 8 };

        // Visual header + text
        var visualHeaderRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var visualEmoji = new Label
        {
            Text = "🎨",
            FontSize = 16,
            VerticalOptions = LayoutOptions.Start
        };
        Grid.SetColumn(visualEmoji, 0);
        visualHeaderRow.Children.Add(visualEmoji);

        var visualLabel = new Label
        {
            Text = string.IsNullOrEmpty(line.VisualDescription) ? "(tap to describe visual)" : line.VisualDescription,
            FontSize = 13,
            TextColor = string.IsNullOrEmpty(line.VisualDescription) ? Color.FromArgb("#999") : Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };
        var visualTap = new TapGestureRecognizer();
        visualTap.Tapped += async (s, e) => await EditVisualDescriptionAsync(line);
        visualLabel.GestureRecognizers.Add(visualTap);
        Grid.SetColumn(visualLabel, 1);
        visualHeaderRow.Children.Add(visualLabel);

        var editVisualBtn = new Label
        {
            Text = "✏️",
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        var editVisualTap = new TapGestureRecognizer();
        editVisualTap.Tapped += async (s, e) => await EditVisualDescriptionAsync(line);
        editVisualBtn.GestureRecognizers.Add(editVisualTap);
        Grid.SetColumn(editVisualBtn, 2);
        visualHeaderRow.Children.Add(editVisualBtn);

        visualStack.Children.Add(visualHeaderRow);

        // Asset path if set
        if (!string.IsNullOrEmpty(line.VisualAssetPath))
        {
            visualStack.Children.Add(new Label
            {
                Text = $"📁 {Path.GetFileName(line.VisualAssetPath)}",
                FontSize = 11,
                TextColor = Color.FromArgb("#2196F3"),
                Margin = new Thickness(24, 0, 0, 0)
            });
        }

        // Spacer to push checkbox to bottom
        visualStack.Children.Add(new BoxView
        {
            HeightRequest = 0,
            Color = Colors.Transparent,
            VerticalOptions = LayoutOptions.FillAndExpand
        });

        // Ready checkbox at bottom-right
        var readyStack = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.End
        };

        var readyCheckbox = new CheckBox
        {
            IsChecked = isPrepared,
            Color = Color.FromArgb("#4CAF50")
        };
        readyCheckbox.CheckedChanged += async (s, e) =>
        {
            if (readyCheckbox.IsChecked && !isPrepared)
            {
                // Just checked - prompt for time
                await PromptAndLogTaskTimeAsync(line, 0, "visual_complete", $"Line {line.LineOrder}: Complete visual");
            }
            await _storyService.ToggleVisualPreparedAsync(line.Id);
            await LoadLinesAsync(preserveScroll: true);
        };
        readyStack.Children.Add(readyCheckbox);

        readyStack.Children.Add(new Label
        {
            Text = isPrepared ? "✓ Done" : "Ready",
            FontSize = 13,
            FontAttributes = isPrepared ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isPrepared ? Color.FromArgb("#4CAF50") : Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        });

        visualStack.Children.Add(readyStack);

        // Shots/Prompts button row
        var shotsRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        
        // Show shot count if broken down
        if (line.ShotCount > 0)
        {
            var shots = _storyService.GetShots(line);
            int doneCount = shots.Count(s => s.Done);
            shotsRow.Children.Add(new Label
            {
                Text = $"📸 {doneCount}/{line.ShotCount} shots",
                FontSize = 12,
                TextColor = doneCount == line.ShotCount ? Color.FromArgb("#4CAF50") : Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.Center
            });
        }
        else if (line.HasPrompts)
        {
            shotsRow.Children.Add(new Label
            {
                Text = "📝 Has prompts",
                FontSize = 12,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.Center
            });
        }

        var editShotsBtn = new Button
        {
            Text = line.ShotCount > 0 ? "✏️ Edit" : "📸 Break Down",
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#E65100"),
            CornerRadius = 6,
            Padding = new Thickness(8, 4),
            FontSize = 11,
            HeightRequest = 28
        };
        var capturedLine = line;
        editShotsBtn.Clicked += async (s, e) =>
        {
            var shotBreakdownPage = new ShotBreakdownPage(_storyService, capturedLine);
            shotBreakdownPage.Disappearing += async (sender, args) => await LoadLinesAsync(preserveScroll: true);
            await Navigation.PushAsync(shotBreakdownPage);
        };
        shotsRow.Children.Add(editShotsBtn);

        visualStack.Children.Add(shotsRow);
        visualFrame.Content = visualStack;

        Grid.SetColumn(visualFrame, 1);
        rowGrid.Children.Add(visualFrame);

        // ========================
        // THIRD CARD: Image thumbnail
        // ========================
        var imageFrame = new Frame
        {
            Padding = 4,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = isPrepared ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            WidthRequest = 120,
            HeightRequest = 120
        };

        var imageStack = new Grid();

        string assetPath = line.VisualAssetPath ?? "";
        bool isVideo = assetPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        string thumbnailPath = isVideo ? assetPath + ".thumb.png" : assetPath;
        bool hasAsset = !string.IsNullOrEmpty(assetPath) && File.Exists(assetPath);
        bool hasThumbnail = hasAsset && (isVideo ? File.Exists(thumbnailPath) : true);

        if (hasAsset && hasThumbnail)
        {
            var img = new Image
            {
                Source = ImageSource.FromFile(isVideo ? thumbnailPath : assetPath),
                Aspect = Aspect.AspectFill,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };
            imageStack.Children.Add(img);

            // Video badge
            if (isVideo)
            {
                var videoBadge = new Label
                {
                    Text = "🎬",
                    FontSize = 16,
                    BackgroundColor = Color.FromArgb("#CC000000"),
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    WidthRequest = 28,
                    HeightRequest = 28,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(2, 0, 0, 2)
                };
                imageStack.Children.Add(videoBadge);
            }

            // Small X to remove
            var removeBtn = new Label
            {
                Text = "✕",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                BackgroundColor = Color.FromArgb("#CC000000"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                WidthRequest = 24,
                HeightRequest = 24,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 2, 2, 0)
            };
            var removeTap = new TapGestureRecognizer();
            removeTap.Tapped += async (s, e) =>
            {
                // Clean up video thumbnail if exists
                string thumbFile = line.VisualAssetPath + ".thumb.png";
                if (File.Exists(thumbFile))
                {
                    try { File.Delete(thumbFile); } catch { }
                }
                line.VisualAssetPath = "";
                await _storyService.UpdateLineAsync(line);
                await LoadLinesAsync(preserveScroll: true);
            };
            removeBtn.GestureRecognizers.Add(removeTap);
            imageStack.Children.Add(removeBtn);
        }
        else
        {
            // Placeholder: tap to pick image
            var placeholder = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 4
            };
            placeholder.Children.Add(new Label
            {
                Text = "🖼️",
                FontSize = 28,
                HorizontalTextAlignment = TextAlignment.Center
            });
            placeholder.Children.Add(new Label
            {
                Text = "tap to add",
                FontSize = 10,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center
            });
            imageStack.Children.Add(placeholder);
        }

        // Tap to pick image
        var pickImageTap = new TapGestureRecognizer();
        pickImageTap.Tapped += async (s, e) => await PickImageForLineAsync(line);
        imageStack.GestureRecognizers.Add(pickImageTap);

        imageFrame.Content = imageStack;

        Grid.SetColumn(imageFrame, 2);
        rowGrid.Children.Add(imageFrame);

        return rowGrid;
    }

    private void OnExpandAllClicked(object? sender, EventArgs e)
    {
        _allExpanded = !_allExpanded;

        foreach (var (collapsed, expanded, btn) in _expandableCards)
        {
            collapsed.IsVisible = !_allExpanded;
            expanded.IsVisible = _allExpanded;
            btn.Text = _allExpanded ? "▼" : "▶";
        }

        _expandAllBtn.Text = _allExpanded ? "▼ Collapse All" : "▶ Expand All";
    }

    private async void OnAddProjectClicked(object? sender, EventArgs e)
    {
        string name = await DisplayPromptAsync(
            "New Project",
            "Enter project name:",
            placeholder: "e.g., AI Revolution Episode 5");
        
        if (string.IsNullOrWhiteSpace(name)) return;
        
        string description = await DisplayPromptAsync(
            "Project Description",
            "Optional description:",
            placeholder: "Brief description of this story...",
            initialValue: "");
        
        var project = await _storyService.CreateProjectAsync(
            _auth.CurrentUsername, 
            name.Trim(), 
            description?.Trim() ?? "");
        
        await LoadProjectsAsync();
        
        // Select the new project
        var index = _projects.FindIndex(p => p.Id == project.Id);
        if (index >= 0)
        {
            _projectPicker.SelectedIndex = index;
        }
    }

    private async void OnDeleteProjectClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        bool confirm = await DisplayAlert("Delete Project",
            $"Delete \"{_currentProject.Name}\" and all its lines?\nThis cannot be undone.",
            "Delete", "Cancel");
        if (!confirm) return;

        await _storyService.DeleteProjectAsync(_currentProject.Id);
        Preferences.Remove($"StoryProd_LastProject_{_auth.CurrentUsername}");
        _currentProject = null;
        _addLineBtn.IsVisible = false;
        _deleteProjectBtn.IsVisible = false;
        _statsLabel.IsVisible = false;
        _projectionLabel.IsVisible = false;
        _linesContainer.Children.Clear();
        await LoadProjectsAsync();
    }

    private async void OnAddMenuClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        var lines = await _storyService.GetLinesAsync(_currentProject.Id);
        
        // Build action sheet options
        var options = new List<string>
        {
            "📝 Add Line at End",
            "🎨 Add Visual at End"
        };
        
        if (lines.Count > 0)
        {
            options.Add("📝 Insert Line Before...");
            options.Add("🎨 Insert Visual Before...");
        }

        string choice = await DisplayActionSheet("Add", "Cancel", null, options.ToArray());
        
        if (choice == null || choice == "Cancel") return;

        if (choice == "📝 Add Line at End")
        {
            await AddLineAtEndAsync();
        }
        else if (choice == "🎨 Add Visual at End")
        {
            await AddVisualAtEndAsync();
        }
        else if (choice == "📝 Insert Line Before...")
        {
            await InsertLineBeforeAsync(lines);
        }
        else if (choice == "🎨 Insert Visual Before...")
        {
            await InsertVisualBeforeAsync(lines);
        }
    }

    private async Task AddLineAtEndAsync()
    {
        if (_currentProject == null) return;
        
        string lineText = await ShowMultiLineInputAsync(
            "Add Script Line",
            "Enter the narration/script text (multi-line supported):",
            "",
            "What does the narrator say?");
        
        if (string.IsNullOrWhiteSpace(lineText)) return;
        
        string visualDesc = await DisplayPromptAsync(
            "Visual Description",
            "Describe the visual for this line:",
            placeholder: "What should the viewer see?",
            initialValue: "");
        
        await _storyService.AddLineAsync(
            _currentProject.Id, 
            lineText.Trim(), 
            visualDesc?.Trim() ?? "");
        
        await LoadLinesAsync(preserveScroll: true);
    }

    private async Task AddVisualAtEndAsync()
    {
        if (_currentProject == null) return;

        string visualDesc = await DisplayPromptAsync(
            "Add Visual",
            "Describe the visual:",
            placeholder: "What should the viewer see?");

        if (string.IsNullOrWhiteSpace(visualDesc)) return;

        var newLine = await _storyService.AddLineAsync(
            _currentProject.Id,
            "",
            visualDesc.Trim());

        newLine.IsSilent = true;
        await _storyService.UpdateLineAsync(newLine);

        await LoadLinesAsync(preserveScroll: true);
    }

    private async Task InsertLineBeforeAsync(List<StoryLine> lines)
    {
        // Build options showing line number and preview
        var options = lines.Select(l => 
        {
            string preview = !string.IsNullOrEmpty(l.LineText) 
                ? (l.LineText.Length > 30 ? l.LineText.Substring(0, 30) + "..." : l.LineText)
                : (l.VisualDescription.Length > 30 ? l.VisualDescription.Substring(0, 30) + "..." : l.VisualDescription);
            string type = l.IsSilent ? "🎨" : "📝";
            return $"{l.LineOrder}. {type} {preview}";
        }).ToArray();

        string choice = await DisplayActionSheet("Insert Before Which Line?", "Cancel", null, options);
        if (choice == null || choice == "Cancel") return;

        // Parse the line number
        int dotIndex = choice.IndexOf('.');
        if (dotIndex <= 0) return;
        if (!int.TryParse(choice.Substring(0, dotIndex), out int targetOrder)) return;

        // Get line text
        string lineText = await ShowMultiLineInputAsync(
            "Insert Script Line",
            $"This line will be inserted before line {targetOrder}:",
            "",
            "What does the narrator say?");
        
        if (string.IsNullOrWhiteSpace(lineText)) return;
        
        string visualDesc = await DisplayPromptAsync(
            "Visual Description",
            "Describe the visual for this line:",
            placeholder: "What should the viewer see?",
            initialValue: "");

        await _storyService.InsertLineBeforeAsync(
            _currentProject.Id,
            targetOrder,
            lineText.Trim(),
            visualDesc?.Trim() ?? "",
            isSilent: false);
        
        await LoadLinesAsync(preserveScroll: true);
    }

    private async Task InsertVisualBeforeAsync(List<StoryLine> lines)
    {
        // Build options showing line number and preview
        var options = lines.Select(l => 
        {
            string preview = !string.IsNullOrEmpty(l.LineText) 
                ? (l.LineText.Length > 30 ? l.LineText.Substring(0, 30) + "..." : l.LineText)
                : (l.VisualDescription.Length > 30 ? l.VisualDescription.Substring(0, 30) + "..." : l.VisualDescription);
            string type = l.IsSilent ? "🎨" : "📝";
            return $"{l.LineOrder}. {type} {preview}";
        }).ToArray();

        string choice = await DisplayActionSheet("Insert Before Which Line?", "Cancel", null, options);
        if (choice == null || choice == "Cancel") return;

        // Parse the line number
        int dotIndex = choice.IndexOf('.');
        if (dotIndex <= 0) return;
        if (!int.TryParse(choice.Substring(0, dotIndex), out int targetOrder)) return;

        string visualDesc = await DisplayPromptAsync(
            "Insert Visual",
            $"This visual will be inserted before line {targetOrder}:",
            placeholder: "What should the viewer see?");

        if (string.IsNullOrWhiteSpace(visualDesc)) return;

        await _storyService.InsertLineBeforeAsync(
            _currentProject.Id,
            targetOrder,
            "",
            visualDesc.Trim(),
            isSilent: true);
        
        await LoadLinesAsync(preserveScroll: true);
    }

    private async void OnAddLineClicked(object? sender, EventArgs e)
    {
        await AddLineAtEndAsync();
    }

    private async void OnAddVisualClicked(object? sender, EventArgs e)
    {
        await AddVisualAtEndAsync();
    }

    private async Task EditLineTextAsync(StoryLine line)
    {
        string newText = await ShowMultiLineInputAsync(
            "Edit Line Text",
            "Script/narration text (multi-line supported):",
            line.LineText,
            "What does the narrator say?");
        
        if (newText == null) return; // Cancelled
        
        line.LineText = newText.Trim();
        await _storyService.UpdateLineAsync(line);
        await LoadLinesAsync(preserveScroll: true);
    }

    private async Task<string> ShowMultiLineInputAsync(string title, string message, string initialValue, string placeholder)
    {
        var tcs = new TaskCompletionSource<string>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = 500,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = message,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var editor = new Editor
        {
            Text = initialValue ?? "",
            Placeholder = placeholder,
            HeightRequest = 180,
            FontSize = 14,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            AutoSize = EditorAutoSizeOption.Disabled
        };
        stack.Children.Add(editor);

        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };

        var okBtn = new Button
        {
            Text = "OK",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };

        cancelBtn.Clicked += (s, e) =>
        {
            if (this.Content is Grid mainGrid)
            {
                mainGrid.Children.Remove(overlay);
            }
            tcs.TrySetResult(null);
        };

        okBtn.Clicked += (s, e) =>
        {
            if (this.Content is Grid mainGrid)
            {
                mainGrid.Children.Remove(overlay);
            }
            tcs.TrySetResult(editor.Text);
        };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(okBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        overlay.Children.Add(card);

        // Content is already a Grid, just add overlay on top
        if (this.Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        editor.Focus();

        return await tcs.Task;
    }

    private async Task EditVisualDescriptionAsync(StoryLine line)
    {
        string newDesc = await DisplayPromptAsync(
            "Edit Visual Description",
            "What should the viewer see?",
            initialValue: line.VisualDescription,
            placeholder: "Describe the visual...");
        
        if (newDesc == null) return; // Cancelled
        
        line.VisualDescription = newDesc.Trim();
        await _storyService.UpdateLineAsync(line);
        await LoadLinesAsync(preserveScroll: true);
    }

    private async Task PickImageForLineAsync(StoryLine line)
    {
        try
        {
            // Allow images and mp4 videos
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".mp4" } },
                { DevicePlatform.Android, new[] { "image/*", "video/mp4" } },
                { DevicePlatform.iOS, new[] { "public.image", "public.mpeg-4" } }
            });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select image or video for visual",
                FileTypes = customFileType
            });

            if (result == null) return; // Cancelled

            string filePath = result.FullPath;
            bool isVideo = filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

            if (isVideo)
            {
                // Extract first frame as thumbnail using MediaElement approach
                string thumbPath = filePath + ".thumb.png";
                bool thumbCreated = await ExtractVideoThumbnailAsync(filePath, thumbPath);
                
                if (!thumbCreated)
                {
                    await DisplayAlert("Video Selected",
                        $"Video saved but thumbnail could not be generated.\nFile: {Path.GetFileName(filePath)}",
                        "OK");
                }
            }

            line.VisualAssetPath = filePath;
            await _storyService.UpdateLineAsync(line);
            await LoadLinesAsync(preserveScroll: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STORY] File pick error: {ex.Message}");
        }
    }

    private async Task<bool> ExtractVideoThumbnailAsync(string videoPath, string thumbnailPath)
    {
        try
        {
#if WINDOWS
            // Use Windows MediaClip API to extract first frame
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(videoPath);
            var clip = await Windows.Media.Editing.MediaClip.CreateFromFileAsync(file);
            var composition = new Windows.Media.Editing.MediaComposition();
            composition.Clips.Add(clip);

            // Get thumbnail at 0.5 second mark (avoids black first frames)
            var timeOffset = TimeSpan.FromMilliseconds(500);
            if (clip.OriginalDuration < timeOffset)
                timeOffset = TimeSpan.Zero;

            var thumbnail = await composition.GetThumbnailAsync(
                timeOffset, 320, 320, 
                Windows.Media.Editing.VideoFramePrecision.NearestKeyFrame);

            // Save to file
            var outputFile = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(
                Path.GetDirectoryName(thumbnailPath));
            var thumbFile = await outputFile.CreateFileAsync(
                Path.GetFileName(thumbnailPath),
                Windows.Storage.CreationCollisionOption.ReplaceExisting);

            using (var destStream = await thumbFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                await Windows.Storage.Streams.RandomAccessStream.CopyAsync(thumbnail, destStream);
            }

            return File.Exists(thumbnailPath);
#else
            // Fallback for non-Windows: just store path, no thumbnail
            await Task.CompletedTask;
            return false;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STORY] Thumbnail extraction error: {ex.Message}");
            return false;
        }
    }

    #region Export

    private async void OnExportMenuClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        string choice = await DisplayActionSheet(
            "Export Options",
            "Cancel",
            null,
            "📝 For Revision (with AI instructions)",
            "✨ Fill Missing (keep story, add prompts)",
            "🔧 Surgical Edit (specific changes)",
            "📱 Mobile Tasks (work on phone)",
            "💬 For Discussion (story only)",
            "📋 Script Only (narration text)",
            "🎨 Visuals Only (visual descriptions)",
            "🤖 Convert Any Story to Import Format");

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        if (choice.StartsWith("📝"))
            await ExportForRevisionAsync();
        else if (choice.StartsWith("✨"))
            await ExportFillMissingAsync();
        else if (choice.StartsWith("🔧"))
            await ExportSurgicalEditAsync();
        else if (choice.StartsWith("📱"))
            await ExportMobileTasksAsync();
        else if (choice.StartsWith("💬"))
            await ExportForDiscussionAsync();
        else if (choice.StartsWith("📋"))
            await ExportScriptOnlyAsync();
        else if (choice.StartsWith("🎨"))
            await ExportVisualsOnlyAsync();
        else if (choice.StartsWith("🤖"))
            await ExportConvertStoryPromptAsync();
    }

    private async Task<List<StoryLine>?> GetLinesForExportAsync()
    {
        var lines = await _storyService.GetLinesAsync(_currentProject!.Id);
        
        if (lines.Count == 0)
        {
            await DisplayAlert("No Lines", "Add some lines to the project before exporting.", "OK");
            return null;
        }
        return lines;
    }

    private async Task<string> BuildStoryDataAsync(List<StoryLine> lines, bool includePrompts = true)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header context
        sb.AppendLine($"// Story: {_currentProject!.Name}");
        if (!string.IsNullOrWhiteSpace(_currentProject.Description))
        {
            sb.AppendLine($"// Description: {_currentProject.Description}");
        }
        
        var (totalLines, preparedLines) = await _storyService.GetProjectStatsAsync(_currentProject.Id);
        int percent = totalLines > 0 ? (int)(preparedLines * 100.0 / totalLines) : 0;
        sb.AppendLine($"// Progress: {preparedLines}/{totalLines} visuals prepared ({percent}%)");
        sb.AppendLine();
        sb.AppendLine("// CURRENT DRAFT:");
        sb.AppendLine("```csharp");
        
        foreach (var line in lines)
        {
            string typeComment = line.IsSilent ? "// VISUAL-ONLY" : "// NARRATION";
            string statusComment = line.VisualPrepared ? " ✓ LOCKED" : "";
            
            sb.AppendLine($"{typeComment}{statusComment}");
            
            // Escape quotes in the text
            string scriptEscaped = (line.LineText ?? "").Replace("\"", "\\\"");
            string visualEscaped = (line.VisualDescription ?? "").Replace("\"", "\\\"");
            
            sb.AppendLine($"lines[{line.LineOrder}].Script = \"{scriptEscaped}\";");
            sb.AppendLine($"lines[{line.LineOrder}].Visual = \"{visualEscaped}\";");
            
            if (includePrompts)
            {
                // Include existing shots if any
                if (line.ShotCount > 0)
                {
                    var shots = _storyService.GetShots(line);
                    var shotsStr = string.Join(" | ", shots.Select(s => $"shot{s.Index}: {s.Description}"));
                    sb.AppendLine($"lines[{line.LineOrder}].Shots = \"{shotsStr.Replace("\"", "\\\"")}\";");
                    
                    // Include per-shot prompts
                    foreach (var shot in shots)
                    {
                        if (!string.IsNullOrEmpty(shot.ImagePrompt))
                        {
                            sb.AppendLine($"lines[{line.LineOrder}].Shot{shot.Index}_ImagePrompt = \"{shot.ImagePrompt.Replace("\"", "\\\"")}\";");
                        }
                        if (!string.IsNullOrEmpty(shot.VideoPrompt))
                        {
                            sb.AppendLine($"lines[{line.LineOrder}].Shot{shot.Index}_VideoPrompt = \"{shot.VideoPrompt.Replace("\"", "\\\"")}\";");
                        }
                    }
                }
                
                // Include existing line-level prompts if any
                if (!string.IsNullOrEmpty(line.ImagePrompt))
                {
                    sb.AppendLine($"lines[{line.LineOrder}].ImagePrompt = \"{line.ImagePrompt.Replace("\"", "\\\"")}\";");
                }
                if (!string.IsNullOrEmpty(line.VideoPrompt))
                {
                    sb.AppendLine($"lines[{line.LineOrder}].VideoPrompt = \"{line.VideoPrompt.Replace("\"", "\\\"")}\";");
                }
            }
            
            sb.AppendLine();
        }
        
        sb.AppendLine("```");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Shared format instructions for export prompts - delegates to StoryPromptTemplates
    /// </summary>
    private string GetExportFormatInstructions()
    {
        // Extract just the format example part (without the --- prefix and rules)
        var fullInstructions = StoryPromptTemplates.GetDraftFormatInstructions();
        // Find the ```csharp block
        int start = fullInstructions.IndexOf("```csharp");
        int end = fullInstructions.IndexOf("```", start + 9) + 3;
        if (start >= 0 && end > start)
        {
            return fullInstructions.Substring(start, end - start);
        }
        return fullInstructions;
    }

    private async Task ExportForRevisionAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        var sb = new System.Text.StringBuilder();
        sb.Append(await BuildStoryDataAsync(lines, includePrompts: true));
        sb.AppendLine();
        
        // Instructions
        sb.AppendLine("---");
        sb.AppendLine("Revise or complete this story.");
        sb.AppendLine(StoryPromptTemplates.GetDraftFormatInstructions());

        await Clipboard.SetTextAsync(sb.ToString());
        
        await DisplayAlert("Exported!", 
            $"Revision prompt for \"{_currentProject!.Name}\" copied to clipboard.\n\n{lines.Count} lines with AI instructions.", 
            "OK");
    }

    private async Task ExportSurgicalEditAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        // Ask user what changes they want
        string? instructions = await ShowMultiLineInputAsync(
            "Surgical Edit",
            "Describe the changes you want to make:\n(e.g., \"Delete lines 5-7\", \"Add a scene after line 3\", \"Change the visual in line 10\")",
            "",
            "Describe your changes...");

        if (string.IsNullOrWhiteSpace(instructions)) return;

        // Build summary of current draft
        var summaryBuilder = new System.Text.StringBuilder();
        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            string type = line.IsSilent ? "VISUAL" : "NARR";
            string script = string.IsNullOrEmpty(line.LineText) ? "(silent)" : Truncate(line.LineText, 60);
            string visual = Truncate(line.VisualDescription ?? "", 40);
            summaryBuilder.AppendLine($"[{line.LineOrder}] {type}: {script} | Visual: {visual}");
        }

        string prompt = StoryPromptTemplates.BuildSurgicalEditPrompt(
            _currentProject!.Name,
            lines.Count,
            summaryBuilder.ToString(),
            instructions);

        await Clipboard.SetTextAsync(prompt);
        
        await DisplayAlert("Exported!", 
            $"Surgical edit prompt copied to clipboard.\n\nPaste to LLM, then use 'Import Draft' → 'Surgical Edit' to apply changes.", 
            "OK");
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..(max - 3)] + "...";
    }
    
    private async Task ExportFillMissingAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        var sb = new System.Text.StringBuilder();
        sb.Append(await BuildStoryDataAsync(lines, includePrompts: true));
        sb.AppendLine();
        
        // Instructions
        sb.AppendLine("---");
        sb.AppendLine("Do NOT change the story. Keep all Script, Visual, and Shots exactly as they are.");
        sb.AppendLine("Your only job: fill in any missing fields. If something is missing, add it. If it exists, keep it exactly as-is.");
        sb.AppendLine();
        sb.AppendLine("Output ONLY a C# code block with the full story in this exact format:");
        sb.AppendLine();
        sb.Append(GetExportFormatInstructions());
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Output ALL lines, numbered sequentially");
        sb.AppendLine("- Use // NARRATION or // VISUAL-ONLY before each line");
        sb.AppendLine("- Do NOT change any existing content - copy Script, Visual, Shots exactly as-is");
        sb.AppendLine("- If a line has Shots but is missing Shot#_ImagePrompt or Shot#_VideoPrompt, add them");
        sb.AppendLine("- If a line is missing Shots breakdown, add it based on the Visual description");
        sb.AppendLine("- If a line is missing ImagePrompt or VideoPrompt, add them");
        sb.AppendLine("- For complex visuals (montages, multiple scenes), break into shots separated by |");
        sb.AppendLine("- No commentary outside the code block");

        await Clipboard.SetTextAsync(sb.ToString());
        
        await DisplayAlert("Exported!", 
            $"Fill-missing prompt for \"{_currentProject!.Name}\" copied to clipboard.\n\n{lines.Count} lines - will fill any missing prompts.", 
            "OK");
    }

    private async Task ExportForDiscussionAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        string storyData = await BuildStoryDataAsync(lines, includePrompts: true);
        
        await Clipboard.SetTextAsync(storyData);
        
        await DisplayAlert("Exported!", 
            $"Story data for \"{_currentProject!.Name}\" copied to clipboard.\n\n{lines.Count} lines ready for discussion.", 
            "OK");
    }

    private async Task ExportScriptOnlyAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {_currentProject!.Name}");
        if (!string.IsNullOrWhiteSpace(_currentProject.Description))
        {
            sb.AppendLine($"_{_currentProject.Description}_");
        }
        sb.AppendLine();

        foreach (var line in lines)
        {
            if (!line.IsSilent && !string.IsNullOrEmpty(line.LineText))
            {
                sb.AppendLine(line.LineText);
                sb.AppendLine();
            }
            else if (line.IsSilent)
            {
                sb.AppendLine($"[VISUAL: {line.VisualDescription}]");
                sb.AppendLine();
            }
        }

        await Clipboard.SetTextAsync(sb.ToString());
        
        int scriptLines = lines.Count(l => !l.IsSilent && !string.IsNullOrEmpty(l.LineText));
        await DisplayAlert("Exported!", 
            $"Script for \"{_currentProject.Name}\" copied to clipboard.\n\n{scriptLines} narration lines.", 
            "OK");
    }

    private async Task ExportVisualsOnlyAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {_currentProject!.Name} - Visual Breakdown");
        sb.AppendLine();

        foreach (var line in lines)
        {
            string status = line.VisualPrepared ? "✓" : "○";
            string type = line.IsSilent ? "[VISUAL-ONLY]" : $"[{line.LineOrder}]";
            
            sb.AppendLine($"{status} {type} {line.VisualDescription}");
            
            if (line.ShotCount > 0)
            {
                var shots = _storyService.GetShots(line);
                foreach (var shot in shots)
                {
                    string shotStatus = shot.Done ? "  ✓" : "  ○";
                    sb.AppendLine($"{shotStatus} Shot {shot.Index}: {shot.Description}");
                }
            }
            sb.AppendLine();
        }

        await Clipboard.SetTextAsync(sb.ToString());
        
        int withVisuals = lines.Count(l => !string.IsNullOrEmpty(l.VisualDescription));
        await DisplayAlert("Exported!", 
            $"Visual breakdown for \"{_currentProject.Name}\" copied to clipboard.\n\n{withVisuals} visuals listed.", 
            "OK");
    }

    private async Task ExportMobileTasksAsync()
    {
        var lines = await GetLinesForExportAsync();
        if (lines == null) return;

        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine($"# {_currentProject!.Name} - Mobile Tasks");
        sb.AppendLine();
        
        // Collect all incomplete tasks
        var tasks = new List<string>();
        int taskNum = 1;
        
        foreach (var line in lines)
        {
            if (line.ShotCount > 0)
            {
                var shots = _storyService.GetShots(line);
                foreach (var shot in shots)
                {
                    // Task 1: Generate Image (if not done)
                    if (!shot.Task1_ImageGenerated)
                    {
                        string prompt = !string.IsNullOrEmpty(shot.ImagePrompt) 
                            ? shot.ImagePrompt 
                            : "(no prompt)";
                        tasks.Add($"{taskNum}. Line {line.LineOrder} Clip {shot.Index}: Generate Image\n   Prompt: {prompt}");
                        taskNum++;
                    }
                    
                    // Task 2: Generate Video (if not done)
                    if (!shot.Task2_VideoGenerated)
                    {
                        string prompt = !string.IsNullOrEmpty(shot.VideoPrompt) 
                            ? shot.VideoPrompt 
                            : "(no prompt)";
                        tasks.Add($"{taskNum}. Line {line.LineOrder} Clip {shot.Index}: Generate Video\n   Prompt: {prompt}");
                        taskNum++;
                    }
                }
            }
            else if (!line.VisualPrepared)
            {
                // Line has no shots breakdown but isn't prepared
                tasks.Add($"{taskNum}. Line {line.LineOrder}: Prepare visual\n   Visual: {line.VisualDescription}");
                taskNum++;
            }
        }
        
        // === TASK LIST ===
        sb.AppendLine("## TASKS TO DO");
        sb.AppendLine();
        
        if (tasks.Count == 0)
        {
            sb.AppendLine("✓ All tasks complete!");
        }
        else
        {
            sb.AppendLine($"{tasks.Count} tasks remaining:");
            sb.AppendLine();
            foreach (var task in tasks)
            {
                sb.AppendLine(task);
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("---");
        sb.AppendLine();
        
        // === FULL STORY REFERENCE ===
        sb.AppendLine("## STORY REFERENCE");
        sb.AppendLine();
        
        foreach (var line in lines)
        {
            string type = line.IsSilent ? "VISUAL-ONLY" : "NARRATION";
            string status = line.VisualPrepared ? "✓" : "○";
            
            sb.AppendLine($"### Line {line.LineOrder} [{type}] {status}");
            
            if (!line.IsSilent && !string.IsNullOrEmpty(line.LineText))
            {
                sb.AppendLine($"Script: \"{line.LineText}\"");
            }
            
            sb.AppendLine($"Visual: {line.VisualDescription}");
            sb.AppendLine();
            
            if (line.ShotCount > 0)
            {
                var shots = _storyService.GetShots(line);
                foreach (var shot in shots)
                {
                    string clipStatus = shot.AllTasksDone ? "✓" : "○";
                    string task1 = shot.Task1_ImageGenerated ? "✓" : "○";
                    string task2 = shot.Task2_VideoGenerated ? "✓" : "○";
                    
                    sb.AppendLine($"  **Clip {shot.Index}** {clipStatus} — {shot.Description}");
                    sb.AppendLine($"    {task1} Image: {(string.IsNullOrEmpty(shot.ImagePrompt) ? "(no prompt)" : shot.ImagePrompt)}");
                    sb.AppendLine($"    {task2} Video: {(string.IsNullOrEmpty(shot.VideoPrompt) ? "(no prompt)" : shot.VideoPrompt)}");
                    sb.AppendLine();
                }
            }
            else
            {
                // Show line-level prompts if no shots
                if (!string.IsNullOrEmpty(line.ImagePrompt))
                    sb.AppendLine($"  Image Prompt: {line.ImagePrompt}");
                if (!string.IsNullOrEmpty(line.VideoPrompt))
                    sb.AppendLine($"  Video Prompt: {line.VideoPrompt}");
                sb.AppendLine();
            }
        }

        await Clipboard.SetTextAsync(sb.ToString());
        
        await DisplayAlert("Exported!", 
            $"Mobile tasks for \"{_currentProject.Name}\" copied to clipboard.\n\n{tasks.Count} tasks remaining.", 
            "OK");
    }

    private async Task ExportConvertStoryPromptAsync()
    {
        // Ask user to paste or type their raw story
        string? rawStory = await ShowMultiLineInputAsync(
            "Convert Story",
            "Paste or write your story in any format.\nA prompt will be generated that you can give to any AI to convert it to the import format.",
            "",
            "Once upon a time...\nThe hero walked into the room.\n[visual: dark corridor]\n...");

        if (string.IsNullOrWhiteSpace(rawStory)) return;

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("I have a story/script that I need converted into a specific C# import format for my video production tool.");
        sb.AppendLine("Below is the raw story. Convert it into the exact format shown, preserving all the content.");
        sb.AppendLine();
        sb.AppendLine("=== RAW STORY ===");
        sb.AppendLine(rawStory);
        sb.AppendLine("=== END RAW STORY ===");
        sb.AppendLine();
        sb.AppendLine(StoryPromptTemplates.GetDraftFormatInstructions());
        sb.AppendLine();
        sb.AppendLine("ADDITIONAL INSTRUCTIONS:");
        sb.AppendLine("- Split the story into logical lines/scenes (one narration segment per line)");
        sb.AppendLine("- If the story has no explicit visual descriptions, create appropriate ones based on the narration");
        sb.AppendLine("- Lines with no narration (visual-only moments) should use Script = \"\" (empty string)");
        sb.AppendLine("- Generate image and video prompts for each line/shot that would work with AI image/video generators");
        sb.AppendLine("- Keep the original text as close to verbatim as possible for the Script fields");
        sb.AppendLine("- Number lines sequentially starting at 1");
        sb.AppendLine("- Output ONLY the code block, no other text");

        await Clipboard.SetTextAsync(sb.ToString());

        await DisplayAlert("Prompt Copied!",
            "The conversion prompt has been copied to your clipboard.\n\n" +
            "Steps:\n" +
            "1. Paste this prompt to any AI (ChatGPT, Claude, etc.)\n" +
            "2. The AI will output the story in the import format\n" +
            "3. Copy the AI's output\n" +
            "4. Use 'Import Draft' → 'Paste from Clipboard' to import",
            "OK");
    }

    #endregion

    #region Import Draft

    private async void OnImportDraftClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        // Ask user: File, Paste, Surgical, or Video?
        string choice = await DisplayActionSheet(
            "Import Draft",
            "Cancel",
            null,
            "📁 From File",
            "📋 Paste from Clipboard",
            "🔧 Surgical Edit (apply changes)",
            "🎬 Import Draft from Video");

        if (choice == null || choice == "Cancel") return;

        // Handle video import separately - it has its own flow
        if (choice == "🎬 Import Draft from Video")
        {
            await ShowVideoImportFlowAsync();
            return;
        }

        // Handle surgical edit separately
        if (choice == "🔧 Surgical Edit (apply changes)")
        {
            await ImportSurgicalEditAsync();
            return;
        }

        string content = "";

        if (choice == "📁 From File")
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select draft file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".md", ".txt", ".cs" } },
                        { DevicePlatform.Android, new[] { "text/*" } },
                        { DevicePlatform.iOS, new[] { "public.plain-text" } }
                    })
                });

                if (result == null) return;
                content = await File.ReadAllTextAsync(result.FullPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STORY] File pick error: {ex.Message}");
                await DisplayAlert("Error", $"Could not read file: {ex.Message}", "OK");
                return;
            }
        }
        else if (choice == "📋 Paste from Clipboard")
        {
            content = await ShowPasteDialogAsync();
            if (content == null) return; // Cancelled
        }

        await ProcessImportContentAsync(content);
    }

    private async Task ImportSurgicalEditAsync()
    {
        // Get content from clipboard
        string? content = await ShowMultiLineInputAsync(
            "Import Surgical Edit",
            "Paste the LLM's surgical edit response:",
            "",
            "// SURGICAL EDIT COMMANDS...");

        if (string.IsNullOrWhiteSpace(content)) return;

        // Parse the commands
        var parseResult = SurgicalEditParser.Parse(content);

        if (!parseResult.Success || parseResult.Commands.Count == 0)
        {
            await DisplayAlert("Parse Error", 
                "Could not parse surgical edit commands.\n\n" + string.Join("\n", parseResult.Errors), 
                "OK");
            return;
        }

        // Get current lines for validation
        var currentLines = await _storyService.GetLinesAsync(_currentProject!.Id);

        // Validate against current draft
        var validationErrors = SurgicalEditParser.ValidateCommands(
            parseResult, 
            _currentProject.Name, 
            currentLines.Count);

        if (validationErrors.Count > 0)
        {
            bool proceed = await DisplayAlert(
                "Validation Warning",
                $"Found issues:\n\n{string.Join("\n", validationErrors)}\n\nProceed anyway?",
                "Proceed", "Cancel");
            
            if (!proceed) return;
        }

        // Show pretty confirmation overlay with checkboxes
        var (confirmedCommands, draftName) = await ShowSurgicalEditConfirmationAsync(parseResult.Commands, currentLines);
        
        if (confirmedCommands == null || confirmedCommands.Count == 0 || string.IsNullOrWhiteSpace(draftName)) 
            return;

        // Create a new draft and apply only confirmed changes
        await ApplySurgicalEditsAsync(confirmedCommands, currentLines, draftName);
    }

    private async Task<(List<SurgicalEditCommand>? Commands, string? DraftName)> ShowSurgicalEditConfirmationAsync(
        List<SurgicalEditCommand> commands, 
        List<StoryLine> currentLines)
    {
        var tcs = new TaskCompletionSource<(List<SurgicalEditCommand>?, string?)>();
        string? chosenDraftName = null;
        
        // Track which commands are checked
        var checkStates = new Dictionary<int, bool>();
        var checkBoxes = new Dictionary<int, CheckBox>();
        for (int i = 0; i < commands.Count; i++)
        {
            checkStates[i] = true; // All checked by default
        }

        // Create overlay
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            ZIndex = 1000
        };

        // Card container
        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D30"),
            BorderColor = Color.FromArgb("#3E3E42"),
            CornerRadius = 12,
            Padding = new Thickness(20),
            MaximumWidthRequest = 700,
            MaximumHeightRequest = 650,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = false
        };

        // Use Grid layout to ensure buttons always visible at bottom
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),   // Header
                new RowDefinition(GridLength.Auto),   // Info
                new RowDefinition(GridLength.Auto),   // Progress bar
                new RowDefinition(GridLength.Star),   // Scrollable content
                new RowDefinition(GridLength.Auto),   // Select all row
                new RowDefinition(GridLength.Auto)    // Button row
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

        // Info line
        var infoLine = new Label
        {
            Text = $"Draft: {_currentProject!.Name}  •  {currentLines.Count} lines  •  {commands.Count} changes",
            FontSize = 13,
            TextColor = Color.FromArgb("#AAAAAA")
        };
        Grid.SetRow(infoLine, 1);
        mainGrid.Children.Add(infoLine);

        // Scrollable list of command cards (in row 3, which is Star-sized)
        var scrollView = new ScrollView { VerticalScrollBarVisibility = ScrollBarVisibility.Always };
        var commandsList = new VerticalStackLayout { Spacing = 10 };

        for (int i = 0; i < commands.Count; i++)
        {
            var cmd = commands[i];
            int index = i; // Capture for closure

            // Command card
            var cmdCard = new Frame
            {
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                BorderColor = GetCommandColor(cmd.Type),
                CornerRadius = 8,
                Padding = new Thickness(12),
                HasShadow = false
            };

            var cmdLayout = new VerticalStackLayout { Spacing = 8 };

            // Header row with checkbox, type, line number
            var headerRow = new HorizontalStackLayout { Spacing = 12 };

            // Checkbox
            var checkBox = new CheckBox
            {
                IsChecked = true,
                Color = GetCommandColor(cmd.Type),
                VerticalOptions = LayoutOptions.Center
            };
            checkBox.CheckedChanged += (s, e) => checkStates[index] = e.Value;
            checkBoxes[i] = checkBox;
            headerRow.Children.Add(checkBox);

            // Icon and type
            var typeLabel = new Label
            {
                Text = GetCommandIcon(cmd.Type) + " " + cmd.Type.ToUpper(),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = GetCommandColor(cmd.Type),
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 90
            };
            headerRow.Children.Add(typeLabel);

            // Line number
            var lineLabel = new Label
            {
                Text = $"Line {cmd.LineNumber}",
                FontSize = 13,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 60
            };
            headerRow.Children.Add(lineLabel);

            // Brief summary
            var summaryText = GetCommandSummary(cmd);
            var summaryLabel = new Label
            {
                Text = summaryText,
                FontSize = 12,
                TextColor = Color.FromArgb("#AAAAAA"),
                VerticalOptions = LayoutOptions.Center
            };
            headerRow.Children.Add(summaryLabel);

            // Expand/collapse indicator
            var expandLabel = new Label
            {
                Text = "▼",
                FontSize = 12,
                TextColor = Color.FromArgb("#888888"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.EndAndExpand
            };
            headerRow.Children.Add(expandLabel);

            cmdLayout.Children.Add(headerRow);

            // Details section (initially collapsed)
            var detailsSection = new VerticalStackLayout
            {
                Spacing = 6,
                IsVisible = false,
                Padding = new Thickness(36, 8, 0, 0) // Indent under checkbox
            };

            // Add detailed content based on command type
            AddCommandDetails(detailsSection, cmd, currentLines);

            cmdLayout.Children.Add(detailsSection);

            // Tap to expand/collapse
            var cardTapGesture = new TapGestureRecognizer();
            cardTapGesture.Tapped += (s, e) =>
            {
                detailsSection.IsVisible = !detailsSection.IsVisible;
                expandLabel.Text = detailsSection.IsVisible ? "▲" : "▼";
            };
            cmdCard.GestureRecognizers.Add(cardTapGesture);

            cmdCard.Content = cmdLayout;
            commandsList.Children.Add(cmdCard);
        }

        scrollView.Content = commandsList;
        Grid.SetRow(scrollView, 3);
        mainGrid.Children.Add(scrollView);

        // Select All / Deselect All row
        var selectRow = new HorizontalStackLayout { Spacing = 10 };
        var selectAllBtn = new Button
        {
            Text = "✓ Select All",
            BackgroundColor = Color.FromArgb("#3E3E42"),
            TextColor = Colors.White,
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(12, 0)
        };
        selectAllBtn.Clicked += (s, e) =>
        {
            foreach (var cb in checkBoxes.Values) cb.IsChecked = true;
        };
        selectRow.Children.Add(selectAllBtn);

        var deselectAllBtn = new Button
        {
            Text = "✗ Deselect All",
            BackgroundColor = Color.FromArgb("#3E3E42"),
            TextColor = Colors.White,
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(12, 0)
        };
        deselectAllBtn.Clicked += (s, e) =>
        {
            foreach (var cb in checkBoxes.Values) cb.IsChecked = false;
        };
        selectRow.Children.Add(deselectAllBtn);
        Grid.SetRow(selectRow, 4);
        mainGrid.Children.Add(selectRow);

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
            if (this.Content is Grid pageGrid && pageGrid.Children.Contains(overlay))
                pageGrid.Children.Remove(overlay);
            tcs.TrySetResult((null, null));
        };
        buttonRow.Children.Add(cancelBtn);

        var applyBtn = new Button
        {
            Text = "🚀 Create New Draft",
            BackgroundColor = Color.FromArgb("#0E639C"),
            TextColor = Colors.White,
            WidthRequest = 160,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        };
        applyBtn.Clicked += async (s, e) =>
        {
            // Get confirmed commands
            var confirmed = new List<SurgicalEditCommand>();
            for (int i = 0; i < commands.Count; i++)
            {
                if (checkStates[i])
                    confirmed.Add(commands[i]);
            }

            // Check if some were unchecked
            int uncheckedCount = commands.Count - confirmed.Count;
            if (uncheckedCount > 0 && confirmed.Count > 0)
            {
                bool proceed = await DisplayAlert(
                    "Partial Selection",
                    $"You unchecked {uncheckedCount} of {commands.Count} changes.\n\nAre you sure you want to skip these changes?",
                    "Yes, Apply Selected Only",
                    "Go Back");
                
                if (!proceed) return;
            }
            else if (confirmed.Count == 0)
            {
                await DisplayAlert("No Changes", "You didn't select any changes to apply.", "OK");
                return;
            }

            // Ask for draft name
            string? draftName = await DisplayPromptAsync(
                "Name New Draft",
                "Enter a name for the new draft:",
                "Create",
                "Cancel",
                placeholder: "e.g., Draft v2 (surgical edit)",
                maxLength: 100);

            if (string.IsNullOrWhiteSpace(draftName))
            {
                // User cancelled naming
                return;
            }

            if (this.Content is Grid pg && pg.Children.Contains(overlay))
                pg.Children.Remove(overlay);
            
            tcs.TrySetResult((confirmed, draftName));
        };
        buttonRow.Children.Add(applyBtn);

        Grid.SetRow(buttonRow, 5);
        mainGrid.Children.Add(buttonRow);
        card.Content = mainGrid;
        overlay.Children.Add(card);

        // Add tap-outside to cancel
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            if (this.Content is Grid pg && pg.Children.Contains(overlay))
                pg.Children.Remove(overlay);
            tcs.TrySetResult((null, null));
        };
        overlay.GestureRecognizers.Add(tapGesture);

        // Add overlay to page
        if (this.Content is Grid pageGridAdd)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGridAdd.Children.Add(overlay);
        }

        return await tcs.Task;
    }

    private static Color GetCommandColor(string type) => type.ToLower() switch
    {
        "delete" => Color.FromArgb("#E74C3C"),
        "update" => Color.FromArgb("#F39C12"),
        "insert" => Color.FromArgb("#27AE60"),
        "reorder" => Color.FromArgb("#3498DB"),
        _ => Colors.Gray
    };

    private static string GetCommandIcon(string type) => type.ToLower() switch
    {
        "delete" => "🗑️",
        "update" => "✏️",
        "insert" => "➕",
        "reorder" => "↕️",
        _ => "•"
    };

    private static string GetCommandSummary(SurgicalEditCommand cmd)
    {
        return cmd.Type.ToLower() switch
        {
            "delete" => "Removing this line",
            "update" => $"Changing {cmd.Fields.Count} field(s)",
            "insert" => "Adding new line",
            "reorder" => $"Moving to position {cmd.TargetPosition}",
            _ => ""
        };
    }

    private void AddCommandDetails(VerticalStackLayout container, SurgicalEditCommand cmd, List<StoryLine> currentLines)
    {
        var line = currentLines.FirstOrDefault(l => l.LineOrder == cmd.LineNumber);

        switch (cmd.Type.ToLower())
        {
            case "delete":
                if (line != null)
                {
                    AddDetailRow(container, "🗑️ REMOVING:", "");
                    if (!line.IsSilent && !string.IsNullOrWhiteSpace(line.LineText))
                        AddDetailRow(container, "Script", line.LineText, Color.FromArgb("#E74C3C"));
                    if (!string.IsNullOrWhiteSpace(line.VisualDescription))
                        AddDetailRow(container, "Visual", line.VisualDescription, Color.FromArgb("#E74C3C"));
                }
                break;

            case "update":
                // Show what's changing
                foreach (var field in cmd.Fields)
                {
                    // Skip shot-specific prompts in main view, just show count
                    if (field.Key.StartsWith("Shot") && field.Key.Contains("Prompt"))
                        continue;

                    string oldValue = GetCurrentFieldValue(line, field.Key);
                    
                    if (!string.IsNullOrEmpty(oldValue))
                    {
                        AddDetailRow(container, $"📝 {field.Key} (was):", oldValue, Color.FromArgb("#888888"), strikethrough: true);
                    }
                    AddDetailRow(container, $"✨ {field.Key} (new):", field.Value, Color.FromArgb("#27AE60"));
                }
                
                // Count shot prompts
                int shotPromptCount = cmd.Fields.Keys.Count(k => k.StartsWith("Shot") && k.Contains("Prompt"));
                if (shotPromptCount > 0)
                {
                    AddDetailRow(container, "🎬 Shot Prompts", $"{shotPromptCount} shot prompt(s) included", Color.FromArgb("#3498DB"));
                }
                break;

            case "insert":
                AddDetailRow(container, "➕ INSERTING AFTER LINE " + cmd.LineNumber + ":", "");
                if (cmd.Fields.TryGetValue("Script", out var script) && !string.IsNullOrWhiteSpace(script))
                    AddDetailRow(container, "Script", script, Color.FromArgb("#27AE60"));
                else
                    AddDetailRow(container, "Type", "(Visual-only / Silent)", Color.FromArgb("#888888"));
                    
                if (cmd.Fields.TryGetValue("Visual", out var visual) && !string.IsNullOrWhiteSpace(visual))
                    AddDetailRow(container, "Visual", visual, Color.FromArgb("#27AE60"));
                if (cmd.Fields.TryGetValue("Shots", out var shots) && !string.IsNullOrWhiteSpace(shots))
                    AddDetailRow(container, "Shots", shots, Color.FromArgb("#27AE60"));
                break;

            case "reorder":
                AddDetailRow(container, "↕️ Moving", $"Line {cmd.LineNumber} → Position {cmd.TargetPosition}", Color.FromArgb("#3498DB"));
                if (line != null && !string.IsNullOrWhiteSpace(line.LineText))
                    AddDetailRow(container, "Content", Truncate(line.LineText, 100), Color.FromArgb("#AAAAAA"));
                break;
        }
    }

    private void AddDetailRow(VerticalStackLayout container, string label, string value, Color? valueColor = null, bool strikethrough = false)
    {
        if (string.IsNullOrEmpty(value) && string.IsNullOrEmpty(label))
            return;

        var row = new VerticalStackLayout { Spacing = 2 };
        
        // Label
        row.Children.Add(new Label
        {
            Text = label,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#888888")
        });
        
        // Value (if any)
        if (!string.IsNullOrEmpty(value))
        {
            var valueLabel = new Label
            {
                Text = value,
                FontSize = 12,
                TextColor = valueColor ?? Color.FromArgb("#CCCCCC"),
                LineBreakMode = LineBreakMode.WordWrap,
                MaxLines = 4
            };
            
            if (strikethrough)
                valueLabel.TextDecorations = TextDecorations.Strikethrough;
            
            row.Children.Add(valueLabel);
        }
        
        container.Children.Add(row);
    }

    private static string GetCurrentFieldValue(StoryLine? line, string fieldName)
    {
        if (line == null) return "";
        
        return fieldName switch
        {
            "Script" => line.IsSilent ? "(silent)" : line.LineText,
            "Visual" => line.VisualDescription,
            "ImagePrompt" => line.ImagePrompt,
            "VideoPrompt" => line.VideoPrompt,
            "Shots" => line.ShotCount > 0 ? $"({line.ShotCount} shots)" : "",
            _ => ""
        };
    }

    private async Task ApplySurgicalEditsAsync(List<SurgicalEditCommand> commands, List<StoryLine> currentLines, string draftName)
    {
        _loadingLabel.Text = "Applying surgical edits...";
        _loadingOverlay.IsVisible = true;

        try
        {
            // Build ImportedLine list from current lines
            var importedLines = new List<ImportedLine>();
            foreach (var line in currentLines.OrderBy(l => l.LineOrder))
            {
                var shots = _storyService.GetShots(line);
                var imported = new ImportedLine
                {
                    Script = line.IsSilent ? null : line.LineText,
                    Visual = line.VisualDescription,
                    IsSilent = line.IsSilent,
                    ImagePrompt = line.ImagePrompt,
                    VideoPrompt = line.VideoPrompt
                };
                
                // Build shots string from VisualShots
                if (shots.Count > 0)
                {
                    var shotDescs = shots.Select((s, i) => $"shot{i + 1}: {s.Description}");
                    imported.Shots = string.Join(" | ", shotDescs);
                    
                    // Copy shot prompts - VisualShot uses Index (1-based)
                    foreach (var shot in shots)
                    {
                        imported.ShotPrompts[shot.Index] = (shot.ImagePrompt, shot.VideoPrompt);
                    }
                }
                
                importedLines.Add(imported);
            }

            // Apply UPDATEs to importedLines
            foreach (var cmd in commands.Where(c => c.Type == "update"))
            {
                int idx = cmd.LineNumber - 1;
                if (idx >= 0 && idx < importedLines.Count)
                {
                    var imported = importedLines[idx];
                    if (cmd.Fields.TryGetValue("Script", out var script))
                    {
                        imported.Script = string.IsNullOrWhiteSpace(script) ? null : script;
                        imported.IsSilent = string.IsNullOrWhiteSpace(script);
                    }
                    if (cmd.Fields.TryGetValue("Visual", out var visual))
                        imported.Visual = visual;
                    if (cmd.Fields.TryGetValue("Shots", out var shots))
                        imported.Shots = shots;
                    if (cmd.Fields.TryGetValue("ImagePrompt", out var imgPrompt))
                        imported.ImagePrompt = imgPrompt;
                    if (cmd.Fields.TryGetValue("VideoPrompt", out var vidPrompt))
                        imported.VideoPrompt = vidPrompt;
                    
                    // Handle shot-specific prompts: Shot1_ImagePrompt, Shot2_VideoPrompt, etc.
                    foreach (var field in cmd.Fields)
                    {
                        var shotMatch = System.Text.RegularExpressions.Regex.Match(field.Key, @"Shot(\d+)_(Image|Video)Prompt");
                        if (shotMatch.Success)
                        {
                            int shotIndex = int.Parse(shotMatch.Groups[1].Value);
                            bool isImage = shotMatch.Groups[2].Value == "Image";
                            
                            if (!imported.ShotPrompts.ContainsKey(shotIndex))
                                imported.ShotPrompts[shotIndex] = (null, null);
                            
                            var current = imported.ShotPrompts[shotIndex];
                            if (isImage)
                                imported.ShotPrompts[shotIndex] = (field.Value, current.VideoPrompt);
                            else
                                imported.ShotPrompts[shotIndex] = (current.ImagePrompt, field.Value);
                        }
                    }
                }
            }

            // Apply DELETEs (in reverse order to maintain indices)
            foreach (var cmd in commands.Where(c => c.Type == "delete").OrderByDescending(c => c.LineNumber))
            {
                int idx = cmd.LineNumber - 1;
                if (idx >= 0 && idx < importedLines.Count)
                {
                    importedLines.RemoveAt(idx);
                }
            }

            // Apply INSERTs
            foreach (var cmd in commands.Where(c => c.Type == "insert").OrderBy(c => c.LineNumber))
            {
                var newImported = new ImportedLine
                {
                    Script = cmd.Fields.GetValueOrDefault("Script"),
                    Visual = cmd.Fields.GetValueOrDefault("Visual", ""),
                    IsSilent = string.IsNullOrWhiteSpace(cmd.Fields.GetValueOrDefault("Script")),
                    Shots = cmd.Fields.GetValueOrDefault("Shots"),
                    ImagePrompt = cmd.Fields.GetValueOrDefault("ImagePrompt"),
                    VideoPrompt = cmd.Fields.GetValueOrDefault("VideoPrompt")
                };
                
                int insertAt = cmd.LineNumber; // Insert AFTER this position
                if (insertAt >= importedLines.Count)
                    importedLines.Add(newImported);
                else
                    importedLines.Insert(insertAt, newImported);
            }

            // Apply REORDERs
            foreach (var cmd in commands.Where(c => c.Type == "reorder"))
            {
                if (cmd.TargetPosition.HasValue)
                {
                    int from = cmd.LineNumber - 1;
                    int to = cmd.TargetPosition.Value - 1;
                    
                    if (from >= 0 && from < importedLines.Count && to >= 0 && to < importedLines.Count && from != to)
                    {
                        var item = importedLines[from];
                        importedLines.RemoveAt(from);
                        importedLines.Insert(to, item);
                    }
                }
            }

            // Create new draft using the service
            var newDraft = await _storyService.CreateDraftFromImportAsync(
                _currentProject!.Id,
                _auth.CurrentUsername,
                importedLines,
                customName: draftName,
                setAsLatest: false);

            // Get root project ID for loading drafts
            int rootProjectId = _currentProject.ParentProjectId ?? _currentProject.Id;
            
            // Switch to new draft
            await LoadDraftsAsync(rootProjectId, newDraft.Id);

            _loadingOverlay.IsVisible = false;

            // Ask if should set as latest
            bool setAsLatest = await DisplayAlert("Set as Latest?",
                $"Created new draft \"{newDraft.Name}\" with {commands.Count} surgical edits applied.\n\n" +
                "Set this as the 'latest' working draft?",
                "Yes, Set as Latest", "No");
            
            if (setAsLatest)
            {
                await _storyService.SetAsLatestAsync(newDraft.Id);
                await LoadDraftsAsync(rootProjectId, newDraft.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STORY] Surgical edit error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to apply surgical edits: {ex.Message}", "OK");
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    /// <summary>
    /// Two-step flow for importing a draft from an existing video:
    /// 1. Shows prompt to analyze video scene-by-scene
    /// 2. After user gets analysis, shows the import format instructions
    /// </summary>
    private async Task ShowVideoImportFlowAsync()
    {
        // === STEP 1: Video Analysis Prompt ===
        var step1Prompt = @"Analyze this video scene by scene. For each scene give me:
1. Timestamp (start - end)
2. NARRATION: Exactly what is being said word for word. If nothing is said write SILENT.
3. VISUAL: Describe exactly what is shown on screen in detail — characters, actions, objects, locations, camera angles, transitions.
4. MOOD/TONE: The feeling of the scene (tense, comedic, somber, etc.)

Be extremely detailed about the visuals. Describe every character's appearance, clothing, props, and actions. Note any text shown on screen. Note any recurring visual elements or callbacks to earlier scenes. Note transitions between scenes (hard cut, fade, dissolve, etc.).

Do not summarize or skip anything. I need every single moment accounted for even if it is only half a second long.";

        var step1Result = await ShowVideoImportStepAsync(
            stepNumber: 1,
            title: "Step 1: Analyze the Video",
            instructions: "Copy this prompt and give it to any LLM along with your video.\nIt will analyze each scene and give you the narration + visuals.",
            promptText: step1Prompt,
            buttonText: "Next: Get Import Format →",
            showPasteArea: false);

        if (!step1Result) return; // Cancelled

        // === STEP 2: Import Format Instructions ===
        var step2Prompt = @"Now convert the analysis into this exact format for import:

```csharp
// SCENE 1 - [timestamp]
lines[1].Script = ""[exact narration or SILENT]"";
lines[1].Visual = ""[visual description]"";

// SCENE 2 - [timestamp]
lines[2].Script = ""[exact narration or SILENT]"";
lines[2].Visual = ""[visual description]"";

// Continue for all scenes...
```

Rules:
- Use SILENT (no quotes) for lines[X].Script if there's no narration
- Keep the exact narration word-for-word
- Combine the VISUAL and MOOD/TONE into the Visual field
- Number lines sequentially starting at 1
- Include all scenes, even very short ones";

        var step2Result = await ShowVideoImportStepAsync(
            stepNumber: 2,
            title: "Step 2: Get Import Format",
            instructions: "Copy this prompt and give it to the LLM along with the analysis from Step 1.\nThen paste the result below to import.",
            promptText: step2Prompt,
            buttonText: "Import Draft",
            showPasteArea: true);

        // step2Result handled inside ShowVideoImportStepAsync when showPasteArea is true
    }

    private async Task<bool> ShowVideoImportStepAsync(
        int stepNumber,
        string title,
        string instructions,
        string promptText,
        string buttonText,
        bool showPasteArea)
    {
        var tcs = new TaskCompletionSource<bool>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = 550,
            MaximumHeightRequest = 550,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        var mainStack = new VerticalStackLayout { Spacing = 10 };

        // Header with step indicator
        var headerRow = new HorizontalStackLayout { Spacing = 8 };
        headerRow.Children.Add(new Frame
        {
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            CornerRadius = 10,
            Padding = new Thickness(8, 3),
            Content = new Label
            {
                Text = $"Step {stepNumber}/2",
                TextColor = Colors.White,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            }
        });
        headerRow.Children.Add(new Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });
        mainStack.Children.Add(headerRow);

        // Instructions
        mainStack.Children.Add(new Label
        {
            Text = instructions,
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        // Scrollable content area
        var scrollContent = new VerticalStackLayout { Spacing = 8 };

        // Prompt text area (read-only, copyable)
        var promptFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Padding = 10,
            CornerRadius = 8,
            BorderColor = Color.FromArgb("#E0E0E0")
        };

        var promptEditor = new Editor
        {
            Text = promptText,
            FontSize = 11,
            FontFamily = "Consolas",
            HeightRequest = showPasteArea ? 140 : 200,
            IsReadOnly = true,
            BackgroundColor = Colors.Transparent
        };
        promptFrame.Content = promptEditor;
        scrollContent.Children.Add(promptFrame);

        // Copy prompt button
        var copyBtn = new Button
        {
            Text = "📋 Copy Prompt to Clipboard",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 6,
            Padding = new Thickness(12, 6),
            FontSize = 12,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Start
        };
        copyBtn.Clicked += async (s, e) =>
        {
            await Clipboard.SetTextAsync(promptText);
            copyBtn.Text = "✓ Copied!";
            await Task.Delay(1500);
            copyBtn.Text = "📋 Copy Prompt to Clipboard";
        };
        scrollContent.Children.Add(copyBtn);

        // Paste area (only for step 2)
        Editor? pasteEditor = null;
        if (showPasteArea)
        {
            scrollContent.Children.Add(new Label
            {
                Text = "Paste the LLM's response here:",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            pasteEditor = new Editor
            {
                Placeholder = "// SCENE 1\nlines[1].Script = \"...\";\nlines[1].Visual = \"...\";",
                HeightRequest = 100,
                FontSize = 11,
                FontFamily = "Consolas",
                BackgroundColor = Color.FromArgb("#FFFDE7")
            };
            scrollContent.Children.Add(pasteEditor);
        }

        var scrollView = new ScrollView
        {
            Content = scrollContent,
            VerticalOptions = LayoutOptions.FillAndExpand,
            MaximumHeightRequest = showPasteArea ? 340 : 280
        };
        mainStack.Children.Add(scrollView);

        // Button row (always visible at bottom)
        var btnRow = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 6,
            Padding = new Thickness(16, 6),
            HeightRequest = 36,
            FontSize = 13
        };

        var nextBtn = new Button
        {
            Text = buttonText,
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(16, 6),
            HeightRequest = 36,
            FontSize = 13
        };

        cancelBtn.Clicked += (s, e) =>
        {
            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        nextBtn.Clicked += async (s, e) =>
        {
            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);

            if (showPasteArea && pasteEditor != null && !string.IsNullOrWhiteSpace(pasteEditor.Text))
            {
                // Process the import with video flag
                await ProcessVideoImportContentAsync(pasteEditor.Text);
                tcs.TrySetResult(true);
            }
            else if (showPasteArea)
            {
                await DisplayAlert("No Content", "Please paste the LLM response before importing.", "OK");
                tcs.TrySetResult(false);
            }
            else
            {
                tcs.TrySetResult(true);
            }
        };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(nextBtn);
        mainStack.Children.Add(btnRow);

        card.Content = mainStack;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Process import from video - same as regular import but marks all tasks as complete
    /// since the video already exists.
    /// </summary>
    private async Task ProcessVideoImportContentAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlert("Empty", "No content to import.", "OK");
            return;
        }

        try
        {
            // Show loading while parsing
            _loadingLabel.Text = "Parsing video breakdown...";
            _loadingOverlay.IsVisible = true;

            var importedLines = _storyService.ParseMarkdownImport(content);

            _loadingOverlay.IsVisible = false;

            if (importedLines.Count == 0)
            {
                await DisplayAlert("No Lines Found",
                    "Could not parse any lines.\n\nExpected format:\nlines[1].Script = \"text\";\nlines[1].Visual = \"text\";",
                    "OK");
                return;
            }

            // Get next version number for default name
            int nextVersion = await _storyService.GetNextDraftVersionAsync(_currentProject.Id);
            string defaultName = $"Draft v{nextVersion} (Video Import)";

            // Prompt for draft name with default
            string draftName = await DisplayPromptAsync(
                "Name This Draft",
                $"Found {importedLines.Count} scenes from video.\nEdit the name or click Save:",
                accept: "Save",
                cancel: "Cancel",
                initialValue: defaultName,
                placeholder: "Draft name");

            if (string.IsNullOrWhiteSpace(draftName)) return; // Cancelled

            // Ask if this should be set as latest
            bool setAsLatest = await DisplayAlert(
                "Set as Latest?",
                $"Set \"{draftName}\" as the latest working draft?\n\nThis will open by default when you return to this project.",
                "Yes, Set as Latest",
                "No");

            // Show loading while creating draft
            _loadingLabel.Text = $"Creating \"{draftName}\"...";
            _loadingOverlay.IsVisible = true;

            var newDraft = await _storyService.CreateDraftFromImportAsync(
                _currentProject.Id,
                _auth.CurrentUsername,
                importedLines,
                draftName.Trim(),
                setAsLatest);

            // Mark all lines as having completed visuals (since video already exists)
            _loadingLabel.Text = "Marking visuals as complete...";
            await _storyService.MarkAllVisualsCompleteAsync(newDraft.Id);

            // Reload drafts and select the new one
            _loadingLabel.Text = "Loading drafts...";
            var parentId = newDraft.ParentProjectId ?? newDraft.Id;
            await LoadDraftsAsync(parentId, newDraft.Id);

            _loadingOverlay.IsVisible = false;

            await DisplayAlert("Video Draft Imported!",
                $"Created \"{draftName}\" with {importedLines.Count} scenes.\n\n✅ All visuals marked as complete (video already exists)." + 
                (setAsLatest ? "\n\n⭐ Set as latest." : ""),
                "OK");
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            System.Diagnostics.Debug.WriteLine($"[STORY] Video import error: {ex.Message}");
            await DisplayAlert("Import Error", $"Failed to import: {ex.Message}", "OK");
        }
    }

    private async Task<string?> ShowPasteDialogAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = 600,
            HeightRequest = 500,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = "Paste AI Response",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = "Paste the C# code block from the AI:",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var editor = new Editor
        {
            Placeholder = "// NARRATION\nlines[1].Script = \"...\"\nlines[1].Visual = \"...\"",
            HeightRequest = 320,
            FontSize = 13,
            FontFamily = "Consolas",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            AutoSize = EditorAutoSizeOption.Disabled
        };
        stack.Children.Add(editor);

        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };

        var importBtn = new Button
        {
            Text = "Import",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };

        cancelBtn.Clicked += (s, e) =>
        {
            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            tcs.TrySetResult(null);
        };

        importBtn.Clicked += (s, e) =>
        {
            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            tcs.TrySetResult(editor.Text);
        };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(importBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        editor.Focus();

        return await tcs.Task;
    }

    private async Task ProcessImportContentAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlert("Empty", "No content to import.", "OK");
            return;
        }

        try
        {
            // Show loading while parsing
            _loadingLabel.Text = "Parsing...";
            _loadingOverlay.IsVisible = true;

            var importedLines = _storyService.ParseMarkdownImport(content);

            _loadingOverlay.IsVisible = false;

            if (importedLines.Count == 0)
            {
                await DisplayAlert("No Lines Found",
                    "Could not parse any lines.\n\nExpected format:\nlines[1].Script = \"text\";\nlines[1].Visual = \"text\";",
                    "OK");
                return;
            }

            // Get next version number for default name
            int nextVersion = await _storyService.GetNextDraftVersionAsync(_currentProject.Id);
            string defaultName = $"Draft v{nextVersion} (AI)";

            // Prompt for draft name with default
            string draftName = await DisplayPromptAsync(
                "Name This Draft",
                $"Found {importedLines.Count} lines.\nEdit the name or click Save:",
                accept: "Save",
                cancel: "Cancel",
                initialValue: defaultName,
                placeholder: "Draft name");

            if (string.IsNullOrWhiteSpace(draftName)) return; // Cancelled

            // Ask if this should be set as latest
            bool setAsLatest = await DisplayAlert(
                "Set as Latest?",
                $"Set \"{draftName}\" as the latest working draft?\n\nThis will open by default when you return to this project.",
                "Yes, Set as Latest",
                "No");

            // Show loading while creating draft
            _loadingLabel.Text = $"Creating \"{draftName}\"...";
            _loadingOverlay.IsVisible = true;

            var newDraft = await _storyService.CreateDraftFromImportAsync(
                _currentProject.Id,
                _auth.CurrentUsername,
                importedLines,
                draftName.Trim(),
                setAsLatest);

            // Reload drafts and select the new one
            _loadingLabel.Text = "Loading drafts...";
            var parentId = newDraft.ParentProjectId ?? newDraft.Id;
            await LoadDraftsAsync(parentId, newDraft.Id);

            _loadingOverlay.IsVisible = false;

            await DisplayAlert("Draft Imported!",
                $"Created \"{draftName}\" with {importedLines.Count} lines." + (setAsLatest ? "\n\n⭐ Set as latest." : ""),
                "OK");

            // Ask about importing visuals from previous draft
            // Find drafts with prepared visuals
            var draftsWithVisuals = new List<(StoryProject draft, int prepared)>();
            foreach (var draft in _drafts)
            {
                if (draft.Id == newDraft.Id) continue;
                var (total, prepared) = await _storyService.GetProjectStatsAsync(draft.Id);
                if (prepared > 0)
                {
                    draftsWithVisuals.Add((draft, prepared));
                }
            }

            if (draftsWithVisuals.Count > 0)
            {
                // Find the one with most visuals as default suggestion
                var bestSource = draftsWithVisuals.OrderByDescending(d => d.prepared).First();
                string sourceName = bestSource.draft.DraftVersion == 1 ? "Original" : bestSource.draft.Name;

                bool importVisuals = await DisplayAlert(
                    "Import Visuals?",
                    $"Would you like to import prepared visuals from \"{sourceName}\" ({bestSource.prepared} visuals)?\n\nThis will copy visual assets for lines with matching script text.",
                    "Yes, Import",
                    "No");

                if (importVisuals)
                {
                    _loadingLabel.Text = "Importing visuals...";
                    _loadingOverlay.IsVisible = true;

                    int imported = await _storyService.ImportVisualsFromDraftAsync(newDraft.Id, bestSource.draft.Id);

                    _loadingOverlay.IsVisible = false;

                    if (imported > 0)
                    {
                        await DisplayAlert("Visuals Imported", 
                            $"Imported {imported} visual(s) from matching lines.", "OK");
                        await LoadLinesAsync();
                    }
                    else
                    {
                        await DisplayAlert("No Matches", 
                            "No matching lines found. Script text must be similar to import visuals.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _loadingOverlay.IsVisible = false;
            System.Diagnostics.Debug.WriteLine($"[STORY] Import error: {ex.Message}");
            await DisplayAlert("Import Error", $"Failed to import: {ex.Message}", "OK");
        }
    }

    private async Task ShowLineComparisonAsync(int lineOrder)
    {
        if (_currentProject == null || _compareToProject == null) return;

        var currentLine = await _storyService.GetLineByOrderAsync(_currentProject.Id, lineOrder);
        var compareLine = await _storyService.GetLineByOrderAsync(_compareToProject.Id, lineOrder);

        string currentName = _currentProject.DraftVersion == 1 ? "Original" : _currentProject.Name;
        string compareName = _compareToProject.DraftVersion == 1 ? "Original" : _compareToProject.Name;

        // Build comparison display
        var tcs = new TaskCompletionSource<bool>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var card = new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 700,
            MaximumHeightRequest = 600
        };

        var scrollView = new ScrollView();
        var stack = new VerticalStackLayout { Spacing = 16 };

        stack.Children.Add(new Label
        {
            Text = $"Line #{lineOrder} Comparison",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Two-column comparison
        var comparisonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 16,
            RowSpacing = 12
        };

        // Headers
        comparisonGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        comparisonGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        comparisonGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        comparisonGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Left header (compare to)
        var leftHeader = new Label
        {
            Text = $"⬅️ {compareName}",
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0"),
            FontSize = 14
        };
        Grid.SetColumn(leftHeader, 0);
        Grid.SetRow(leftHeader, 0);
        comparisonGrid.Children.Add(leftHeader);

        // Right header (current)
        var rightHeader = new Label
        {
            Text = $"➡️ {currentName} (Current)",
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2"),
            FontSize = 14
        };
        Grid.SetColumn(rightHeader, 1);
        Grid.SetRow(rightHeader, 0);
        comparisonGrid.Children.Add(rightHeader);

        // Script labels
        var leftScriptLabel = new Label { Text = "📝 Script:", FontAttributes = FontAttributes.Bold, FontSize = 12 };
        Grid.SetColumn(leftScriptLabel, 0);
        Grid.SetRow(leftScriptLabel, 1);
        comparisonGrid.Children.Add(leftScriptLabel);

        var rightScriptLabel = new Label { Text = "📝 Script:", FontAttributes = FontAttributes.Bold, FontSize = 12 };
        Grid.SetColumn(rightScriptLabel, 1);
        Grid.SetRow(rightScriptLabel, 1);
        comparisonGrid.Children.Add(rightScriptLabel);

        // Script content
        var leftScriptFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Padding = 10,
            CornerRadius = 6
        };
        leftScriptFrame.Content = new Label
        {
            Text = compareLine?.LineText ?? "(line doesn't exist)",
            FontSize = 13,
            TextColor = compareLine == null ? Color.FromArgb("#999") : Color.FromArgb("#333")
        };
        Grid.SetColumn(leftScriptFrame, 0);
        Grid.SetRow(leftScriptFrame, 2);
        comparisonGrid.Children.Add(leftScriptFrame);

        var rightScriptFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            Padding = 10,
            CornerRadius = 6
        };
        rightScriptFrame.Content = new Label
        {
            Text = currentLine?.LineText ?? "(line doesn't exist)",
            FontSize = 13,
            TextColor = currentLine == null ? Color.FromArgb("#999") : Color.FromArgb("#333")
        };
        Grid.SetColumn(rightScriptFrame, 1);
        Grid.SetRow(rightScriptFrame, 2);
        comparisonGrid.Children.Add(rightScriptFrame);

        // Visual labels
        var leftVisualLabel = new Label { Text = "🎨 Visual:", FontAttributes = FontAttributes.Bold, FontSize = 12 };
        Grid.SetColumn(leftVisualLabel, 0);
        Grid.SetRow(leftVisualLabel, 3);
        comparisonGrid.Children.Add(leftVisualLabel);

        var rightVisualLabel = new Label { Text = "🎨 Visual:", FontAttributes = FontAttributes.Bold, FontSize = 12 };
        Grid.SetColumn(rightVisualLabel, 1);
        Grid.SetRow(rightVisualLabel, 3);
        comparisonGrid.Children.Add(rightVisualLabel);

        // Visual content
        comparisonGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        
        var leftVisualFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Padding = 10,
            CornerRadius = 6
        };
        leftVisualFrame.Content = new Label
        {
            Text = compareLine?.VisualDescription ?? "(line doesn't exist)",
            FontSize = 13,
            TextColor = compareLine == null ? Color.FromArgb("#999") : Color.FromArgb("#333")
        };
        Grid.SetColumn(leftVisualFrame, 0);
        Grid.SetRow(leftVisualFrame, 4);
        comparisonGrid.Children.Add(leftVisualFrame);

        var rightVisualFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            Padding = 10,
            CornerRadius = 6
        };
        rightVisualFrame.Content = new Label
        {
            Text = currentLine?.VisualDescription ?? "(line doesn't exist)",
            FontSize = 13,
            TextColor = currentLine == null ? Color.FromArgb("#999") : Color.FromArgb("#333")
        };
        Grid.SetColumn(rightVisualFrame, 1);
        Grid.SetRow(rightVisualFrame, 4);
        comparisonGrid.Children.Add(rightVisualFrame);

        stack.Children.Add(comparisonGrid);

        // Buttons
        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };
        closeBtn.Clicked += (s, e) =>
        {
            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };
        btnRow.Children.Add(closeBtn);

        // Copy to comparison button (sync the comparison version to match current)
        var copyBtn = new Button
        {
            Text = "📋 Copy Current → " + compareName,
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };
        copyBtn.Clicked += async (s, e) =>
        {
            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            
            await _storyService.CopyLineToComparisonAsync(_currentProject.Id, _compareToProject.Id, lineOrder);
            await LoadLinesAsync(preserveScroll: true);
            tcs.TrySetResult(true);
        };
        btnRow.Children.Add(copyBtn);

        stack.Children.Add(btnRow);

        scrollView.Content = stack;
        card.Content = scrollView;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        await tcs.Task;
    }

    private async Task ShowShotsEditorAsync(StoryLine line)
    {
        var tcs = new TaskCompletionSource<bool>();

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var card = new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 750,
            MaximumHeightRequest = 650
        };

        var mainScroll = new ScrollView();
        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = $"📸 Shot Breakdown - Line #{line.LineOrder}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Visual description context
        var contextFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Padding = 10,
            CornerRadius = 6
        };
        contextFrame.Content = new Label
        {
            Text = $"🎨 {line.VisualDescription}",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        mainStack.Children.Add(contextFrame);

        // Show line-level prompts if they exist (for reference/copying)
        bool hasLinePrompts = !string.IsNullOrEmpty(line.ImagePrompt) || !string.IsNullOrEmpty(line.VideoPrompt);
        if (hasLinePrompts)
        {
            var linePromptsFrame = new Frame
            {
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                Padding = 10,
                CornerRadius = 6,
                BorderColor = Color.FromArgb("#4CAF50")
            };
            var linePromptsStack = new VerticalStackLayout { Spacing = 6 };
            linePromptsStack.Children.Add(new Label
            {
                Text = "📋 Line-Level Prompts (from AI import):",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2E7D32")
            });
            
            if (!string.IsNullOrEmpty(line.ImagePrompt))
            {
                var imgRow = new HorizontalStackLayout { Spacing = 6 };
                imgRow.Children.Add(new Label
                {
                    Text = $"🖼️ {(line.ImagePrompt.Length > 80 ? line.ImagePrompt.Substring(0, 80) + "..." : line.ImagePrompt)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#666"),
                    VerticalOptions = LayoutOptions.Center
                });
                var copyImgLineBtn = new Button
                {
                    Text = "📋 Copy",
                    BackgroundColor = Color.FromArgb("#FFFDE7"),
                    TextColor = Color.FromArgb("#F57F17"),
                    HeightRequest = 26,
                    Padding = new Thickness(8, 2),
                    CornerRadius = 4,
                    FontSize = 10
                };
                copyImgLineBtn.Clicked += async (s, e) =>
                {
                    await Clipboard.SetTextAsync(line.ImagePrompt);
                    copyImgLineBtn.Text = "✓";
                    await Task.Delay(1000);
                    copyImgLineBtn.Text = "📋 Copy";
                };
                imgRow.Children.Add(copyImgLineBtn);
                linePromptsStack.Children.Add(imgRow);
            }
            
            if (!string.IsNullOrEmpty(line.VideoPrompt))
            {
                var vidRow = new HorizontalStackLayout { Spacing = 6 };
                vidRow.Children.Add(new Label
                {
                    Text = $"🎬 {(line.VideoPrompt.Length > 80 ? line.VideoPrompt.Substring(0, 80) + "..." : line.VideoPrompt)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#666"),
                    VerticalOptions = LayoutOptions.Center
                });
                var copyVidLineBtn = new Button
                {
                    Text = "📋 Copy",
                    BackgroundColor = Color.FromArgb("#E3F2FD"),
                    TextColor = Color.FromArgb("#1565C0"),
                    HeightRequest = 26,
                    Padding = new Thickness(8, 2),
                    CornerRadius = 4,
                    FontSize = 10
                };
                copyVidLineBtn.Clicked += async (s, e) =>
                {
                    await Clipboard.SetTextAsync(line.VideoPrompt);
                    copyVidLineBtn.Text = "✓";
                    await Task.Delay(1000);
                    copyVidLineBtn.Text = "📋 Copy";
                };
                vidRow.Children.Add(copyVidLineBtn);
                linePromptsStack.Children.Add(vidRow);
            }
            
            linePromptsFrame.Content = linePromptsStack;
            mainStack.Children.Add(linePromptsFrame);
        }

        // Get current shots
        var shots = _storyService.GetShots(line);
        var shotsContainer = new VerticalStackLayout { Spacing = 8 };

        void RebuildShotsList()
        {
            shotsContainer.Children.Clear();
            shots = _storyService.GetShots(line);

            if (shots.Count == 0)
            {
                // Show simple prompts editor
                shotsContainer.Children.Add(new Label
                {
                    Text = "No shot breakdown yet. Add shots below or use simple prompts:",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#666"),
                    Margin = new Thickness(0, 8)
                });

                // Image prompt
                var imgPromptLabel = new Label { Text = "🖼️ Image Prompt (ChatGPT/DALL-E/Midjourney):", FontSize = 12, FontAttributes = FontAttributes.Bold };
                shotsContainer.Children.Add(imgPromptLabel);

                var imgPromptEditor = new Editor
                {
                    Text = line.ImagePrompt,
                    Placeholder = "Prompt for starting frame image...",
                    HeightRequest = 60,
                    FontSize = 12,
                    BackgroundColor = Color.FromArgb("#FFFDE7")
                };
                imgPromptEditor.TextChanged += (s, e) => line.ImagePrompt = imgPromptEditor.Text;
                shotsContainer.Children.Add(imgPromptEditor);

                // Video prompt
                var vidPromptLabel = new Label { Text = "🎬 Video Prompt (Luma/Runway):", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 8, 0, 0) };
                shotsContainer.Children.Add(vidPromptLabel);

                var vidPromptEditor = new Editor
                {
                    Text = line.VideoPrompt,
                    Placeholder = "Prompt for video generation...",
                    HeightRequest = 60,
                    FontSize = 12,
                    BackgroundColor = Color.FromArgb("#E3F2FD")
                };
                vidPromptEditor.TextChanged += (s, e) => line.VideoPrompt = vidPromptEditor.Text;
                shotsContainer.Children.Add(vidPromptEditor);
            }
            else
            {
                // Show shots list
                foreach (var shot in shots)
                {
                    var shotFrame = new Frame
                    {
                        BackgroundColor = shot.Done ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFF8E1"),
                        Padding = 10,
                        CornerRadius = 8,
                        BorderColor = shot.Done ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FFB300")
                    };

                    var shotStack = new VerticalStackLayout { Spacing = 6 };

                    // Shot header row
                    var shotHeader = new HorizontalStackLayout { Spacing = 8 };
                    shotHeader.Children.Add(new Label
                    {
                        Text = $"Shot {shot.Index}",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333"),
                        VerticalOptions = LayoutOptions.Center
                    });

                    var doneCheck = new CheckBox { IsChecked = shot.Done, Color = Color.FromArgb("#4CAF50") };
                    int shotIdx = shot.Index - 1;
                    doneCheck.CheckedChanged += async (s, e) =>
                    {
                        await _storyService.ToggleShotDoneAsync(line, shotIdx);
                        RebuildShotsList();
                    };
                    shotHeader.Children.Add(doneCheck);
                    shotHeader.Children.Add(new Label
                    {
                        Text = shot.Done ? "Done" : "Pending",
                        FontSize = 11,
                        TextColor = shot.Done ? Color.FromArgb("#4CAF50") : Color.FromArgb("#999"),
                        VerticalOptions = LayoutOptions.Center
                    });

                    var deleteBtn = new Button
                    {
                        Text = "🗑️",
                        BackgroundColor = Color.FromArgb("#FFEBEE"),
                        TextColor = Color.FromArgb("#C62828"),
                        WidthRequest = 30,
                        HeightRequest = 26,
                        Padding = 0,
                        CornerRadius = 4,
                        FontSize = 10
                    };
                    deleteBtn.Clicked += async (s, e) =>
                    {
                        await _storyService.DeleteShotAsync(line, shotIdx);
                        RebuildShotsList();
                    };
                    shotHeader.Children.Add(deleteBtn);

                    shotStack.Children.Add(shotHeader);

                    // Description (smaller, just context)
                    shotStack.Children.Add(new Label
                    {
                        Text = $"📝 {shot.Description}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        Margin = new Thickness(0, 0, 0, 8)
                    });

                    // === TASK 1: Generate Image ===
                    var task1Frame = new Frame
                    {
                        BackgroundColor = shot.Task1_ImageGenerated ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFFDE7"),
                        Padding = 8,
                        CornerRadius = 6,
                        BorderColor = shot.Task1_ImageGenerated ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FFC107")
                    };
                    var task1Stack = new VerticalStackLayout { Spacing = 4 };
                    
                    var task1Header = new HorizontalStackLayout { Spacing = 6 };
                    var task1Check = new CheckBox 
                    { 
                        IsChecked = shot.Task1_ImageGenerated, 
                        Color = Color.FromArgb("#4CAF50"),
                        VerticalOptions = LayoutOptions.Center
                    };
                    task1Check.CheckedChanged += async (s, e) =>
                    {
                        if (task1Check.IsChecked && !shot.Task1_ImageGenerated)
                        {
                            // Just checked - prompt for time
                            await PromptAndLogTaskTimeAsync(line, shotIdx, "image_generated", $"Shot {shotIdx + 1}: Generate image", shot.Description);
                        }
                        shot.Task1_ImageGenerated = task1Check.IsChecked;
                        shot.Done = shot.AllTasksDone;
                        await _storyService.SaveShotsAsync(line, shots);
                        await UpdateTimeProjectionAsync();
                        RebuildShotsList();
                    };
                    task1Header.Children.Add(task1Check);
                    task1Header.Children.Add(new Label
                    {
                        Text = "1️⃣ Generate Starting Image",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = shot.Task1_ImageGenerated ? Color.FromArgb("#4CAF50") : Color.FromArgb("#333"),
                        VerticalOptions = LayoutOptions.Center
                    });
                    task1Stack.Children.Add(task1Header);

                    // Image prompt with copy button
                    var imgPromptRow = new HorizontalStackLayout { Spacing = 6 };
                    var imgPromptEntry = new Entry
                    {
                        Text = shot.ImagePrompt,
                        Placeholder = "ChatGPT/DALL-E prompt for starting frame...",
                        FontSize = 11,
                        HorizontalOptions = LayoutOptions.FillAndExpand
                    };
                    imgPromptEntry.TextChanged += (s, e) => shot.ImagePrompt = imgPromptEntry.Text;
                    imgPromptRow.Children.Add(imgPromptEntry);
                    
                    // Fill from line button (if shot empty but line has prompt)
                    if (string.IsNullOrEmpty(shot.ImagePrompt) && !string.IsNullOrEmpty(line.ImagePrompt))
                    {
                        var fillImgBtn = new Button
                        {
                            Text = "⬇️",
                            BackgroundColor = Color.FromArgb("#FFF3E0"),
                            TextColor = Color.FromArgb("#E65100"),
                            WidthRequest = 36,
                            HeightRequest = 30,
                            Padding = 0,
                            CornerRadius = 4,
                            FontSize = 12
                        };
                        fillImgBtn.Clicked += async (s, e) =>
                        {
                            shot.ImagePrompt = line.ImagePrompt;
                            await _storyService.SaveShotsAsync(line, shots);
                            RebuildShotsList();
                        };
                        imgPromptRow.Children.Add(fillImgBtn);
                    }
                    
                    var copyImgBtn = new Button
                    {
                        Text = "📋",
                        BackgroundColor = Color.FromArgb("#E3F2FD"),
                        TextColor = Color.FromArgb("#1976D2"),
                        WidthRequest = 36,
                        HeightRequest = 30,
                        Padding = 0,
                        CornerRadius = 4,
                        FontSize = 12
                    };
                    copyImgBtn.Clicked += async (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(shot.ImagePrompt))
                        {
                            await Clipboard.SetTextAsync(shot.ImagePrompt);
                            copyImgBtn.Text = "✓";
                            await Task.Delay(1000);
                            copyImgBtn.Text = "📋";
                        }
                    };
                    imgPromptRow.Children.Add(copyImgBtn);
                    task1Stack.Children.Add(imgPromptRow);
                    
                    task1Frame.Content = task1Stack;
                    shotStack.Children.Add(task1Frame);

                    // === TASK 2: Generate Video ===
                    var task2Frame = new Frame
                    {
                        BackgroundColor = shot.Task2_VideoGenerated ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#E3F2FD"),
                        Padding = 8,
                        CornerRadius = 6,
                        BorderColor = shot.Task2_VideoGenerated ? Color.FromArgb("#4CAF50") : Color.FromArgb("#2196F3"),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    var task2Stack = new VerticalStackLayout { Spacing = 4 };
                    
                    var task2Header = new HorizontalStackLayout { Spacing = 6 };
                    var task2Check = new CheckBox 
                    { 
                        IsChecked = shot.Task2_VideoGenerated, 
                        Color = Color.FromArgb("#4CAF50"),
                        VerticalOptions = LayoutOptions.Center
                    };
                    task2Check.CheckedChanged += async (s, e) =>
                    {
                        if (task2Check.IsChecked && !shot.Task2_VideoGenerated)
                        {
                            // Just checked - prompt for time
                            await PromptAndLogTaskTimeAsync(line, shotIdx, "video_generated", $"Shot {shotIdx + 1}: Generate video", shot.Description);
                        }
                        shot.Task2_VideoGenerated = task2Check.IsChecked;
                        shot.Done = shot.AllTasksDone;
                        await _storyService.SaveShotsAsync(line, shots);
                        await UpdateTimeProjectionAsync();
                        RebuildShotsList();
                    };
                    task2Header.Children.Add(task2Check);
                    task2Header.Children.Add(new Label
                    {
                        Text = "2️⃣ Generate Video (Luma/Runway)",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = shot.Task2_VideoGenerated ? Color.FromArgb("#4CAF50") : Color.FromArgb("#333"),
                        VerticalOptions = LayoutOptions.Center
                    });
                    task2Stack.Children.Add(task2Header);

                    // Video prompt with copy button
                    var vidPromptRow = new HorizontalStackLayout { Spacing = 6 };
                    var vidPromptEntry = new Entry
                    {
                        Text = shot.VideoPrompt,
                        Placeholder = "Luma prompt (set image as first frame)...",
                        FontSize = 11,
                        HorizontalOptions = LayoutOptions.FillAndExpand
                    };
                    vidPromptEntry.TextChanged += (s, e) => shot.VideoPrompt = vidPromptEntry.Text;
                    vidPromptRow.Children.Add(vidPromptEntry);
                    
                    var copyVidBtn = new Button
                    {
                        Text = "📋",
                        BackgroundColor = Color.FromArgb("#E3F2FD"),
                        TextColor = Color.FromArgb("#1976D2"),
                        WidthRequest = 36,
                        HeightRequest = 30,
                        Padding = 0,
                        CornerRadius = 4,
                        FontSize = 12
                    };
                    copyVidBtn.Clicked += async (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(shot.VideoPrompt))
                        {
                            await Clipboard.SetTextAsync(shot.VideoPrompt);
                            copyVidBtn.Text = "✓";
                            await Task.Delay(1000);
                            copyVidBtn.Text = "📋";
                        }
                    };
                    vidPromptRow.Children.Add(copyVidBtn);
                    
                    // Fill from line button (if shot empty but line has prompt)
                    if (string.IsNullOrEmpty(shot.VideoPrompt) && !string.IsNullOrEmpty(line.VideoPrompt))
                    {
                        var fillVidBtn = new Button
                        {
                            Text = "⬇️",
                            BackgroundColor = Color.FromArgb("#E3F2FD"),
                            TextColor = Color.FromArgb("#1565C0"),
                            WidthRequest = 36,
                            HeightRequest = 30,
                            Padding = 0,
                            CornerRadius = 4,
                            FontSize = 12
                        };
                        fillVidBtn.Clicked += async (s, e) =>
                        {
                            shot.VideoPrompt = line.VideoPrompt;
                            await _storyService.SaveShotsAsync(line, shots);
                            RebuildShotsList();
                        };
                        vidPromptRow.Children.Add(fillVidBtn);
                    }
                    
                    task2Stack.Children.Add(vidPromptRow);
                    
                    task2Frame.Content = task2Stack;
                    shotStack.Children.Add(task2Frame);

                    shotFrame.Content = shotStack;
                    shotsContainer.Children.Add(shotFrame);
                }
            }
        }

        RebuildShotsList();
        mainStack.Children.Add(shotsContainer);

        // Add shot button
        var addShotRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        var newShotEntry = new Entry
        {
            Placeholder = "New shot description...",
            HorizontalOptions = LayoutOptions.FillAndExpand,
            FontSize = 12
        };
        addShotRow.Children.Add(newShotEntry);

        var addShotBtn = new Button
        {
            Text = "+ Add Shot",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(12, 6),
            FontSize = 12
        };
        addShotBtn.Clicked += async (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(newShotEntry.Text))
            {
                await _storyService.AddShotAsync(line, newShotEntry.Text.Trim());
                newShotEntry.Text = "";
                RebuildShotsList();
            }
        };
        addShotRow.Children.Add(addShotBtn);

        mainStack.Children.Add(addShotRow);

        // Close/Save buttons
        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(20, 8)
        };
        closeBtn.Clicked += async (s, e) =>
        {
            // Save any changes to simple prompts
            if (shots.Count == 0)
            {
                await _storyService.SavePromptsAsync(line, line.ImagePrompt, line.VideoPrompt);
            }
            else
            {
                // Save all shot edits
                await _storyService.SaveShotsAsync(line, shots);
            }

            if (this.Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            
            await LoadLinesAsync(preserveScroll: true);
            tcs.TrySetResult(true);
        };
        btnRow.Children.Add(closeBtn);

        mainStack.Children.Add(btnRow);

        mainScroll.Content = mainStack;
        card.Content = mainScroll;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        await tcs.Task;
    }

    #endregion

    #region Task Time Logging

    /// <summary>
    /// Prompt user for time spent on a task and log it
    /// </summary>
    private async Task<int?> PromptAndLogTaskTimeAsync(StoryLine line, int shotIndex, string taskType, string taskDescription, string shotDesc = "")
    {
        if (_currentProject == null) return null;

        int? minutes = await ShowTimePickerDialogAsync(taskDescription);
        
        if (minutes == null) return null; // Cancelled
        
        // -1 means "I don't know"
        int? logMinutes = minutes == -1 ? null : minutes;

        // Log the task with full context
        await _storyService.LogTaskTimeAsync(
            _auth.CurrentUsername,
            _currentProject.Id,
            line.Id,
            shotIndex,
            taskType,
            logMinutes,
            taskDescription,
            lineText: line.LineText,
            visualDescription: line.VisualDescription,
            shotDescription: shotDesc);

        return logMinutes;
    }

    /// <summary>
    /// Show custom time picker dialog with fixed minute buttons
    /// </summary>
    private Task<int?> ShowTimePickerDialogAsync(string taskDescription)
    {
        var tcs = new TaskCompletionSource<int?>();

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
            WidthRequest = 320,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            HasShadow = true
        };

        var mainStack = new VerticalStackLayout { Spacing = 16 };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "⏱️ How long did this take?",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        mainStack.Children.Add(new Label
        {
            Text = taskDescription,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        // Minutes row
        mainStack.Children.Add(new Label
        {
            Text = "Minutes",
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            Margin = new Thickness(0, 8, 0, 0)
        });

        var minutesRow = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center
        };

        int[] minuteOptions = { 5, 10, 20, 30, 60 };
        foreach (var mins in minuteOptions)
        {
            var btn = new Button
            {
                Text = $"{mins}",
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                FontSize = 14,
                WidthRequest = 50,
                HeightRequest = 40,
                Margin = new Thickness(0, 0, 8, 8),
                CornerRadius = 8,
                Padding = 0
            };
            btn.Clicked += (s, e) =>
            {
                CloseOverlay(overlay);
                tcs.TrySetResult(mins);
            };
            minutesRow.Children.Add(btn);
        }
        mainStack.Children.Add(minutesRow);

        // Hours row
        mainStack.Children.Add(new Label
        {
            Text = "Hours",
            FontSize = 12,
            TextColor = Color.FromArgb("#999")
        });

        var hoursRow = new HorizontalStackLayout { Spacing = 8 };
        int[] hourOptions = { 2, 3, 4, 5 };
        foreach (var hrs in hourOptions)
        {
            var btn = new Button
            {
                Text = $"{hrs}h",
                BackgroundColor = Color.FromArgb("#FFF3E0"),
                TextColor = Color.FromArgb("#E65100"),
                FontSize = 14,
                WidthRequest = 50,
                HeightRequest = 40,
                CornerRadius = 8,
                Padding = 0
            };
            btn.Clicked += (s, e) =>
            {
                CloseOverlay(overlay);
                tcs.TrySetResult(hrs * 60);
            };
            hoursRow.Children.Add(btn);
        }
        mainStack.Children.Add(hoursRow);

        // Custom input row
        mainStack.Children.Add(new Label
        {
            Text = "Custom (minutes)",
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            Margin = new Thickness(0, 8, 0, 0)
        });

        var customRow = new HorizontalStackLayout { Spacing = 8 };
        var customEntry = new Entry
        {
            Placeholder = "e.g. 45",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 100,
            HeightRequest = 40
        };
        customRow.Children.Add(customEntry);

        var customBtn = new Button
        {
            Text = "Set",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontSize = 14,
            WidthRequest = 60,
            HeightRequest = 40,
            CornerRadius = 8,
            Padding = 0
        };
        customBtn.Clicked += (s, e) =>
        {
            if (int.TryParse(customEntry.Text, out int customMins) && customMins > 0)
            {
                CloseOverlay(overlay);
                tcs.TrySetResult(customMins);
            }
        };
        customRow.Children.Add(customBtn);
        mainStack.Children.Add(customRow);

        // Bottom buttons
        var bottomRow = new HorizontalStackLayout 
        { 
            Spacing = 12, 
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var dontKnowBtn = new Button
        {
            Text = "I don't know",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#666"),
            FontSize = 13,
            HeightRequest = 36,
            CornerRadius = 8,
            Padding = new Thickness(12, 0)
        };
        dontKnowBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(-1); // -1 means unknown
        };
        bottomRow.Children.Add(dontKnowBtn);

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#999"),
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        cancelBtn.Clicked += (s, e) =>
        {
            CloseOverlay(overlay);
            tcs.TrySetResult(null);
        };
        bottomRow.Children.Add(cancelBtn);

        mainStack.Children.Add(bottomRow);

        card.Content = mainStack;
        overlay.Children.Add(card);

        if (this.Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }

        return tcs.Task;
    }

    private void CloseOverlay(Grid overlay)
    {
        if (this.Content is Grid pageGrid && pageGrid.Children.Contains(overlay))
        {
            pageGrid.Children.Remove(overlay);
        }
    }

    /// <summary>
    /// Update the time projection display
    /// </summary>
    private async Task UpdateTimeProjectionAsync()
    {
        if (_currentProject == null)
        {
            _projectionLabel.IsVisible = false;
            return;
        }

        try
        {
            var (estimatedMinutes, remainingTasks, breakdown) = 
                await _storyService.GetProjectTimeEstimateAsync(_auth.CurrentUsername, _currentProject.Id);

            if (remainingTasks == 0)
            {
                _projectionLabel.Text = "⏱️ All tasks complete!";
                _projectionLabel.TextColor = Color.FromArgb("#4CAF50");
            }
            else
            {
                string timeStr;
                if (estimatedMinutes < 60)
                {
                    timeStr = $"{estimatedMinutes} min";
                }
                else
                {
                    int hours = estimatedMinutes / 60;
                    int mins = estimatedMinutes % 60;
                    timeStr = mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
                }

                _projectionLabel.Text = $"⏱️ Est. {timeStr} remaining ({remainingTasks} tasks)";
                _projectionLabel.TextColor = Color.FromArgb("#1565C0");
            }
            _projectionLabel.IsVisible = true;
        }
        catch
        {
            _projectionLabel.IsVisible = false;
        }
    }

    #endregion
}
