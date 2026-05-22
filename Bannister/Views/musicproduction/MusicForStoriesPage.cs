using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Bannister.Views;

public class MusicForStoriesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MusicProductionService _musicService;
    private readonly DatabaseService _db;
    private readonly IdeasService _ideas;
    private readonly CustomPromptService _customPrompts;

    private Picker _projectPicker = null!;
    private Picker _draftPicker = null!;
    private Label _currentDraftLabel = null!;
    private Label _statusLabel = null!;
    private VerticalStackLayout _linesContainer = null!;
    private Button _newProjectButton = null!;
    private Button _projectSettingsButton = null!;
    private Button _deleteProjectButton = null!;
    private Button _newDraftButton = null!;
    private Button _renameDraftButton = null!;
    private Button _setLatestButton = null!;
    private Button _compareButton = null!;
    private Button _deleteDraftButton = null!;
    private Button _exportPromptButton = null!;
    private Button _customPromptsButton = null!;
    private Button _exportMusicStateButton = null!;
    private Button _importButton = null!;
    private Frame _musicPlanningFrame = null!;
    private Label _generalMusicDescriptionLabel = null!;
    private Button _askDescriptionButton = null!;
    private Button _writeOwnDescriptionButton = null!;
    private Button _pasteDescriptionButton = null!;
    private Button _askPromptOptionsButton = null!;
    private Button _askMotifBlockPromptsButton = null!;
    private Button _writeOwnPromptButton = null!;
    private Label _timestampStatusLabel = null!;
    private Button _getTranscriptionPromptButton = null!;
    private Button _pasteTimestampsButton = null!;
    private Label _motifDescriptionStatusLabel = null!;
    private Button _describeMotifBlockButton = null!;
    private Button _pasteMotifDescriptionButton = null!;
    private Button _buildFullSoundtrackPromptButton = null!;
    private Button _planRemixStitchButton = null!;
    private Button _saveWorkingPromptTemplateButton = null!;
    private Button _pasteNewTemplateButton = null!;
    private Picker _templatePicker = null!;
    private Button _buildTemplatePromptButton = null!;
    private Button _manageTemplatesButton = null!;
    private Button _addCardButton = null!;
    private Grid _loadingOverlay = null!;
    private Label _loadingOverlayLabel = null!;

    private List<MusicProject> _projects = new();
    private List<MusicProject> _drafts = new();
    private List<MusicLine> _currentLines = new();
    private List<MusicPromptTemplate> _promptTemplates = new();
    private MusicProject? _currentProject;
    private MusicProject? _compareToProject;
    private HashSet<int> _changedLineOrders = new();
    private bool? _pendingTemplateIsTimestamped;
    private bool _isLoadingProjects;
    private bool _isLoadingDrafts;

    private bool IsMaster => !_db.IsReadOnly;

    public MusicForStoriesPage(AuthService auth, MusicProductionService musicService, DatabaseService db, IdeasService ideas, CustomPromptService customPrompts)
    {
        _auth = auth;
        _musicService = musicService;
        _db = db;
        _ideas = ideas;
        _customPrompts = customPrompts;

        Title = "Music for Stories";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProjectsAsync();
    }

    private void BuildUI()
    {
        var pageStack = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = 16
        };

        var topFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var topStack = new VerticalStackLayout { Spacing = 12 };
        topStack.Children.Add(new Label
        {
            Text = "Music for Stories",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3949AB")
        });

        _statusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        topStack.Children.Add(_statusLabel);

        topStack.Children.Add(new Label
        {
            Text = "Project",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });

        var projectRow = new HorizontalStackLayout { Spacing = 8 };
        _projectPicker = new Picker
        {
            Title = "Choose a project...",
            HorizontalOptions = LayoutOptions.FillAndExpand,
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Color.FromArgb("#FFFFFF")
        };
        _projectPicker.SelectedIndexChanged += OnProjectSelected;
        projectRow.Children.Add(_projectPicker);

        _newProjectButton = ActionButton("+ New Project", Color.FromArgb("#3949AB"), Colors.White);
        _newProjectButton.IsVisible = IsMaster;
        _newProjectButton.Clicked += OnNewProjectClicked;
        projectRow.Children.Add(_newProjectButton);

        _projectSettingsButton = ActionButton("Settings", Color.FromArgb("#E0F7FA"), Color.FromArgb("#006064"));
        _projectSettingsButton.IsVisible = false;
        _projectSettingsButton.Clicked += OnProjectSettingsClicked;
        projectRow.Children.Add(_projectSettingsButton);

        _deleteProjectButton = ActionButton("Delete Project", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
        _deleteProjectButton.IsVisible = false;
        _deleteProjectButton.Clicked += OnDeleteProjectClicked;
        projectRow.Children.Add(_deleteProjectButton);

        topStack.Children.Add(projectRow);

        topStack.Children.Add(new Label
        {
            Text = "Draft Version",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });

        var draftRow = new HorizontalStackLayout { Spacing = 8 };
        _draftPicker = new Picker
        {
            Title = "Select draft...",
            WidthRequest = 210,
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            IsVisible = false
        };
        _draftPicker.SelectedIndexChanged += OnDraftSelected;
        draftRow.Children.Add(_draftPicker);

        _newDraftButton = ActionButton("+ New Draft", Color.FromArgb("#3949AB"), Colors.White);
        _newDraftButton.IsVisible = false;
        _newDraftButton.Clicked += OnNewDraftClicked;
        draftRow.Children.Add(_newDraftButton);

        _setLatestButton = ActionButton("Set Latest", Color.FromArgb("#FFF8E1"), Color.FromArgb("#F57F17"));
        _setLatestButton.IsVisible = false;
        _setLatestButton.Clicked += OnSetLatestClicked;
        draftRow.Children.Add(_setLatestButton);

        _compareButton = ActionButton("Compare", Color.FromArgb("#E8EAF6"), Color.FromArgb("#3F51B5"));
        _compareButton.IsVisible = false;
        _compareButton.Clicked += OnCompareClicked;
        draftRow.Children.Add(_compareButton);

        _renameDraftButton = ActionButton("Rename", Color.FromArgb("#FFF3E0"), Color.FromArgb("#E65100"));
        _renameDraftButton.IsVisible = false;
        _renameDraftButton.Clicked += OnRenameDraftClicked;
        draftRow.Children.Add(_renameDraftButton);

        _deleteDraftButton = ActionButton("Delete Draft", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
        _deleteDraftButton.IsVisible = false;
        _deleteDraftButton.Clicked += OnDeleteDraftClicked;
        draftRow.Children.Add(_deleteDraftButton);

        topStack.Children.Add(draftRow);

        var importExportRow = new HorizontalStackLayout { Spacing = 8 };
        _exportPromptButton = ActionButton("Export Prompt", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _exportPromptButton.IsVisible = false;
        _exportPromptButton.Clicked += OnExportPromptClicked;
        importExportRow.Children.Add(_exportPromptButton);

        _customPromptsButton = ActionButton("Custom Prompts", Color.FromArgb("#FFF8E1"), Color.FromArgb("#F57F17"));
        _customPromptsButton.IsVisible = false;
        _customPromptsButton.Clicked += OnCustomPromptsClicked;
        importExportRow.Children.Add(_customPromptsButton);

        _exportMusicStateButton = ActionButton("Export Music State", Color.FromArgb("#F3E5F5"), Color.FromArgb("#6A1B9A"));
        _exportMusicStateButton.IsVisible = false;
        _exportMusicStateButton.Clicked += OnExportMusicStateClicked;
        importExportRow.Children.Add(_exportMusicStateButton);

        _importButton = ActionButton("Import", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _importButton.IsVisible = false;
        _importButton.Clicked += OnImportClicked;
        importExportRow.Children.Add(_importButton);
        topStack.Children.Add(importExportRow);

        _musicPlanningFrame = BuildMusicPlanningSection();
        topStack.Children.Add(_musicPlanningFrame);

        _currentDraftLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3949AB"),
            IsVisible = false
        };
        topStack.Children.Add(_currentDraftLabel);

        var cardHeaderRow = new HorizontalStackLayout { Spacing = 8 };
        cardHeaderRow.Children.Add(new Label
        {
            Text = "Cards",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.StartAndExpand
        });

        _addCardButton = ActionButton("+ Add Card", Color.FromArgb("#3949AB"), Colors.White);
        _addCardButton.IsVisible = false;
        _addCardButton.Clicked += OnAddCardClicked;
        cardHeaderRow.Children.Add(_addCardButton);
        topStack.Children.Add(cardHeaderRow);

        topFrame.Content = topStack;
        pageStack.Children.Add(topFrame);

        _linesContainer = new VerticalStackLayout { Spacing = 10 };
        pageStack.Children.Add(_linesContainer);

        var scroll = new ScrollView { Content = pageStack };
        _loadingOverlayLabel = new Label
        {
            Text = "",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        _loadingOverlay = new Grid
        {
            IsVisible = false,
            InputTransparent = false,
            Children =
            {
                new BoxView
                {
                    Color = Colors.Black,
                    Opacity = 0.6
                },
                new Frame
                {
                    BackgroundColor = Colors.White,
                    CornerRadius = 12,
                    Padding = 24,
                    HasShadow = true,
                    WidthRequest = 300,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 14,
                        Children =
                        {
                            new ActivityIndicator
                            {
                                IsRunning = true,
                                Color = Color.FromArgb("#5B63EE"),
                                WidthRequest = 36,
                                HeightRequest = 36,
                                HorizontalOptions = LayoutOptions.Center
                            },
                            _loadingOverlayLabel
                        }
                    }
                }
            }
        };

        Content = new Grid
        {
            Children =
            {
                scroll,
                _loadingOverlay
            }
        };
    }

    private Frame BuildMusicPlanningSection()
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(new Label
        {
            Text = "Music Planning",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = "Stage 1 - General Music Description",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3949AB")
        });

        _generalMusicDescriptionLabel = new Label
        {
            Text = "No general music description yet.",
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        stack.Children.Add(new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = 10,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            Content = _generalMusicDescriptionLabel
        });

        var stageOneButtons = new HorizontalStackLayout { Spacing = 8 };

        _askDescriptionButton = ActionButton("Ask AI for Description", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _askDescriptionButton.Clicked += OnAskAiForDescriptionClicked;
        _writeOwnDescriptionButton = ActionButton("Write My Own", Color.FromArgb("#E8EAF6"), Color.FromArgb("#3F51B5"));
        _writeOwnDescriptionButton.Clicked += OnWriteOwnDescriptionClicked;
        _pasteDescriptionButton = ActionButton("Paste Description", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _pasteDescriptionButton.Clicked += OnPasteDescriptionClicked;

        foreach (var button in new[] { _askDescriptionButton, _writeOwnDescriptionButton, _pasteDescriptionButton })
        {
            stageOneButtons.Children.Add(button);
        }
        stack.Children.Add(stageOneButtons);

        stack.Children.Add(new Label
        {
            Text = "Stage 2 - How to Prompt for the Music",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3949AB")
        });

        stack.Children.Add(new Label
        {
            Text = "Timestamps",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });

        _timestampStatusLabel = new Label
        {
            Text = "Timestamps: none yet",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        stack.Children.Add(_timestampStatusLabel);

        var timestampButtons = new HorizontalStackLayout { Spacing = 8 };
        _getTranscriptionPromptButton = ActionButton("Get Transcription Prompt", Color.FromArgb("#E0F7FA"), Color.FromArgb("#006064"));
        _getTranscriptionPromptButton.Clicked += OnGetTranscriptionPromptClicked;
        _pasteTimestampsButton = ActionButton("Paste Timestamps", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _pasteTimestampsButton.Clicked += OnPasteTimestampsClicked;
        timestampButtons.Children.Add(_getTranscriptionPromptButton);
        timestampButtons.Children.Add(_pasteTimestampsButton);
        stack.Children.Add(timestampButtons);

        var stageTwoButtons = new HorizontalStackLayout { Spacing = 8 };

        _askPromptOptionsButton = ActionButton("Ask AI for 10 Prompt Options", Color.FromArgb("#FFF8E1"), Color.FromArgb("#F57F17"));
        _askPromptOptionsButton.Clicked += OnAskPromptOptionsClicked;
        _askMotifBlockPromptsButton = ActionButton("Main Motif", Color.FromArgb("#FFF3E0"), Color.FromArgb("#E65100"));
        _askMotifBlockPromptsButton.Clicked += OnAskMotifBlockPromptsClicked;
        _writeOwnPromptButton = ActionButton("Write My Own Music Prompt", Color.FromArgb("#F3E5F5"), Color.FromArgb("#6A1B9A"));
        _writeOwnPromptButton.Clicked += OnWriteOwnPromptClicked;

        foreach (var button in new[] { _askPromptOptionsButton, _askMotifBlockPromptsButton, _writeOwnPromptButton })
        {
            stageTwoButtons.Children.Add(button);
        }
        stack.Children.Add(stageTwoButtons);

        _motifDescriptionStatusLabel = new Label
        {
            Text = "Motif description: none yet",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        stack.Children.Add(_motifDescriptionStatusLabel);

        var motifDescriptionButtons = new HorizontalStackLayout { Spacing = 8 };
        _describeMotifBlockButton = ActionButton("Describe Motif Block", Color.FromArgb("#E0F7FA"), Color.FromArgb("#006064"));
        _describeMotifBlockButton.Clicked += OnDescribeMotifBlockClicked;
        _pasteMotifDescriptionButton = ActionButton("Paste Motif Description", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _pasteMotifDescriptionButton.Clicked += OnPasteMotifDescriptionClicked;
        _buildFullSoundtrackPromptButton = ActionButton("Build Full Soundtrack Prompt", Color.FromArgb("#EDE7F6"), Color.FromArgb("#4527A0"));
        _buildFullSoundtrackPromptButton.Clicked += OnBuildFullSoundtrackPromptClicked;
        _planRemixStitchButton = ActionButton("Plan Remix and Stitch", Color.FromArgb("#E8EAF6"), Color.FromArgb("#303F9F"));
        _planRemixStitchButton.Clicked += OnPlanRemixStitchClicked;
        motifDescriptionButtons.Children.Add(_describeMotifBlockButton);
        motifDescriptionButtons.Children.Add(_pasteMotifDescriptionButton);
        motifDescriptionButtons.Children.Add(_buildFullSoundtrackPromptButton);
        motifDescriptionButtons.Children.Add(_planRemixStitchButton);
        stack.Children.Add(motifDescriptionButtons);

        var templateSaveButtons = new HorizontalStackLayout { Spacing = 8 };
        _saveWorkingPromptTemplateButton = ActionButton("Save a Working Prompt as Template", Color.FromArgb("#E0F2F1"), Color.FromArgb("#00695C"));
        _saveWorkingPromptTemplateButton.Clicked += OnSaveWorkingPromptTemplateClicked;
        _pasteNewTemplateButton = ActionButton("Paste New Template", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _pasteNewTemplateButton.Clicked += OnPasteNewTemplateClicked;
        templateSaveButtons.Children.Add(_saveWorkingPromptTemplateButton);
        templateSaveButtons.Children.Add(_pasteNewTemplateButton);
        stack.Children.Add(templateSaveButtons);

        var templateUseRow = new HorizontalStackLayout { Spacing = 8 };
        _templatePicker = new Picker
        {
            Title = "Use saved template...",
            WidthRequest = 260,
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Colors.White
        };
        templateUseRow.Children.Add(_templatePicker);

        _buildTemplatePromptButton = ActionButton("Build Prompt from Template", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _buildTemplatePromptButton.Clicked += OnBuildTemplatePromptClicked;
        templateUseRow.Children.Add(_buildTemplatePromptButton);

        _manageTemplatesButton = ActionButton("Manage Templates", Color.FromArgb("#ECEFF1"), Color.FromArgb("#333"));
        _manageTemplatesButton.Clicked += OnManageTemplatesClicked;
        templateUseRow.Children.Add(_manageTemplatesButton);
        stack.Children.Add(templateUseRow);

        return new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = Color.FromArgb("#F8F9FF"),
            BorderColor = Color.FromArgb("#DDE2FF"),
            HasShadow = false,
            IsVisible = false,
            Content = stack
        };
    }

    private async Task LoadProjectsAsync(int? selectProjectId = null)
    {
        _isLoadingProjects = true;

        var allProjects = await _musicService.GetActiveProjectsAsync(_auth.CurrentUsername);
        _projects = allProjects
            .Where(p => p.ParentProjectId == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        _projectPicker.Items.Clear();
        foreach (var project in _projects)
        {
            string label = project.Name;
            if (!string.IsNullOrWhiteSpace(project.ProjectCategory))
                label = $"[{project.ProjectCategory}] {label}";
            _projectPicker.Items.Add(label);
        }

        _statusLabel.Text = _db.IsReadOnly
            ? "Secondary mode: projects and drafts are view-only."
            : $"{_projects.Count} project{(_projects.Count == 1 ? "" : "s")}";

        _newProjectButton.IsVisible = IsMaster;

        int targetId = selectProjectId ?? _currentProject?.ParentProjectId ?? _currentProject?.Id ?? -1;
        if (targetId > 0)
        {
            var target = allProjects.FirstOrDefault(p => p.Id == targetId);
            if (target?.ParentProjectId != null)
                targetId = target.ParentProjectId.Value;
        }

        int selectedIndex = _projects.FindIndex(p => p.Id == targetId);
        _isLoadingProjects = false;

        if (selectedIndex >= 0)
            _projectPicker.SelectedIndex = selectedIndex;
        else if (_projects.Count > 0 && _projectPicker.SelectedIndex < 0)
            _projectPicker.SelectedIndex = 0;
        else if (_projects.Count == 0)
            ClearCurrentProject("No music projects yet. Create one to start.");
    }

    private async void OnProjectSelected(object? sender, EventArgs e)
    {
        if (_isLoadingProjects) return;
        if (_projectPicker.SelectedIndex < 0 || _projectPicker.SelectedIndex >= _projects.Count)
        {
            ClearCurrentProject("Choose a project to view its drafts.");
            return;
        }

        var selectedProject = _projects[_projectPicker.SelectedIndex];
        await LoadDraftsAsync(selectedProject.Id);
        await LoadLinesAsync();
    }

    private async Task LoadDraftsAsync(int projectId, int? selectDraftId = null)
    {
        _isLoadingDrafts = true;
        _drafts = await _musicService.GetProjectDraftsAsync(projectId);

        _draftPicker.Items.Clear();
        foreach (var draft in _drafts)
        {
            string label = draft.DraftVersion == 1 ? "Original" : draft.Name;
            if (draft.IsLatest) label += " *";
            _draftPicker.Items.Add(label);
        }

        _draftPicker.IsVisible = _drafts.Count > 0;

        int selectIndex = 0;
        if (selectDraftId.HasValue)
        {
            selectIndex = _drafts.FindIndex(d => d.Id == selectDraftId.Value);
            if (selectIndex < 0) selectIndex = 0;
        }
        else
        {
            selectIndex = _drafts.FindIndex(d => d.IsLatest);
            if (selectIndex < 0) selectIndex = 0;
        }

        _isLoadingDrafts = false;

        if (_drafts.Count > 0)
        {
            _draftPicker.SelectedIndex = selectIndex;
            _currentProject = _drafts[selectIndex];
            UpdateDraftControls();
        }
    }

    private async void OnDraftSelected(object? sender, EventArgs e)
    {
        if (_isLoadingDrafts) return;
        if (_draftPicker.SelectedIndex < 0 || _draftPicker.SelectedIndex >= _drafts.Count) return;

        _currentProject = _drafts[_draftPicker.SelectedIndex];
        UpdateDraftControls();
        await LoadLinesAsync();
    }

    private void UpdateDraftControls()
    {
        if (_currentProject == null)
        {
            _currentDraftLabel.IsVisible = false;
            _projectSettingsButton.IsVisible = false;
            _deleteProjectButton.IsVisible = false;
            _newDraftButton.IsVisible = false;
            _renameDraftButton.IsVisible = false;
            _setLatestButton.IsVisible = false;
            _compareButton.IsVisible = false;
            _deleteDraftButton.IsVisible = false;
            _exportPromptButton.IsVisible = false;
            _customPromptsButton.IsVisible = false;
            _exportMusicStateButton.IsVisible = false;
            _importButton.IsVisible = false;
            _musicPlanningFrame.IsVisible = false;
            _describeMotifBlockButton.IsVisible = false;
            _pasteMotifDescriptionButton.IsVisible = false;
            _buildFullSoundtrackPromptButton.IsVisible = false;
            _planRemixStitchButton.IsVisible = false;
            _addCardButton.IsVisible = false;
            return;
        }

        string display = _currentProject.DraftVersion == 1 ? "Original" : _currentProject.Name;
        if (_currentProject.IsLatest) display += " * (Latest)";
        if (_compareToProject != null)
        {
            string compareName = _compareToProject.DraftVersion == 1 ? "Original" : _compareToProject.Name;
            display += $"\nComparing to: {compareName}";
            if (_changedLineOrders.Count > 0)
                display += $" ({_changedLineOrders.Count} changed)";
        }

        _currentDraftLabel.Text = display;
        _currentDraftLabel.IsVisible = true;

        bool isDraft = _currentProject.DraftVersion > 1;
        _projectSettingsButton.IsVisible = IsMaster;
        _deleteProjectButton.IsVisible = IsMaster;
        _newDraftButton.IsVisible = IsMaster;
        _renameDraftButton.IsVisible = IsMaster && isDraft;
        _deleteDraftButton.IsVisible = IsMaster && isDraft;
        _setLatestButton.IsVisible = IsMaster && _drafts.Count > 1 && !_currentProject.IsLatest;
        _compareButton.IsVisible = IsMaster && _drafts.Count > 1;
        _exportPromptButton.IsVisible = IsMaster;
        _customPromptsButton.IsVisible = IsMaster;
        _exportMusicStateButton.IsVisible = IsMaster;
        _importButton.IsVisible = IsMaster;
        _musicPlanningFrame.IsVisible = true;
        _askDescriptionButton.IsVisible = IsMaster;
        _writeOwnDescriptionButton.IsVisible = IsMaster;
        _pasteDescriptionButton.IsVisible = IsMaster;
        _askPromptOptionsButton.IsVisible = IsMaster;
        _askMotifBlockPromptsButton.IsVisible = IsMaster;
        _writeOwnPromptButton.IsVisible = IsMaster;
        _getTranscriptionPromptButton.IsVisible = IsMaster;
        _pasteTimestampsButton.IsVisible = IsMaster;
        _describeMotifBlockButton.IsVisible = IsMaster;
        _pasteMotifDescriptionButton.IsVisible = IsMaster;
        _buildFullSoundtrackPromptButton.IsVisible = IsMaster;
        _planRemixStitchButton.IsVisible = IsMaster;
        _saveWorkingPromptTemplateButton.IsVisible = IsMaster;
        _pasteNewTemplateButton.IsVisible = IsMaster;
        _buildTemplatePromptButton.IsVisible = IsMaster;
        _manageTemplatesButton.IsVisible = IsMaster;
        _addCardButton.IsVisible = IsMaster;
    }

    private void SetLoadingBusy(bool isBusy, string statusText = "")
    {
        if (_loadingOverlay == null || _loadingOverlayLabel == null)
            return;

        _loadingOverlayLabel.Text = statusText;
        _loadingOverlay.IsVisible = isBusy;
    }

    private async Task LoadLinesAsync()
    {
        _linesContainer.Children.Clear();
        _changedLineOrders.Clear();
        _compareToProject = await _musicService.GetComparisonProjectAsync(_currentProject);

        if (_currentProject == null)
        {
            _linesContainer.Children.Add(InfoLabel("Choose a project to begin."));
            return;
        }

        var lines = await _musicService.GetLinesAsync(_currentProject.Id);
        _currentLines = lines;
        if (_compareToProject != null)
            _changedLineOrders = await _musicService.GetChangedLineOrdersAsync(_currentProject.Id, _compareToProject.Id);

        UpdateDraftControls();
        await RefreshMusicPlanningAsync();
        await RefreshPromptTemplatesAsync();

        string categoryText = string.IsNullOrWhiteSpace(_currentProject.ProjectCategory)
            ? ""
            : $" - {_currentProject.ProjectCategory}";
        _statusLabel.Text = $"{lines.Count} card{(lines.Count == 1 ? "" : "s")}{categoryText}";

        if (lines.Count == 0)
        {
            _linesContainer.Children.Add(InfoLabel(IsMaster ? "No cards yet. Add one to start." : "No cards in this draft."));
            return;
        }

        SetLoadingBusy(true, $"Loading card 0 of {lines.Count}...");
        await Task.Yield();

        try
        {
            for (int i = 0; i < lines.Count; i++)
            {
                SetLoadingBusy(true, $"Loading card {i + 1} of {lines.Count}...");

                var line = lines[i];
                bool isChanged = _changedLineOrders.Contains(line.LineOrder);
                _linesContainer.Children.Add(CreateLineCard(line, lines.Count, isChanged));

                if ((i + 1) % 5 == 0 || i == lines.Count - 1)
                {
                    await Task.Yield();
                    await Task.Delay(1);
                }
            }
        }
        finally
        {
            SetLoadingBusy(false);
        }
    }

    private View CreateLineCard(MusicLine line, int totalLines, bool isChanged)
    {
        var frame = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = isChanged ? Color.FromArgb("#FFF8E1") : Colors.White,
            BorderColor = isChanged ? Color.FromArgb("#FFB300") : Color.FromArgb("#E0E0E0"),
            HasShadow = !isChanged
        };

        var stack = new VerticalStackLayout { Spacing = 10 };
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var title = $"Card {line.LineOrder}" + (isChanged ? " - changed" : "");
        headerGrid.Children.Add(new Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3949AB"),
            VerticalOptions = LayoutOptions.Center
        });

        if (IsMaster)
        {
            var actionRow = new HorizontalStackLayout { Spacing = 6 };
            var upButton = SmallButton("Up", Color.FromArgb("#ECEFF1"), Color.FromArgb("#333"));
            upButton.IsEnabled = line.LineOrder > 1;
            upButton.Clicked += async (s, e) =>
            {
                await _musicService.MoveLineUpAsync(line.Id);
                await LoadLinesAsync();
            };
            actionRow.Children.Add(upButton);

            var downButton = SmallButton("Down", Color.FromArgb("#ECEFF1"), Color.FromArgb("#333"));
            downButton.IsEnabled = line.LineOrder < totalLines;
            downButton.Clicked += async (s, e) =>
            {
                await _musicService.MoveLineDownAsync(line.Id);
                await LoadLinesAsync();
            };
            actionRow.Children.Add(downButton);

            var deleteButton = SmallButton("Delete", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
            deleteButton.Clicked += async (s, e) => await DeleteLineAsync(line);
            actionRow.Children.Add(deleteButton);

            Grid.SetColumn(actionRow, 1);
            headerGrid.Children.Add(actionRow);
        }

        stack.Children.Add(headerGrid);

        var columns = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };

        var musicEditor = CreateColumnEditor("Music", line.Music);
        var scriptEditor = CreateColumnEditor("Script", line.Script);
        var visualsEditor = CreateColumnEditor("Visuals", line.Visuals);

        Grid.SetColumn(musicEditor, 0);
        Grid.SetColumn(scriptEditor, 1);
        Grid.SetColumn(visualsEditor, 2);
        columns.Children.Add(musicEditor);
        columns.Children.Add(scriptEditor);
        columns.Children.Add(visualsEditor);
        stack.Children.Add(columns);

        if (IsMaster)
        {
            var saveButton = SmallButton("Save Card", Color.FromArgb("#3949AB"), Colors.White);
            saveButton.HorizontalOptions = LayoutOptions.End;
            saveButton.Clicked += async (s, e) =>
            {
                line.Music = GetEditorText(musicEditor);
                line.Script = GetEditorText(scriptEditor);
                line.Visuals = GetEditorText(visualsEditor);
                await _musicService.UpdateLineAsync(line);
                await LoadLinesAsync();
            };
            stack.Children.Add(saveButton);
        }

        frame.Content = stack;
        return frame;
    }

    private View CreateColumnEditor(string title, string text)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var editor = new Editor
        {
            Text = text ?? "",
            Placeholder = title,
            HeightRequest = 140,
            AutoSize = EditorAutoSizeOption.Disabled,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White,
            IsReadOnly = !IsMaster
        };

        stack.Children.Add(new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = 4,
            BackgroundColor = Colors.White,
            Content = editor
        });

        return stack;
    }

    private static string GetEditorText(View columnView)
    {
        if (columnView is not VerticalStackLayout stack) return "";
        var border = stack.Children.OfType<Border>().FirstOrDefault();
        return border?.Content is Editor editor ? editor.Text?.Trim() ?? "" : "";
    }

    private async void OnNewProjectClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;

        string? title = await DisplayPromptAsync("New Project", "Project title:", placeholder: "Story soundtrack project");
        if (string.IsNullOrWhiteSpace(title)) return;

        string? category = await DisplayPromptAsync("Category", "Optional category:", placeholder: "series, client, project type");
        var project = await _musicService.CreateProjectAsync(_auth.CurrentUsername, title.Trim(), category?.Trim() ?? "");

        await LoadProjectsAsync(project.Id);
    }

    private async void OnProjectSettingsClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        int rootId = _currentProject.ParentProjectId ?? _currentProject.Id;
        var rootProject = await _musicService.GetProjectByIdAsync(rootId);
        if (rootProject == null) return;

        string? title = await DisplayPromptAsync("Project Title", "Update the project title:", initialValue: rootProject.Name);
        if (title == null) return;

        string? category = await DisplayPromptAsync("Project Category", "Update the project category:", initialValue: rootProject.ProjectCategory);
        if (category == null) return;

        var family = await _musicService.GetProjectDraftsAsync(rootId);
        foreach (var draft in family)
        {
            draft.Name = draft.DraftVersion == 1 ? title.Trim() : draft.Name;
            draft.ProjectCategory = category.Trim();
            await _musicService.UpdateProjectAsync(draft);
        }

        await LoadProjectsAsync(rootId);
    }

    private async void OnDeleteProjectClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        int rootId = _currentProject.ParentProjectId ?? _currentProject.Id;
        var rootProject = await _musicService.GetProjectByIdAsync(rootId);
        if (rootProject == null) return;

        bool confirm = await DisplayAlert(
            "Delete Project",
            $"Delete \"{rootProject.Name}\" and all its drafts/cards?",
            "Delete",
            "Cancel");
        if (!confirm) return;

        await _musicService.DeleteProjectAsync(rootId);
        _currentProject = null;
        await LoadProjectsAsync();
    }

    private async void OnNewDraftClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        int nextVersion = await _musicService.GetNextDraftVersionAsync(_currentProject.Id);
        string defaultName = $"Draft v{nextVersion}";
        string? name = await DisplayPromptAsync(
            "New Draft",
            "Name this draft:",
            "Create",
            "Cancel",
            initialValue: defaultName,
            placeholder: "Draft name");
        if (string.IsNullOrWhiteSpace(name)) return;

        var newDraft = await _musicService.CreateDraftVersionAsync(_currentProject.Id, name.Trim());
        int rootId = newDraft.ParentProjectId ?? newDraft.Id;
        await LoadDraftsAsync(rootId, newDraft.Id);
        await LoadLinesAsync();
    }

    private async void OnExportPromptClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;
        if (_currentProject == null)
        {
            await DisplayAlert("No Project", "Select or create a music project first.", "OK");
            return;
        }

        string? choice = await DisplayActionSheet(
            "Export Options",
            "Cancel",
            null,
            "Convert Story Idea to Import Format");

        if (choice == "Convert Story Idea to Import Format")
            await ExportConvertStoryPromptAsync();
    }

    private async void OnCustomPromptsClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;

        var prompts = await LoadCustomPromptsAsync();
        var options = new List<string> { "+ Add Custom Prompt" };
        options.AddRange(prompts.Select(p => p.Title));

        string? choice = await DisplayActionSheet(
            "Custom Prompts",
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
            return;

        if (choice == "+ Add Custom Prompt")
        {
            await AddCustomPromptAsync();
            return;
        }

        var selected = prompts.FirstOrDefault(p => p.Title == choice);
        if (selected != null)
            await ShowCustomPromptActionsAsync(selected);
    }

    private async Task AddCustomPromptAsync()
    {
        string? title = await DisplayPromptAsync(
            "New Custom Prompt",
            "Title shown in the custom prompt menu:",
            "Next",
            "Cancel",
            placeholder: "Prompt title...");

        if (string.IsNullOrWhiteSpace(title))
            return;

        string? text = await ShowMultiLineInputAsync(
            "Custom Prompt",
            "Prompt text:",
            "",
            "Paste or write the custom prompt...");

        if (string.IsNullOrWhiteSpace(text))
            return;

        var prompts = await LoadCustomPromptsAsync();
        string finalTitle = GetUniqueCustomPromptTitle(prompts, title.Trim());
        var prompt = await _customPrompts.AddCustomPromptAsync(_auth.CurrentUsername, "music", finalTitle, text.Trim());

        await DisplayAlert("Saved", $"Custom prompt \"{finalTitle}\" saved.", "OK");
        await _ideas.CreateIdeaAsync(_auth.CurrentUsername, prompt.Text, "music_custom_prompts", fullIdea: prompt.Text);
    }

    private async Task ShowCustomPromptActionsAsync(CustomPromptItem prompt)
    {
        string? choice = await DisplayActionSheet(
            prompt.Title,
            "Cancel",
            null,
            "Copy Prompt",
            "View Prompt",
            "Edit Prompt",
            "Delete Prompt",
            "Log As Idea");

        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
            return;

        switch (choice)
        {
            case "Copy Prompt":
                await Clipboard.SetTextAsync(prompt.Text);
                await DisplayAlert("Copied", $"\"{prompt.Title}\" copied to clipboard.", "OK");
                break;
            case "View Prompt":
                await DisplayAlert(prompt.Title, prompt.Text, "OK");
                break;
            case "Edit Prompt":
                await EditCustomPromptAsync(prompt);
                break;
            case "Delete Prompt":
                await DeleteCustomPromptAsync(prompt);
                break;
            case "Log As Idea":
                await _ideas.CreateIdeaAsync(_auth.CurrentUsername, prompt.Text, "music_custom_prompts", fullIdea: prompt.Text);
                break;
        }
    }

    private async Task EditCustomPromptAsync(CustomPromptItem prompt)
    {
        string? newTitle = await DisplayPromptAsync(
            "Edit Custom Prompt",
            "Title shown in the custom prompt menu:",
            "Next",
            "Cancel",
            initialValue: prompt.Title);

        if (string.IsNullOrWhiteSpace(newTitle))
            return;

        string? newText = await ShowMultiLineInputAsync(
            "Edit Custom Prompt",
            "Prompt text:",
            prompt.Text,
            "Prompt text...");

        if (string.IsNullOrWhiteSpace(newText))
            return;

        var prompts = await LoadCustomPromptsAsync();
        prompt.Title = GetUniqueCustomPromptTitle(prompts.Where(p => p.Id != prompt.Id).ToList(), newTitle.Trim());
        prompt.Text = newText.Trim();
        await _customPrompts.UpdateCustomPromptAsync(prompt);
    }

    private async Task DeleteCustomPromptAsync(CustomPromptItem prompt)
    {
        bool delete = await DisplayAlert(
            "Delete Custom Prompt?",
            $"Delete \"{prompt.Title}\"?",
            "Delete",
            "Cancel");

        if (!delete)
            return;

        await _customPrompts.DeleteCustomPromptAsync(prompt.Id);
    }

    private Task<List<CustomPromptItem>> LoadCustomPromptsAsync() =>
        _customPrompts.GetCustomPromptsAsync(_auth.CurrentUsername, "music");

    private static string GetUniqueCustomPromptTitle(List<CustomPromptItem> prompts, string title)
    {
        string baseTitle = string.IsNullOrWhiteSpace(title) ? "Custom Prompt" : title.Trim();
        string candidate = baseTitle;
        int suffix = 2;

        while (prompts.Any(p => string.Equals(p.Title, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseTitle} ({suffix})";
            suffix++;
        }

        return candidate;
    }

    private async Task ExportConvertStoryPromptAsync()
    {
        string? rawStory = await ShowMultiLineInputAsync(
            "Convert Story Idea",
            "Paste or write your story idea/script in any format.\nA prompt will be generated that you can give to any AI to convert it to the import format.",
            "",
            "Opening scene...\nNarration...\nVisual beat...");

        if (string.IsNullOrWhiteSpace(rawStory)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("I have a story/script that I need converted into a specific C# import format for my Music for Stories tool.");
        sb.AppendLine("Below is the raw story. Convert it into the exact format shown, preserving all the content.");
        sb.AppendLine();
        sb.AppendLine("=== RAW STORY ===");
        sb.AppendLine(rawStory);
        sb.AppendLine("=== END RAW STORY ===");
        sb.AppendLine();
        sb.AppendLine(MusicPromptTemplates.GetDraftFormatInstructions());
        sb.AppendLine();
        sb.AppendLine("ADDITIONAL INSTRUCTIONS:");
        sb.AppendLine("- Fill Script and Visual only");
        sb.AppendLine("- Do not generate cue, DNA, variation, rhythm, layer, or music-planning fields");
        sb.AppendLine("- Output ONLY the code block, no other text");

        await Clipboard.SetTextAsync(sb.ToString());

        await DisplayAlert(
            "Prompt Copied!",
            "The conversion prompt has been copied to your clipboard.\n\n" +
            "Steps:\n" +
            "1. Paste this prompt to any AI (ChatGPT, Claude, etc.)\n" +
            "2. The AI will output Script and Visual lines in the import format\n" +
            "3. Copy the AI's output\n" +
            "4. Use Import -> Paste from Clipboard to import",
            "OK");
    }

    private async Task RefreshMusicPlanningAsync()
    {
        if (_currentProject == null || _generalMusicDescriptionLabel == null) return;

        var description = await _musicService.GetGeneralMusicDescriptionAsync(_currentProject.Id);
        _generalMusicDescriptionLabel.Text = string.IsNullOrWhiteSpace(description)
            ? "No general music description yet."
            : description.Trim();

        if (_timestampStatusLabel != null)
        {
            var timestampedNarration = await _musicService.GetTimestampedNarrationAsync(_currentProject.Id);
            _timestampStatusLabel.Text = string.IsNullOrWhiteSpace(timestampedNarration)
                ? "Timestamps: none yet"
                : "Timestamps: saved";
            _timestampStatusLabel.TextColor = string.IsNullOrWhiteSpace(timestampedNarration)
                ? Color.FromArgb("#666")
                : Color.FromArgb("#2E7D32");
        }

        if (_motifDescriptionStatusLabel != null)
        {
            var motifDescription = await _musicService.GetMotifDescriptionAsync(_currentProject.Id);
            _motifDescriptionStatusLabel.Text = string.IsNullOrWhiteSpace(motifDescription)
                ? "Motif description: none yet"
                : "Motif description: saved";
            _motifDescriptionStatusLabel.TextColor = string.IsNullOrWhiteSpace(motifDescription)
                ? Color.FromArgb("#666")
                : Color.FromArgb("#2E7D32");
        }
    }

    private async Task RefreshPromptTemplatesAsync(int? selectTemplateId = null)
    {
        if (_templatePicker == null) return;

        _promptTemplates = await _musicService.GetPromptTemplatesAsync(_auth.CurrentUsername);
        _templatePicker.Items.Clear();

        foreach (var template in _promptTemplates)
        {
            var label = template.IsTimestamped
                ? $"{template.Name} (timestamped)"
                : template.Name;
            _templatePicker.Items.Add(label);
        }

        if (selectTemplateId.HasValue)
        {
            int index = _promptTemplates.FindIndex(t => t.Id == selectTemplateId.Value);
            _templatePicker.SelectedIndex = index;
        }
        else if (_templatePicker.SelectedIndex >= _promptTemplates.Count)
        {
            _templatePicker.SelectedIndex = -1;
        }
    }

    private async Task<string> BuildCurrentScriptTextAsync()
    {
        if (_currentProject == null) return "";

        var lines = _currentLines.Count > 0
            ? _currentLines
            : await _musicService.GetLinesAsync(_currentProject.Id);

        return string.Join(Environment.NewLine, lines
            .OrderBy(l => l.LineOrder)
            .Select(l => l.Script?.Trim() ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private async Task<string> GetGeneralMusicDescriptionTextAsync()
    {
        if (_currentProject == null) return "";
        return (await _musicService.GetGeneralMusicDescriptionAsync(_currentProject.Id)).Trim();
    }

    private async Task CopyPlanningPromptAsync(string prompt, string title, string message)
    {
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert(title, message, "OK");
    }

    private async void OnAskAiForDescriptionClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var script = await BuildCurrentScriptTextAsync();
        if (string.IsNullOrWhiteSpace(script))
        {
            await DisplayAlert("No Script", "Add script lines to the current draft before asking for a music description.", "OK");
            return;
        }

        var prompt =
            "I am planning music for a short-video soundtrack.\n" +
            "Read the script below and write a general music description for the whole piece.\n" +
            "Focus on mood, instrumentation, overall feel, pacing, and how the music should support the story.\n" +
            "Output only the description as prose.\n\n" +
            "SCRIPT:\n" +
            script;

        await CopyPlanningPromptAsync(
            prompt,
            "Prompt Copied",
            "Paste this into your LLM. Copy its answer, then use Paste Description to save it to this project.");
    }

    private async void OnWriteOwnDescriptionClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? userText = await ShowMultiLineInputAsync(
            "Write Music Direction",
            "Describe, in your own words, what kind of music you want. A wrapper prompt will be copied for your LLM to refine it against the script.",
            "",
            "I want the music to feel...");
        if (string.IsNullOrWhiteSpace(userText)) return;

        var script = await BuildCurrentScriptTextAsync();
        var prompt =
            "I am planning music for a short-video soundtrack.\n" +
            "The user described the music they want. Refine their words into a clear general music description for this project.\n" +
            "Output only the refined description as prose.\n\n" +
            "USER MUSIC DIRECTION:\n" +
            userText.Trim() + "\n\n" +
            "SCRIPT:\n" +
            script;

        await CopyPlanningPromptAsync(
            prompt,
            "Prompt Copied",
            "Paste this into your LLM. Copy its refined description, then use Paste Description to save it.");
    }

    private async void OnPasteDescriptionClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? description = await ShowPasteDialogAsync(
            "Paste Description",
            "Paste the general music description returned by your LLM:",
            "A sparse, tense soundtrack built around...",
            "Save");
        if (string.IsNullOrWhiteSpace(description)) return;

        await _musicService.SetGeneralMusicDescriptionAsync(_currentProject.Id, description.Trim());
        await RefreshMusicPlanningAsync();
        await DisplayAlert("Description Saved", "The general music description was saved to the project.", "OK");
    }

    private async void OnAskPromptOptionsClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var description = await GetGeneralMusicDescriptionTextAsync();
        if (string.IsNullOrWhiteSpace(description))
        {
            await DisplayAlert("Description Needed", "Do Stage 1 first: save a general music description before asking for prompt options.", "OK");
            return;
        }

        var script = await BuildCurrentScriptTextAsync();
        var prompt =
            "I am planning prompts for an AI music generator such as Suno or ElevenLabs for a short-video soundtrack.\n" +
            "Using the script and general music description below, produce 10 distinct, numbered, ready-to-use music-generation prompt options.\n" +
            "Output only the 10 numbered prompts.\n\n" +
            "GENERAL MUSIC DESCRIPTION:\n" +
            description + "\n\n" +
            "SCRIPT:\n" +
            script;

        await CopyPlanningPromptAsync(
            prompt,
            "Prompt Copied",
            "Paste this into your LLM to get 10 ready-to-use music prompt options.");
    }

    private async void OnAskMotifBlockPromptsClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var script = await BuildCurrentScriptTextAsync();
        if (string.IsNullOrWhiteSpace(script))
        {
            await DisplayAlert("No Script", "Add script lines to the current draft before asking for motif block prompt options.", "OK");
            return;
        }

        var description = await GetGeneralMusicDescriptionTextAsync();
        var timestamps = await _musicService.GetTimestampedNarrationAsync(_currentProject.Id);
        var prompt =
            "I am planning prompts for an AI music generator such as Suno or ElevenLabs for a short-video soundtrack.\n" +
            "Produce 10 distinct, numbered, ready-to-use ElevenLabs/Suno prompt options for generating ONLY the soundtrack's MAIN MOTIF.\n" +
            "The main motif is the foundational, recurring musical idea the rest of the soundtrack will be built from.\n" +
            "This is just the motif as a focused standalone block, not the whole timestamped soundtrack or full emotional arc.\n" +
            "Leave the exact length to your own judgment; do not specify a duration.\n" +
            "Use the general music description, script, and timestamps only as context for the motif's emotional character and fit.\n" +
            "Output ONLY the 10 numbered prompts. Each numbered item must be a complete ready-to-paste ElevenLabs/Suno prompt for the motif block, with no commentary.\n\n" +
            "GENERAL MUSIC DESCRIPTION:\n" +
            (string.IsNullOrWhiteSpace(description) ? "(none provided)" : description) + "\n\n" +
            "SCRIPT:\n" +
            script + "\n\n" +
            "TIMESTAMPS:\n" +
            (string.IsNullOrWhiteSpace(timestamps) ? "(no timestamps saved)" : timestamps.Trim());

        await CopyPlanningPromptAsync(
            prompt,
            "Motif Prompt Copied",
            "Paste this into your LLM to get 10 ready-to-use main motif block prompt options.");
    }

    private async void OnDescribeMotifBlockClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        const string prompt =
            "Analyze the provided main motif audio block for a short-video soundtrack.\n" +
            "Output a detailed musical description only, with no commentary about the task.\n\n" +
            "Include these sections:\n" +
            "1. Overall mood and emotional color.\n" +
            "2. Musical identity: key or tonal center if detectable, tempo/BPM if detectable, rhythmic feel, groove, and pacing.\n" +
            "3. Instrumentation and sound palette: lead instruments, supporting textures, synth/acoustic qualities, percussion, bass, ambience, and production character.\n" +
            "4. Structure by timestamp: section-by-section description of what changes over the motif block, using timestamps from the audio.\n" +
            "5. Best suited for: what kind of story moment, emotional use, or soundtrack role this motif supports.\n" +
            "6. Best-description summary: a concise paragraph that captures the motif's reusable musical identity.\n\n" +
            "Analyze the provided audio directly and output only the detailed description.";

        await CopyPlanningPromptAsync(
            prompt,
            "Motif Description Prompt Copied",
            "Take this prompt plus your motif audio block to an audio-capable LLM. Copy the analysis it returns, then use Paste Motif Description to store it on this draft.");
    }

    private async void OnPasteMotifDescriptionClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? description = await ShowPasteDialogAsync(
            "Paste Motif Description",
            "Paste the detailed motif-block description returned by your audio-capable LLM. This saves to the current draft only.",
            "Overall mood and emotional color...\nMusical identity...\nStructure by timestamp...",
            "Save");
        if (string.IsNullOrWhiteSpace(description)) return;

        await _musicService.SetMotifDescriptionAsync(_currentProject.Id, description.Trim());
        _currentProject = await _musicService.GetProjectByIdAsync(_currentProject.Id) ?? _currentProject;
        await RefreshMusicPlanningAsync();
        await DisplayAlert("Motif Description Saved", "The motif description was saved to this draft.", "OK");
    }

    private async void OnBuildFullSoundtrackPromptClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var motifDescription = await _musicService.GetMotifDescriptionAsync(_currentProject.Id);
        if (string.IsNullOrWhiteSpace(motifDescription))
        {
            await DisplayAlert(
                "Motif Description Needed",
                "Describe the motif block first (Describe Motif Block -> Paste Motif Description) so the soundtrack can be built around it.",
                "OK");
            return;
        }

        var timestamps = await _musicService.GetTimestampedNarrationAsync(_currentProject.Id);
        if (string.IsNullOrWhiteSpace(timestamps))
        {
            await DisplayAlert(
                "Timestamps Needed",
                "Add timestamps first (Get Transcription Prompt -> Paste Timestamps) so the soundtrack can be mapped to the narration and timing.",
                "OK");
            return;
        }

        var prompt =
            "I am preparing a prompt for an AI music generator such as Suno or ElevenLabs.\n" +
            "Produce ONE final, complete, ready-to-paste music-generation prompt for the ENTIRE soundtrack of this short-video story.\n" +
            "Do not produce a list, alternatives, or options.\n\n" +
            "Build the whole piece around the provided MOTIF DESCRIPTION as the single main recurring motif and the ONLY source of musical identity, including instrumentation, key/tonal feel, tempo, and mood. Do not introduce instrumentation or stylistic elements that are not implied by the motif description; keep the established identity consistent.\n" +
            "Map the soundtrack's progression onto the provided TIMESTAMPS section by section. Let the motif transform darker, fuller, sparser, tender, or otherwise as each timestamp's narration demands, while always remaining recognizably the same motif. Infer total duration from the last timestamp.\n" +
            "Open on the motif, return to it, transform it as the timestamped narration demands, and end as the final timestamp implies.\n" +
            "Output ONLY the final soundtrack prompt, with no commentary.\n\n" +
            "MOTIF DESCRIPTION:\n" +
            motifDescription.Trim() + "\n\n" +
            "TIMESTAMPS:\n" +
            timestamps.Trim();

        await CopyPlanningPromptAsync(
            prompt,
            "Full Soundtrack Meta-Prompt Copied",
            "Paste this into your LLM to get a full-soundtrack prompt built around your motif. Copy its output into Suno or ElevenLabs.");
    }

    private async void OnPlanRemixStitchClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var motifDescription = await _musicService.GetMotifDescriptionAsync(_currentProject.Id);
        if (string.IsNullOrWhiteSpace(motifDescription))
        {
            await DisplayAlert(
                "Motif Description Needed",
                "Describe the motif block first (Describe Motif Block -> Paste Motif Description).",
                "OK");
            return;
        }

        var timestamps = await _musicService.GetTimestampedNarrationAsync(_currentProject.Id);
        if (string.IsNullOrWhiteSpace(timestamps))
        {
            await DisplayAlert(
                "Timestamps Needed",
                "Add timestamps first (Get Transcription Prompt -> Paste Timestamps).",
                "OK");
            return;
        }

        var prompt =
            "You are planning a remix-and-stitch assembly for a short-video soundtrack.\n" +
            "The user already HAS a real main motif audio block, described in MOTIF DESCRIPTION, and will assemble the final soundtrack in audio software by placing copies or cuts of this motif and generating connecting pieces through Suno's remix feature.\n" +
            "You are NOT writing one music-generation prompt. Produce a PLACEMENT PLAN plus Suno remix prompts for the connecting gaps.\n\n" +
            "Read the motif's length and internal structure from MOTIF DESCRIPTION. Its structure-by-timestamp indicates how long the motif is and what happens inside it.\n" +
            "Plan against the story timeline in TIMESTAMPS. The last timestamp gives the total duration.\n\n" +
            "First, produce a PLACEMENT PLAN across the timestamped story. Specify where the actual motif audio should be placed, using time ranges and story beats from TIMESTAMPS.\n" +
            "For each placement, specify how to cut the motif: trims, which portion to use, loops, shortened versions, or whether to use the full motif, plus the target time region and target length. The motif does not have to be used in full every time.\n" +
            "Tie each placement to the narration moment it supports, such as a full opening statement, a trimmed darker return, a sparse short version, or a tender ending.\n\n" +
            "Second, identify every gap between motif placements that needs connecting material.\n" +
            "For EACH gap, produce a self-contained SUNO REMIX PROMPT under 1000 characters. Each remix prompt must explain how to remix or extend the motif into connecting music for that specific gap while matching the motif's established instrumentation, key/tonal feel, tempo, and mood so the stitched soundtrack feels continuous.\n\n" +
            "Lay out the answer clearly with the placement plan first, then the connecting remix prompts. Label each placement and each gap with its time region.\n" +
            "Keep every Suno remix prompt under 1000 characters.\n" +
            "Output the plan and the prompts only, with no extra commentary.\n\n" +
            "MOTIF DESCRIPTION:\n" +
            motifDescription.Trim() + "\n\n" +
            "TIMESTAMPS:\n" +
            timestamps.Trim();

        await CopyPlanningPromptAsync(
            prompt,
            "Remix Stitch Plan Copied",
            "Paste this into your LLM to get a placement plan and Suno remix prompts. Use them to place the motif and generate connecting pieces in your music software.");
    }

    private async void OnWriteOwnPromptClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? userPrompt = await ShowMultiLineInputAsync(
            "Write Music Prompt",
            "Write the music prompt you want in your own words. A wrapper prompt will be copied for your LLM to refine it.",
            "",
            "Make a short instrumental prompt that...");
        if (string.IsNullOrWhiteSpace(userPrompt)) return;

        var description = await GetGeneralMusicDescriptionTextAsync();
        var script = await BuildCurrentScriptTextAsync();
        var prompt =
            "I am preparing an AI music-generation prompt for Suno or ElevenLabs for a short-video soundtrack.\n" +
            "Refine the user's draft into a clear, ready-to-use music-generation prompt.\n" +
            "Output only the improved prompt text, with no commentary.\n\n" +
            "USER'S DRAFT MUSIC PROMPT:\n" +
            userPrompt.Trim() + "\n\n";

        if (!string.IsNullOrWhiteSpace(description))
        {
            prompt += "GENERAL MUSIC DESCRIPTION:\n" +
                description + "\n\n";
        }

        prompt += "SCRIPT:\n" + script;

        await CopyPlanningPromptAsync(
            prompt,
            "Prompt Copied",
            "Paste this into your LLM to refine your music-generation prompt.");
    }

    private async void OnGetTranscriptionPromptClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        const string prompt =
            "Transcribe this audio file and produce a timestamped narration script. For each line or natural phrase break, give me:\n" +
            "Timestamp (start – end) — precise to the second (or finer if needed for short beats).\n" +
            "NARRATION: Exactly what is said, word for word. Mark pauses, breaths, or silences explicitly (e.g. [pause 2s], [breath], [silent 3s]).\n" +
            "Break a new line at every natural pause, sentence end, or shift in delivery. Account for every second of the audio's runtime (~2:58 total) — including any silence at the start, between lines, or at the end. Do not summarize, paraphrase, or skip anything.\n" +
            "At the end, give the total runtime and confirm full coverage.";

        await CopyPlanningPromptAsync(
            prompt,
            "Transcription Prompt Copied",
            "Take this prompt plus your narration audio to an audio-capable LLM. Copy the timestamped narration it returns, then use Paste Timestamps to save it to this draft.");
    }

    private async void OnPasteTimestampsClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? timestamps = await ShowPasteDialogAsync(
            "Paste Timestamps",
            "Paste the timestamped narration returned by your audio-capable LLM. This saves to the current draft only.",
            "00:00 - 00:03 NARRATION: ...\n00:03 - 00:05 NARRATION: [pause 2s]",
            "Save");
        if (string.IsNullOrWhiteSpace(timestamps)) return;

        await _musicService.SetTimestampedNarrationAsync(_currentProject.Id, timestamps.Trim());
        _currentProject = await _musicService.GetProjectByIdAsync(_currentProject.Id) ?? _currentProject;
        await RefreshMusicPlanningAsync();
        await DisplayAlert("Timestamps Saved", "The timestamped narration was saved to this draft.", "OK");
    }

    private async void OnSaveWorkingPromptTemplateClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? workingPrompt = await ShowMultiLineInputAsync(
            "Save Working Prompt",
            "Paste a Suno/ElevenLabs prompt that worked. A meta-prompt will be copied so your LLM can generalize it into a reusable template.",
            "",
            "Paste the working music prompt here...");
        if (string.IsNullOrWhiteSpace(workingPrompt)) return;

        string trimmedWorkingPrompt = workingPrompt.Trim();
        try
        {
            await _ideas.CreateIdeaAsync(
                _auth.CurrentUsername,
                "Music prompt " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                "Music Prompts",
                fullIdea: trimmedWorkingPrompt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Failed to log working music prompt as idea: " + ex);
        }

        string? timestampChoice = await DisplayActionSheet(
            "Does this prompt use timestamps?",
            "Cancel",
            null,
            "No",
            "Yes");
        if (timestampChoice == null || timestampChoice == "Cancel") return;

        bool isTimestamped = timestampChoice == "Yes";
        _pendingTemplateIsTimestamped = isTimestamped;

        var metaPrompt = BuildTemplateExtractionPrompt(trimmedWorkingPrompt, isTimestamped);
        await CopyPlanningPromptAsync(
            metaPrompt,
            "Template Extraction Prompt Copied",
            "Paste this into your LLM. Copy the generalized template it returns, then use Paste New Template to save it.");
    }

    private static string BuildTemplateExtractionPrompt(string workingPrompt, bool isTimestamped)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("I have a music-generation prompt that worked well for Suno/ElevenLabs.");
        sb.AppendLine("Generalize it into a reusable, script-agnostic template for future short-video soundtracks.");
        sb.AppendLine();
        sb.AppendLine("Preserve the structural DNA of the original prompt:");
        sb.AppendLine("- Keep its instrumentation choices and mood vocabulary");
        sb.AppendLine("- Preserve any immediate hook / no slow build-up instruction if present");
        sb.AppendLine("- Preserve cyclical or recurring-motif structure if present");
        sb.AppendLine("- Preserve any darker-each-time-it-returns idea if present");
        sb.AppendLine("- Preserve any unresolved or specific ending instruction if present");
        sb.AppendLine();
        sb.AppendLine("Remove story-specific content: specific characters, objects, places, and plot beats.");
        sb.AppendLine("Replace the place where the story drives the music with the literal placeholder {SCRIPT}.");
        sb.AppendLine("Use {DESCRIPTION} where a general music description would help.");

        if (isTimestamped)
        {
            sb.AppendLine("This is a TIMESTAMPED template. Preserve the timestamped-progression structure conceptually.");
            sb.AppendLine("Express the time-mapped section as an instruction to map the music's emotional progression onto provided timestamps using the literal placeholder {TIMESTAMPS}.");
            sb.AppendLine("The template must be ready for a future pipeline that fills {SCRIPT}, {DESCRIPTION}, and {TIMESTAMPS}.");
        }
        else
        {
            sb.AppendLine("This is a NON-timestamped template. Given {SCRIPT} and optionally {DESCRIPTION}, it should be a ready music-generation prompt that tells the generator to follow the script's emotional arc.");
        }

        sb.AppendLine();
        sb.AppendLine("Output EXACTLY this structure and nothing else:");
        sb.AppendLine("TEMPLATE_NAME: <a short descriptive name for this template>");
        sb.AppendLine("TEMPLATE:");
        sb.AppendLine("<the full generalized template text with {SCRIPT}, optionally {DESCRIPTION}, and {TIMESTAMPS} if timestamped>");
        sb.AppendLine();
        sb.AppendLine("Put the suggested name only on the TEMPLATE_NAME line.");
        sb.AppendLine("Put the full reusable template body after the TEMPLATE: line.");
        sb.AppendLine("No commentary, no analysis, no Markdown wrapper, and no text outside this structure.");
        sb.AppendLine();
        sb.AppendLine("WORKING PROMPT TO GENERALIZE:");
        sb.AppendLine(workingPrompt);
        return sb.ToString();
    }

    private async void OnPasteNewTemplateClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;

        bool isTimestamped = _pendingTemplateIsTimestamped ?? await DisplayAlert(
            "Timestamped Template?",
            "Does this template use timestamps?",
            "Yes",
            "No");

        string? templateText = await ShowPasteDialogAsync(
            "Paste New Template",
            "Paste the generalized template returned by your LLM:",
            "TEMPLATE_NAME: Dark cyclical piano bed\nTEMPLATE:\nUse the following script to shape the music's emotional arc:\n{SCRIPT}",
            "Save");
        if (string.IsNullOrWhiteSpace(templateText)) return;

        var parsedTemplate = ParseTemplatePaste(templateText);
        string? name = await DisplayPromptAsync(
            "Template Name",
            "Name this prompt template:",
            "Save",
            "Cancel",
            initialValue: parsedTemplate.SuggestedName,
            placeholder: "Dark cyclical piano bed");
        if (name == null)
        {
            await DisplayAlert("Template Not Saved", "Template not saved.", "OK");
            return;
        }

        string finalName = string.IsNullOrWhiteSpace(name)
            ? $"Untitled Template {DateTime.Now:yyyy-MM-dd HHmm}"
            : name.Trim();

        var template = await _musicService.AddPromptTemplateAsync(
            _auth.CurrentUsername,
            finalName,
            parsedTemplate.TemplateBody,
            isTimestamped);
        _pendingTemplateIsTimestamped = null;

        try
        {
            await _ideas.CreateIdeaAsync(
                _auth.CurrentUsername,
                template.Name,
                "Music Prompt Templates",
                fullIdea: template.TemplateText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to log music prompt template as idea: {ex}");
        }

        await RefreshPromptTemplatesAsync(template.Id);
        await DisplayAlert("Template Saved", $"Saved \"{template.Name}\" and logged it to Ideas.", "OK");
    }

    private static (string SuggestedName, string TemplateBody) ParseTemplatePaste(string rawText)
    {
        string text = rawText?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            return ("", "");

        var nameMatch = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?im)^\s*TEMPLATE_NAME\s*:\s*(.+?)\s*$");
        var templateMatch = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?im)^\s*TEMPLATE\s*:\s*$");

        if (!nameMatch.Success && !templateMatch.Success)
            return ("", text);

        string suggestedName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "";
        string body = templateMatch.Success
            ? text[(templateMatch.Index + templateMatch.Length)..].Trim()
            : text;

        return (suggestedName, body);
    }

    private async void OnBuildTemplatePromptClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        if (_templatePicker.SelectedIndex < 0 || _templatePicker.SelectedIndex >= _promptTemplates.Count)
        {
            await DisplayAlert("No Template", "Select a saved template first.", "OK");
            return;
        }

        var template = _promptTemplates[_templatePicker.SelectedIndex];
        var script = await BuildCurrentScriptTextAsync();
        if (string.IsNullOrWhiteSpace(script))
        {
            await DisplayAlert("No Script", "Add script lines to the current draft before building a prompt from a template.", "OK");
            return;
        }

        var description = await GetGeneralMusicDescriptionTextAsync();

        if (template.IsTimestamped)
        {
            var timestamps = await _musicService.GetTimestampedNarrationAsync(_currentProject.Id);
            if (string.IsNullOrWhiteSpace(timestamps))
            {
                await DisplayAlert(
                    "Timestamps Needed",
                    "Do the transcription step first: use Get Transcription Prompt with your narration audio, then Paste Timestamps to save the result to this draft.",
                    "OK");
                return;
            }

            var metaPrompt = BuildTimestampedTemplateMetaPrompt(template, script, description, timestamps.Trim());
            await CopyPlanningPromptAsync(
                metaPrompt,
                "Timestamped Meta-Prompt Copied",
                "Paste this into your LLM. Copy the final timestamped music prompt it returns, then use that in Suno or ElevenLabs.");
            return;
        }

        var prompt = ReplaceTemplateToken(template.TemplateText, "{SCRIPT}", script);
        prompt = ReplaceTemplateToken(prompt, "{DESCRIPTION}", description);

        if (!ContainsToken(template.TemplateText, "{SCRIPT}"))
        {
            prompt += "\n\nSCRIPT:\n" + script;
        }

        await CopyPlanningPromptAsync(
            prompt,
            "Prompt Copied",
            "The ready music-generation prompt was copied. Paste it into Suno or ElevenLabs.");
    }

    private static string BuildTimestampedTemplateMetaPrompt(
        MusicPromptTemplate template,
        string script,
        string description,
        string timestamps)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Produce a final timestamped music-generation prompt for Suno or ElevenLabs for a short-video soundtrack.");
        sb.AppendLine("Use the template's structural DNA, but map it onto the real narration time ranges below.");
        sb.AppendLine("The template may contain {SCRIPT}, {DESCRIPTION}, and {TIMESTAMPS}; those placeholders correspond to the labeled sections provided here.");
        sb.AppendLine("Infer the total duration from the last timestamp. Output ONLY the final ready-to-use timestamped music-generation prompt, with no commentary.");
        sb.AppendLine();
        sb.AppendLine("TEMPLATE:");
        sb.AppendLine(template.TemplateText?.Trim() ?? "");
        sb.AppendLine();
        sb.AppendLine("SCRIPT:");
        sb.AppendLine(script?.Trim() ?? "");
        sb.AppendLine();
        sb.AppendLine("GENERAL MUSIC DESCRIPTION:");
        sb.AppendLine(string.IsNullOrWhiteSpace(description) ? "(none provided)" : description.Trim());
        sb.AppendLine();
        sb.AppendLine("TIMESTAMPS:");
        sb.AppendLine(timestamps?.Trim() ?? "");
        return sb.ToString();
    }

    private static bool ContainsToken(string text, string token)
    {
        return (text ?? "").IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ReplaceTemplateToken(string text, string token, string replacement)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            text ?? "",
            System.Text.RegularExpressions.Regex.Escape(token),
            replacement ?? "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async void OnManageTemplatesClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;
        await ShowManageTemplatesOverlayAsync();
    }

    private async Task ShowManageTemplatesOverlayAsync()
    {
        await RefreshPromptTemplatesAsync();

        var overlay = CreateOverlay();
        var card = CreateOverlayCard(width: 640, height: 560);
        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = "Manage Music Prompt Templates",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var listStack = new VerticalStackLayout { Spacing = 8 };
        BuildTemplateManagementRows(listStack, overlay);

        stack.Children.Add(new ScrollView
        {
            HeightRequest = 410,
            Content = listStack
        });

        var closeButton = ActionButton("Close", Color.FromArgb("#E0E0E0"), Color.FromArgb("#333"));
        closeButton.HorizontalOptions = LayoutOptions.End;
        closeButton.Clicked += (s, e) => RemoveOverlay(overlay);
        stack.Children.Add(closeButton);

        card.Content = stack;
        overlay.Children.Add(card);
        AddOverlay(overlay);
    }

    private void BuildTemplateManagementRows(VerticalStackLayout listStack, Grid overlay)
    {
        listStack.Children.Clear();

        if (_promptTemplates.Count == 0)
        {
            listStack.Children.Add(InfoLabel("No saved templates yet."));
            return;
        }

        foreach (var template in _promptTemplates)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                Padding = 8,
                BackgroundColor = Color.FromArgb("#FAFAFA")
            };

            var name = new Label
            {
                Text = template.IsTimestamped ? $"{template.Name} (timestamped)" : template.Name,
                TextColor = Color.FromArgb("#333"),
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            row.Children.Add(name);

            var rename = SmallButton("Rename", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
            rename.Clicked += async (s, e) =>
            {
                string? newName = await DisplayPromptAsync(
                    "Rename Template",
                    "Template name:",
                    "Save",
                    "Cancel",
                    initialValue: template.Name);
                if (string.IsNullOrWhiteSpace(newName)) return;

                template.Name = newName.Trim();
                await _musicService.UpdatePromptTemplateAsync(template);
                await RefreshPromptTemplatesAsync(template.Id);
                BuildTemplateManagementRows(listStack, overlay);
            };
            Grid.SetColumn(rename, 1);
            row.Children.Add(rename);

            var delete = SmallButton("Delete", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
            delete.Clicked += async (s, e) =>
            {
                bool confirm = await DisplayAlert(
                    "Delete Template",
                    $"Delete \"{template.Name}\"?",
                    "Delete",
                    "Cancel");
                if (!confirm) return;

                await _musicService.DeletePromptTemplateAsync(template.Id);
                await RefreshPromptTemplatesAsync();
                BuildTemplateManagementRows(listStack, overlay);
            };
            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);

            listStack.Children.Add(row);
        }
    }

    private async void OnExportMusicStateClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;
        if (_currentProject == null)
        {
            await DisplayAlert("No Project", "Select or create a music project first.", "OK");
            return;
        }

        var lines = await _musicService.GetLinesAsync(_currentProject.Id);
        var generalDescription = await _musicService.GetGeneralMusicDescriptionAsync(_currentProject.Id);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MUSIC STATE DISCUSSION - {_currentProject.Name}");
        sb.AppendLine();
        sb.AppendLine("This is a short-video soundtrack in progress. I want to discuss and refine how the Music column supports the Script and Visuals across the full draft.");
        sb.AppendLine("Please review the whole sequence and help me improve the music direction, continuity, emotional arc, and reusable ideas.");
        sb.AppendLine();
        sb.AppendLine("GENERAL MUSIC DESCRIPTION:");
        sb.AppendLine(string.IsNullOrWhiteSpace(generalDescription) ? "(none yet)" : generalDescription.Trim());
        sb.AppendLine();

        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            sb.AppendLine($"LINE {line.LineOrder}");
            sb.AppendLine($"Music: {line.Music}");
            sb.AppendLine($"Script: {line.Script}");
            sb.AppendLine($"Visual: {line.Visuals}");
            sb.AppendLine();
        }

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert(
            "Music State Copied",
            "The draft's Music/Script/Visual state was copied to clipboard.\n\nPaste it into your LLM to discuss or refine the soundtrack direction.",
            "OK");
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;
        if (_currentProject == null)
        {
            await DisplayAlert("No Project", "Select or create a music project first.", "OK");
            return;
        }

        string? choice = await DisplayActionSheet(
            "Import Draft",
            "Cancel",
            null,
            "Paste from Clipboard");

        if (choice != "Paste from Clipboard") return;

        string? content = await ShowPasteDialogAsync();
        if (content == null) return;

        await ProcessImportContentAsync(content);
    }

    private async Task ProcessImportContentAsync(string content)
    {
        if (_currentProject == null)
        {
            await DisplayAlert("No Project", "Select or create a music project first.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlert("Empty", "No content to import.", "OK");
            return;
        }

        try
        {
            var importResult = _musicService.ParseMusicImport(content);
            if (importResult.Lines.Count == 0)
            {
                await DisplayAlert(
                    "No Lines Found",
                    "Could not parse any lines.\n\nExpected format:\nlines[1].Script = \"text\";\nlines[1].Visual = \"text\";",
                    "OK");
                return;
            }

            int nextVersion = await _musicService.GetNextDraftVersionAsync(_currentProject.Id);
            string defaultName = $"Draft v{nextVersion} (AI)";
            string? draftName = await DisplayPromptAsync(
                "Name This Draft",
                $"Found {importResult.Lines.Count} lines.\nEdit the name or click Save:",
                accept: "Save",
                cancel: "Cancel",
                initialValue: defaultName,
                placeholder: "Draft name");

            if (string.IsNullOrWhiteSpace(draftName)) return;

            bool setAsLatest = await DisplayAlert(
                "Set as Latest?",
                $"Set \"{draftName}\" as the latest working draft?\n\nThis will open by default when you return to this project.",
                "Yes, Set as Latest",
                "No");

            var newDraft = await _musicService.CreateDraftFromImportAsync(
                _currentProject.Id,
                _auth.CurrentUsername,
                importResult,
                draftName.Trim(),
                setAsLatest);

            int rootId = newDraft.ParentProjectId ?? newDraft.Id;
            await LoadDraftsAsync(rootId, newDraft.Id);
            await LoadLinesAsync();

            await DisplayAlert(
                "Draft Imported!",
                $"Created \"{draftName}\" with {importResult.Lines.Count} lines." + (setAsLatest ? "\n\nSet as latest." : ""),
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MUSIC] Import error: {ex.Message}");
            await DisplayAlert("Import Error", $"Failed to import: {ex.Message}", "OK");
        }
    }

    private async void OnSetLatestClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        await _musicService.SetAsLatestAsync(_currentProject.Id);
        int rootId = _currentProject.ParentProjectId ?? _currentProject.Id;
        await LoadDraftsAsync(rootId, _currentProject.Id);
        await LoadLinesAsync();
    }

    private async void OnCompareClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null || _drafts.Count < 2) return;

        var options = new List<string> { "Auto (previous version)" };
        foreach (var draft in _drafts)
        {
            if (draft.Id == _currentProject.Id) continue;
            options.Add(draft.DraftVersion == 1 ? "Original" : draft.Name);
        }

        string? result = await DisplayActionSheet("Compare to which version?", "Cancel", null, options.ToArray());
        if (string.IsNullOrWhiteSpace(result) || result == "Cancel") return;

        if (result == "Auto (previous version)")
        {
            if (IsMaster) await _musicService.SetCompareToAsync(_currentProject.Id, null);
        }
        else
        {
            var selected = _drafts.FirstOrDefault(d =>
                d.Id != _currentProject.Id &&
                (d.DraftVersion == 1 ? "Original" : d.Name) == result);

            if (selected != null && IsMaster)
                await _musicService.SetCompareToAsync(_currentProject.Id, selected.Id);
        }

        _currentProject = await _musicService.GetProjectByIdAsync(_currentProject.Id) ?? _currentProject;
        await LoadLinesAsync();
    }

    private async void OnRenameDraftClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null || _currentProject.DraftVersion <= 1) return;

        string? name = await DisplayPromptAsync("Rename Draft", "Draft name:", initialValue: _currentProject.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        await _musicService.RenameDraftAsync(_currentProject.Id, name.Trim());
        int rootId = _currentProject.ParentProjectId ?? _currentProject.Id;
        await LoadDraftsAsync(rootId, _currentProject.Id);
        await LoadLinesAsync();
    }

    private async void OnDeleteDraftClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null || _currentProject.DraftVersion <= 1) return;

        bool confirm = await DisplayAlert(
            "Delete Draft",
            $"Delete \"{_currentProject.Name}\" and all its cards?",
            "Delete",
            "Cancel");
        if (!confirm) return;

        int rootId = _currentProject.ParentProjectId ?? _currentProject.Id;
        await _musicService.DeleteDraftAsync(_currentProject.Id);
        await LoadDraftsAsync(rootId);
        await LoadLinesAsync();
    }

    private async void OnAddCardClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        await _musicService.AddLineAsync(_currentProject.Id);
        await LoadLinesAsync();
    }

    private async Task DeleteLineAsync(MusicLine line)
    {
        if (!IsMaster) return;

        bool delete = await DisplayAlert("Delete Card", $"Delete card {line.LineOrder}?", "Delete", "Cancel");
        if (!delete) return;

        await _musicService.DeleteLineAsync(line.Id);
        await LoadLinesAsync();
    }

    private async Task<string?> ShowMultiLineInputAsync(string title, string message, string initialValue, string placeholder)
    {
        var tcs = new TaskCompletionSource<string?>();

        var overlay = CreateOverlay();
        var card = CreateOverlayCard(width: 520);
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
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            AutoSize = EditorAutoSizeOption.Disabled
        };
        stack.Children.Add(editor);

        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = ActionButton("Cancel", Color.FromArgb("#E0E0E0"), Color.FromArgb("#333"));
        var okBtn = ActionButton("OK", Color.FromArgb("#3949AB"), Colors.White);

        cancelBtn.Clicked += (s, e) =>
        {
            RemoveOverlay(overlay);
            tcs.TrySetResult(null);
        };

        okBtn.Clicked += (s, e) =>
        {
            RemoveOverlay(overlay);
            tcs.TrySetResult(editor.Text);
        };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(okBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        overlay.Children.Add(card);
        AddOverlay(overlay);
        editor.Focus();

        return await tcs.Task;
    }

    private async Task<string?> ShowPasteDialogAsync(
        string title = "Paste AI Response",
        string message = "Paste the C# code block from the AI:",
        string placeholder = "// NARRATION\nlines[1].Script = \"...\";\nlines[1].Visual = \"...\";",
        string acceptText = "Import")
    {
        var tcs = new TaskCompletionSource<string?>();

        var overlay = CreateOverlay();
        var card = CreateOverlayCard(width: 600, height: 500);
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
            Placeholder = placeholder,
            HeightRequest = 320,
            FontSize = 13,
            FontFamily = "Consolas",
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            AutoSize = EditorAutoSizeOption.Disabled
        };
        stack.Children.Add(editor);

        var btnRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End
        };

        var cancelBtn = ActionButton("Cancel", Color.FromArgb("#E0E0E0"), Color.FromArgb("#333"));
        var importBtn = ActionButton(acceptText, Color.FromArgb("#4CAF50"), Colors.White);

        cancelBtn.Clicked += (s, e) =>
        {
            RemoveOverlay(overlay);
            tcs.TrySetResult(null);
        };

        importBtn.Clicked += (s, e) =>
        {
            RemoveOverlay(overlay);
            tcs.TrySetResult(editor.Text);
        };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(importBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        overlay.Children.Add(card);
        AddOverlay(overlay);
        editor.Focus();

        return await tcs.Task;
    }

    private static Grid CreateOverlay()
    {
        return new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };
    }

    private static Frame CreateOverlayCard(double width, double? height = null)
    {
        return new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            WidthRequest = width,
            HeightRequest = height ?? -1,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
    }

    private void AddOverlay(Grid overlay)
    {
        if (Content is Grid pageGrid)
        {
            Grid.SetRowSpan(overlay, 2);
            pageGrid.Children.Add(overlay);
        }
    }

    private void RemoveOverlay(Grid overlay)
    {
        if (Content is Grid pageGrid)
            pageGrid.Children.Remove(overlay);
    }

    private void ClearCurrentProject(string message)
    {
        _currentProject = null;
        _drafts.Clear();
        _currentLines.Clear();
        _draftPicker.Items.Clear();
        _draftPicker.IsVisible = false;
        UpdateDraftControls();
        _linesContainer.Children.Clear();
        _linesContainer.Children.Add(InfoLabel(message));
    }

    private static Label InfoLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(4, 12)
        };
    }

    private static Button ActionButton(string text, Color background, Color textColor)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = background,
            TextColor = textColor,
            CornerRadius = 8,
            Padding = new Thickness(12, 7),
            FontSize = 12
        };
    }

    private static Button SmallButton(string text, Color background, Color textColor) => ActionButton(text, background, textColor);
}
