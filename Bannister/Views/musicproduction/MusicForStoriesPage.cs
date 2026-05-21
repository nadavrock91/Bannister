using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace Bannister.Views;

public class MusicForStoriesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MusicProductionService _musicService;
    private readonly DatabaseService _db;

    private Picker _projectPicker = null!;
    private Picker _draftPicker = null!;
    private Label _currentDraftLabel = null!;
    private Label _statusLabel = null!;
    private VerticalStackLayout _linesContainer = null!;
    private VerticalStackLayout _cueLibraryContainer = null!;
    private VerticalStackLayout _cueCardsContainer = null!;
    private Button _toggleCueLibraryButton = null!;
    private Button _newProjectButton = null!;
    private Button _projectSettingsButton = null!;
    private Button _deleteProjectButton = null!;
    private Button _newDraftButton = null!;
    private Button _renameDraftButton = null!;
    private Button _setLatestButton = null!;
    private Button _compareButton = null!;
    private Button _deleteDraftButton = null!;
    private Button _exportPromptButton = null!;
    private Button _importButton = null!;
    private Button _newCueButton = null!;
    private Button _generatePlanButton = null!;
    private Button _addCardButton = null!;
    private Grid _loadingOverlay = null!;
    private Label _loadingOverlayLabel = null!;

    private List<MusicProject> _projects = new();
    private List<MusicProject> _drafts = new();
    private List<MusicCue> _cues = new();
    private List<MusicLine> _currentLines = new();
    private MusicProject? _currentProject;
    private MusicProject? _compareToProject;
    private HashSet<int> _changedLineOrders = new();
    private bool _isLoadingProjects;
    private bool _isLoadingDrafts;
    private bool _isCueLibraryExpanded = true;

    private static readonly string[] RhythmOptions = { "Repetitive", "Evolving", "Shifting" };
    private static readonly string[] LayerOptions = { "piano", "percussion", "drone", "strings", "bass", "silence" };
    private static readonly string[] SectionDecisionOptions = { "UseOriginalCue", "Variation", "Intensified", "Stripped", "NewCue", "Silence", "Callback" };
    private static readonly string[] VariationTypeOptions = { "Original", "AddLayers", "DarkRemix", "RemovePercussion", "ExposePiano", "IncreaseBassDrone", "Custom" };
    private static readonly string[] EnergyOptions = { "Low", "Medium", "High" };
    private static readonly string[] StatusOptions = { "NotGenerated", "Generating", "Works", "Rejected" };
    private static readonly string[] ReuseOptions = { "Reusable", "NeedsVariation", "Replace" };

    private bool IsMaster => !_db.IsReadOnly;

    public MusicForStoriesPage(AuthService auth, MusicProductionService musicService, DatabaseService db)
    {
        _auth = auth;
        _musicService = musicService;
        _db = db;

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

        _importButton = ActionButton("Import", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _importButton.IsVisible = false;
        _importButton.Clicked += OnImportClicked;
        importExportRow.Children.Add(_importButton);
        topStack.Children.Add(importExportRow);

        _cueLibraryContainer = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false
        };
        topStack.Children.Add(_cueLibraryContainer);

        var cueHeaderRow = new HorizontalStackLayout { Spacing = 8 };
        cueHeaderRow.Children.Add(new Label
        {
            Text = "Cue Library",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.StartAndExpand
        });

        _toggleCueLibraryButton = ActionButton("Collapse", Color.FromArgb("#ECEFF1"), Color.FromArgb("#333"));
        _toggleCueLibraryButton.Clicked += (s, e) =>
        {
            _isCueLibraryExpanded = !_isCueLibraryExpanded;
            RenderCueLibrary();
        };
        cueHeaderRow.Children.Add(_toggleCueLibraryButton);

        _newCueButton = ActionButton("+ New Cue", Color.FromArgb("#3949AB"), Colors.White);
        _newCueButton.Clicked += OnNewCueClicked;
        cueHeaderRow.Children.Add(_newCueButton);

        _generatePlanButton = ActionButton("Generate Full Soundtrack Plan", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _generatePlanButton.Clicked += OnGenerateFullPlanClicked;
        cueHeaderRow.Children.Add(_generatePlanButton);

        _cueLibraryContainer.Children.Add(cueHeaderRow);
        _cueCardsContainer = new VerticalStackLayout { Spacing = 8 };
        _cueLibraryContainer.Children.Add(_cueCardsContainer);

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
            _importButton.IsVisible = false;
            _cueLibraryContainer.IsVisible = false;
            _newCueButton.IsVisible = false;
            _generatePlanButton.IsVisible = false;
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
        _importButton.IsVisible = IsMaster;
        _cueLibraryContainer.IsVisible = true;
        _newCueButton.IsVisible = IsMaster;
        _generatePlanButton.IsVisible = IsMaster;
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
            _cues.Clear();
            RenderCueLibrary();
            _linesContainer.Children.Add(InfoLabel("Choose a project to begin."));
            return;
        }

        var lines = await _musicService.GetLinesAsync(_currentProject.Id);
        _currentLines = lines;
        _cues = await _musicService.GetCuesAsync(_currentProject.Id);
        if (_compareToProject != null)
            _changedLineOrders = await _musicService.GetChangedLineOrdersAsync(_currentProject.Id, _compareToProject.Id);

        UpdateDraftControls();
        RenderCueLibrary();

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

        var musicEditor = CreateMusicPlanningEditor(line, out var musicControls);
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
                ApplyMusicPlanningControls(line, musicControls);
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

    private View CreateMusicPlanningEditor(MusicLine line, out SectionMusicControls controls)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label
        {
            Text = "Music",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var targetEntry = new Entry
        {
            Text = line.TargetEmotion ?? "",
            Placeholder = "Target emotion",
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White,
            IsReadOnly = !IsMaster
        };
        stack.Children.Add(WrapInput(targetEntry));

        var rhythmPicker = CreatePicker("Rhythm", RhythmOptions, line.RhythmIntent);
        stack.Children.Add(rhythmPicker);

        var layerBoxes = new Dictionary<string, CheckBox>();
        var selectedLayers = SplitCsv(line.LayerNotes);
        var layerWrap = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            AlignItems = FlexAlignItems.Center
        };
        foreach (var layer in LayerOptions)
        {
            var box = new CheckBox
            {
                IsChecked = selectedLayers.Contains(layer),
                IsEnabled = IsMaster,
                Color = Color.FromArgb("#3949AB")
            };
            layerBoxes[layer] = box;
            layerWrap.Children.Add(new HorizontalStackLayout
            {
                Spacing = 2,
                Margin = new Thickness(0, 0, 8, 0),
                Children =
                {
                    box,
                    new Label
                    {
                        Text = layer,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#444"),
                        VerticalOptions = LayoutOptions.Center
                    }
                }
            });
        }
        stack.Children.Add(new Label { Text = "Layers", FontSize = 12, TextColor = Color.FromArgb("#666") });
        stack.Children.Add(layerWrap);

        var decisionPicker = CreatePicker("Decision", SectionDecisionOptions, NormalizeDecision(line.SectionDecision));
        stack.Children.Add(decisionPicker);

        var cuePicker = new Picker
        {
            Title = "Assigned cue",
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Colors.White,
            IsEnabled = IsMaster
        };
        cuePicker.Items.Add("(none)");
        foreach (var cue in _cues)
            cuePicker.Items.Add(cue.Label);
        cuePicker.SelectedIndex = 0;
        if (line.AssignedCueId.HasValue)
        {
            int cueIndex = _cues.FindIndex(c => c.Id == line.AssignedCueId.Value);
            if (cueIndex >= 0) cuePicker.SelectedIndex = cueIndex + 1;
        }
        stack.Children.Add(WrapInput(cuePicker));

        controls = new SectionMusicControls(targetEntry, rhythmPicker, layerBoxes, decisionPicker, cuePicker);
        return stack;
    }

    private Picker CreatePicker(string title, string[] options, string? selectedValue)
    {
        var picker = new Picker
        {
            Title = title,
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Colors.White,
            IsEnabled = IsMaster
        };
        foreach (var option in options)
            picker.Items.Add(ToDisplayLabel(option));

        var normalized = string.IsNullOrWhiteSpace(selectedValue) ? "" : selectedValue.Trim();
        int index = Array.FindIndex(options, o => string.Equals(o, normalized, StringComparison.OrdinalIgnoreCase));
        picker.SelectedIndex = index >= 0 ? index : -1;
        return picker;
    }

    private void ApplyMusicPlanningControls(MusicLine line, SectionMusicControls controls)
    {
        line.TargetEmotion = controls.TargetEmotion.Text?.Trim() ?? "";
        line.RhythmIntent = PickerValue(controls.RhythmIntent, RhythmOptions);
        line.LayerNotes = string.Join(",", controls.LayerBoxes.Where(kvp => kvp.Value.IsChecked).Select(kvp => kvp.Key));
        line.SectionDecision = PickerValue(controls.SectionDecision, SectionDecisionOptions);
        line.AssignedCueId = controls.AssignedCue.SelectedIndex > 0 && controls.AssignedCue.SelectedIndex - 1 < _cues.Count
            ? _cues[controls.AssignedCue.SelectedIndex - 1].Id
            : null;
    }

    private static string PickerValue(Picker picker, string[] options)
    {
        return picker.SelectedIndex >= 0 && picker.SelectedIndex < options.Length
            ? options[picker.SelectedIndex]
            : "";
    }

    private static HashSet<string> SplitCsv(string? value)
    {
        return new HashSet<string>(
            (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeDecision(string? value)
    {
        if (string.Equals(value, "Use Original Cue", StringComparison.OrdinalIgnoreCase)) return "UseOriginalCue";
        if (string.Equals(value, "New Cue", StringComparison.OrdinalIgnoreCase)) return "NewCue";
        return value ?? "";
    }

    private static string ToDisplayLabel(string value)
    {
        return value switch
        {
            "UseOriginalCue" => "Use Original Cue",
            "NewCue" => "New Cue",
            "AddLayers" => "Add Layers",
            "DarkRemix" => "Dark Remix",
            "RemovePercussion" => "Remove Percussion",
            "ExposePiano" => "Expose Piano",
            "IncreaseBassDrone" => "Increase Bass Drone",
            "NotGenerated" => "Not Generated",
            "NeedsVariation" => "Needs Variation",
            _ => value
        };
    }

    private View WrapInput(View input)
    {
        return new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = 4,
            BackgroundColor = Colors.White,
            Content = input
        };
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

    private record SectionMusicControls(
        Entry TargetEmotion,
        Picker RhythmIntent,
        Dictionary<string, CheckBox> LayerBoxes,
        Picker SectionDecision,
        Picker AssignedCue);

    private static string GetEditorText(View columnView)
    {
        if (columnView is not VerticalStackLayout stack) return "";
        var border = stack.Children.OfType<Border>().FirstOrDefault();
        return border?.Content is Editor editor ? editor.Text?.Trim() ?? "" : "";
    }

    private void RenderCueLibrary()
    {
        if (_cueCardsContainer == null || _toggleCueLibraryButton == null) return;

        _cueCardsContainer.Children.Clear();
        _toggleCueLibraryButton.Text = _isCueLibraryExpanded ? "Collapse" : "Expand";
        _cueCardsContainer.IsVisible = _isCueLibraryExpanded;
        _newCueButton.IsVisible = IsMaster && _currentProject != null;
        _generatePlanButton.IsVisible = IsMaster && _currentProject != null;

        if (!_isCueLibraryExpanded) return;

        if (_currentProject == null)
        {
            _cueCardsContainer.Children.Add(InfoLabel("Choose a draft to view cues."));
            return;
        }

        if (_cues.Count == 0)
        {
            _cueCardsContainer.Children.Add(InfoLabel(IsMaster
                ? "No cues yet. Add a primary DNA cue to start planning reusable music blocks."
                : "No cues in this draft."));
            return;
        }

        foreach (var cue in _cues)
            _cueCardsContainer.Children.Add(CreateCueCard(cue));
    }

    private View CreateCueCard(MusicCue cue)
    {
        var frame = new Frame
        {
            Padding = 10,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            BorderColor = ReuseColor(cue.ReuseFlag),
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 6 };
        var title = cue.Label + (cue.IsPrimaryDNA ? " - Primary DNA" : "");
        if (cue.ParentCueId.HasValue)
        {
            var parent = _cues.FirstOrDefault(c => c.Id == cue.ParentCueId.Value);
            if (parent != null) title += $" - variation of {parent.Label}";
        }

        stack.Children.Add(new Label
        {
            Text = title,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = $"{ToDisplayLabel(cue.VariationType)} | {ToDisplayLabel(cue.Status)} | {ToDisplayLabel(cue.ReuseFlag)}",
            FontSize = 12,
            TextColor = ReuseColor(cue.ReuseFlag)
        });

        if (!string.IsNullOrWhiteSpace(cue.Mood) || !string.IsNullOrWhiteSpace(cue.Motif))
        {
            stack.Children.Add(new Label
            {
                Text = $"{cue.Mood} {cue.EnergyLevel} | {cue.Motif}".Trim(),
                FontSize = 12,
                TextColor = Color.FromArgb("#666")
            });
        }

        if (IsMaster)
        {
            var actions = new FlexLayout
            {
                Wrap = FlexWrap.Wrap,
                Direction = FlexDirection.Row
            };

            var generate = SmallButton("Generate Prompt", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
            generate.Margin = new Thickness(0, 0, 6, 4);
            generate.Clicked += async (s, e) => await GenerateCuePromptAsync(cue);
            actions.Children.Add(generate);

            var refine = SmallButton("Refine", Color.FromArgb("#F3E5F5"), Color.FromArgb("#6A1B9A"));
            refine.Margin = new Thickness(0, 0, 6, 4);
            refine.Clicked += async (s, e) => await RefineCuePromptAsync(cue);
            actions.Children.Add(refine);

            var variation = SmallButton("+ Variation", Color.FromArgb("#FFF8E1"), Color.FromArgb("#F57F17"));
            variation.Margin = new Thickness(0, 0, 6, 4);
            variation.Clicked += async (s, e) => await CreateVariationAsync(cue);
            actions.Children.Add(variation);

            var edit = SmallButton("Edit", Color.FromArgb("#E8EAF6"), Color.FromArgb("#3F51B5"));
            edit.Margin = new Thickness(0, 0, 6, 4);
            edit.Clicked += async (s, e) => await EditCueAsync(cue);
            actions.Children.Add(edit);

            var delete = SmallButton("Delete", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
            delete.Margin = new Thickness(0, 0, 6, 4);
            delete.Clicked += async (s, e) => await DeleteCueAsync(cue);
            actions.Children.Add(delete);

            stack.Children.Add(actions);
        }

        frame.Content = stack;
        return frame;
    }

    private static Color ReuseColor(string reuseFlag)
    {
        return reuseFlag switch
        {
            "Reusable" => Color.FromArgb("#2E7D32"),
            "NeedsVariation" => Color.FromArgb("#F57F17"),
            "Replace" => Color.FromArgb("#C62828"),
            _ => Color.FromArgb("#666")
        };
    }

    private async void OnNewCueClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        string? label = await DisplayPromptAsync("New Cue", "Cue label:", initialValue: _cues.Count == 0 ? "Main DNA" : "", placeholder: "Main DNA");
        if (string.IsNullOrWhiteSpace(label)) return;

        bool isPrimary = _cues.Count == 0 || await DisplayAlert("Primary DNA?", "Mark this as the foundational first cue?", "Yes", "No");
        var cue = await _musicService.AddCueAsync(_currentProject.Id, label.Trim(), isPrimary);
        await EditCueAsync(cue);
        await LoadLinesAsync();
    }

    private async Task CreateVariationAsync(MusicCue parent)
    {
        string? typeDisplay = await DisplayActionSheet("Variation Type", "Cancel", null, VariationTypeOptions.Select(ToDisplayLabel).ToArray());
        if (string.IsNullOrWhiteSpace(typeDisplay) || typeDisplay == "Cancel") return;

        string variationType = VariationTypeOptions.FirstOrDefault(o => ToDisplayLabel(o) == typeDisplay) ?? "Custom";
        string? label = await DisplayPromptAsync("New Variation", "Variation cue label:", initialValue: $"{parent.Label} {ToDisplayLabel(variationType)}");
        if (string.IsNullOrWhiteSpace(label)) return;

        var cue = await _musicService.CreateVariationCueAsync(parent.Id, variationType, label.Trim());
        await EditCueAsync(cue);
        await LoadLinesAsync();
    }

    private async Task EditCueAsync(MusicCue cue)
    {
        string? label = await DisplayPromptAsync("Cue Label", "Label:", initialValue: cue.Label);
        if (label == null) return;
        cue.Label = label.Trim();

        string? mood = await DisplayPromptAsync("Mood", "Emotion/mood for this cue:", initialValue: cue.Mood);
        if (mood == null) return;
        cue.Mood = mood.Trim();

        string? pulse = await DisplayPromptAsync("Pulse", "Rhythm/pulse description:", initialValue: cue.Pulse);
        if (pulse == null) return;
        cue.Pulse = pulse.Trim();

        string? motif = await DisplayPromptAsync("Motif", "Main motif description:", initialValue: cue.Motif);
        if (motif == null) return;
        cue.Motif = motif.Trim();

        cue.EnergyLevel = await PickOptionAsync("Energy Level", EnergyOptions, cue.EnergyLevel) ?? cue.EnergyLevel;
        cue.VariationType = await PickOptionAsync("Variation Type", VariationTypeOptions, cue.VariationType) ?? cue.VariationType;
        cue.Status = await PickOptionAsync("Status", StatusOptions, cue.Status) ?? cue.Status;
        cue.ReuseFlag = await PickOptionAsync("Reuse Flag", ReuseOptions, cue.ReuseFlag) ?? cue.ReuseFlag;

        string? duration = await DisplayPromptAsync("Duration", "Duration in seconds:", initialValue: cue.DurationSeconds.ToString(), keyboard: Keyboard.Numeric);
        if (duration == null) return;
        if (int.TryParse(duration, out int seconds) && seconds > 0)
            cue.DurationSeconds = seconds;

        cue.MustLoop = await DisplayAlert("Must Loop?", "Must loop cleanly without becoming annoying?", "Yes", "No");
        cue.MustSitUnderNarration = await DisplayAlert("Narration Safe?", "Must sit under spoken narration without competing?", "Yes", "No");

        string? notes = await ShowMultiLineInputAsync("Cue Notes", "Notes or custom variation instructions:", cue.Notes, "Notes...");
        if (notes == null) return;
        cue.Notes = notes.Trim();

        await _musicService.UpdateCueAsync(cue);
        await LoadLinesAsync();
    }

    private async Task<string?> PickOptionAsync(string title, string[] options, string currentValue)
    {
        var labels = options.Select(o => string.Equals(o, currentValue, StringComparison.OrdinalIgnoreCase) ? $"{ToDisplayLabel(o)} *" : ToDisplayLabel(o)).ToArray();
        string? choice = await DisplayActionSheet(title, "Cancel", null, labels);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return null;
        choice = choice.Replace(" *", "");
        return options.FirstOrDefault(o => ToDisplayLabel(o) == choice);
    }

    private async Task DeleteCueAsync(MusicCue cue)
    {
        bool confirm = await DisplayAlert("Delete Cue", $"Delete \"{cue.Label}\"? Sections using it will be set to no cue.", "Delete", "Cancel");
        if (!confirm) return;

        await _musicService.DeleteCueAsync(cue.Id);
        await LoadLinesAsync();
    }

    private async Task GenerateCuePromptAsync(MusicCue cue)
    {
        var prompt = BuildCuePrompt(cue);
        cue.GeneratedPrompt = prompt;
        cue.Status = cue.Status == "NotGenerated" ? "Generating" : cue.Status;
        await _musicService.UpdateCueAsync(cue);
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert(
            "Prompt Copied",
            "Cue prompt copied to clipboard.\n\nPaste it into Suno or ElevenLabs, generate an approximately 30-second music block, iterate until it works, then mark the cue Status = Works.",
            "OK");
        await LoadLinesAsync();
    }

    private async Task RefineCuePromptAsync(MusicCue cue)
    {
        string? choice = await DisplayActionSheet(
            "Refine Prompt",
            "Cancel",
            null,
            "Describe what's wrong",
            "Paste refined prompt");

        if (choice == "Describe what's wrong")
            await CreateCueRefinementPromptAsync(cue);
        else if (choice == "Paste refined prompt")
            await PasteRefinedCuePromptAsync(cue);
    }

    private async Task CreateCueRefinementPromptAsync(MusicCue cue)
    {
        string? feedback = await ShowMultiLineInputAsync(
            "Refine Cue Prompt",
            "Describe what is not working with the generated music.",
            "",
            "Too generic, piano feels lifeless, needs darker low end...");

        if (string.IsNullOrWhiteSpace(feedback)) return;

        var parent = cue.ParentCueId.HasValue ? _cues.FirstOrDefault(c => c.Id == cue.ParentCueId.Value) : null;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("This is a prompt-refinement task for an AI music generator such as Suno or ElevenLabs.");
        sb.AppendLine("The prompt is for a roughly 30-second loopable instrumental cue in a short video soundtrack.");
        sb.AppendLine("The current prompt produced unsatisfying results. Rewrite it into a better, more specific music-generation prompt.");
        sb.AppendLine();
        sb.AppendLine("CUE DETAILS:");
        sb.AppendLine($"Label: {cue.Label}");
        sb.AppendLine($"Role: {(cue.IsPrimaryDNA || !cue.ParentCueId.HasValue ? "Primary DNA cue" : $"Variation of {parent?.Label ?? "parent cue"}")}");
        sb.AppendLine($"Variation Type: {ToDisplayLabel(cue.VariationType)}");
        sb.AppendLine($"Mood: {cue.Mood}");
        sb.AppendLine($"Pulse: {cue.Pulse}");
        sb.AppendLine($"Motif: {cue.Motif}");
        sb.AppendLine($"Energy Level: {cue.EnergyLevel}");
        sb.AppendLine($"Must Loop: {cue.MustLoop}");
        sb.AppendLine($"Must Sit Under Narration: {cue.MustSitUnderNarration}");
        sb.AppendLine($"Duration Seconds: {Math.Max(1, cue.DurationSeconds)}");
        if (!string.IsNullOrWhiteSpace(cue.Notes))
            sb.AppendLine($"Notes: {cue.Notes}");
        sb.AppendLine();
        sb.AppendLine("CURRENT GENERATED PROMPT:");
        sb.AppendLine(string.IsNullOrWhiteSpace(cue.GeneratedPrompt) ? BuildCuePrompt(cue) : cue.GeneratedPrompt);
        sb.AppendLine();
        sb.AppendLine("USER FEEDBACK ABOUT WHAT ISN'T WORKING:");
        sb.AppendLine(feedback.Trim());
        sb.AppendLine();
        sb.AppendLine("Rewrite the music-generation prompt.");
        sb.AppendLine("Output ONLY the improved prompt text, with no commentary.");
        sb.AppendLine("Keep it concise enough for Suno/ElevenLabs.");
        sb.AppendLine("Be specific about instrumentation, texture, tempo feel, and emotional intent.");
        sb.AppendLine("Preserve loopability, narration-safe mixing, and the approximate target duration.");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert(
            "Refinement Prompt Copied",
            "Paste this into your LLM, copy the improved prompt it returns, then use Refine -> Paste refined prompt on this cue to save it.",
            "OK");
    }

    private async Task PasteRefinedCuePromptAsync(MusicCue cue)
    {
        string? refinedPrompt = await ShowPasteDialogAsync(
            "Paste Refined Prompt",
            "Paste the improved Suno/ElevenLabs prompt returned by your LLM:",
            "Paste the improved music-generation prompt here...",
            "Save");

        if (string.IsNullOrWhiteSpace(refinedPrompt)) return;

        cue.GeneratedPrompt = refinedPrompt.Trim();
        await _musicService.UpdateCueAsync(cue);
        await DisplayAlert("Prompt Saved", "The refined prompt was saved to this cue.", "OK");
        await LoadLinesAsync();
    }

    private string BuildCuePrompt(MusicCue cue)
    {
        if (cue.IsPrimaryDNA || !cue.ParentCueId.HasValue)
        {
            var layers = GetLayersForCue(cue.Id);
            var parts = new List<string>
            {
                $"Instrumental, {cue.Mood}, {cue.EnergyLevel} energy.",
                cue.Pulse,
                $"Main motif: {cue.Motif}.",
                cue.MustLoop ? "Must loop cleanly without becoming repetitive." : "",
                cue.MustSitUnderNarration ? "Must sit under spoken narration without competing." : "",
                $"Approximately {Math.Max(1, cue.DurationSeconds)} seconds.",
                string.IsNullOrWhiteSpace(layers) ? "Layers: general cinematic music bed." : $"Layers: {layers}."
            };
            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
        }

        var parent = _cues.FirstOrDefault(c => c.Id == cue.ParentCueId.Value);
        string parentLabel = parent?.Label ?? "parent cue";
        return $"Variation of the main theme '{parentLabel}'. Keep the core motif recognizable. {VariationInstruction(cue.VariationType)} Same tempo and key feel as the original. Approximately {Math.Max(1, cue.DurationSeconds)} seconds." +
               (string.IsNullOrWhiteSpace(cue.Notes) ? "" : $" Notes: {cue.Notes}");
    }

    private string GetLayersForCue(int cueId)
    {
        var layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in _currentLines.Where(l => l.AssignedCueId == cueId))
        {
            foreach (var layer in SplitCsv(line.LayerNotes))
                layers.Add(layer);
        }
        return string.Join(", ", layers);
    }

    private static string VariationInstruction(string variationType)
    {
        return variationType switch
        {
            "AddLayers" => "Add instrumental layers.",
            "DarkRemix" => "Create a darker, tenser reinterpretation.",
            "RemovePercussion" => "Remove percussion.",
            "ExposePiano" => "Strip down to expose the piano motif.",
            "IncreaseBassDrone" => "Increase bass and drone presence.",
            "Custom" => "Follow the custom notes.",
            _ => "Keep the original cue identity."
        };
    }

    private async void OnGenerateFullPlanClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var lines = await _musicService.GetLinesAsync(_currentProject.Id);
        var cueLookup = _cues.ToDictionary(c => c.Id);
        var usedCueIds = new HashSet<int>();
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"FULL SOUNDTRACK PLAN - {_currentProject.Name}");
        sb.AppendLine("Build this as modular reusable ~30-second cue blocks, not one long track.");
        sb.AppendLine();

        foreach (var line in lines.OrderBy(l => l.LineOrder))
        {
            MusicCue? cue = line.AssignedCueId.HasValue && cueLookup.TryGetValue(line.AssignedCueId.Value, out var found) ? found : null;
            if (cue != null) usedCueIds.Add(cue.Id);

            sb.AppendLine($"SECTION {line.LineOrder} - {line.TargetEmotion}");
            sb.AppendLine($"Script: {Shorten(line.Script, 140)}");
            sb.AppendLine($"Rhythm: {line.RhythmIntent}");
            sb.AppendLine($"Layers: {line.LayerNotes}");
            sb.AppendLine($"Decision: {ToDisplayLabel(line.SectionDecision)}");
            sb.AppendLine($"Cue: {(cue == null ? "(none)" : cue.Label)}");
            sb.AppendLine($"Visual: {Shorten(line.Visuals, 140)}");
            sb.AppendLine();
        }

        sb.AppendLine("CUES TO GENERATE ONCE AND REUSE:");
        foreach (var cueId in usedCueIds)
        {
            var cue = cueLookup[cueId];
            sb.AppendLine();
            sb.AppendLine($"{cue.Label} ({ToDisplayLabel(cue.VariationType)})");
            if (cue.ParentCueId.HasValue && cueLookup.TryGetValue(cue.ParentCueId.Value, out var parent))
                sb.AppendLine($"Variation of: {parent.Label}");
            sb.AppendLine(string.IsNullOrWhiteSpace(cue.GeneratedPrompt)
                ? "Prompt not generated yet."
                : cue.GeneratedPrompt);
        }

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert(
            "Plan Copied",
            "Full soundtrack plan copied to clipboard.\n\nUse it as a modular build sheet: generate each unique cue once, then reuse or vary it across sections.",
            "OK");
    }

    private static string Shorten(string? value, int max)
    {
        value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= max ? value : value.Substring(0, max - 3) + "...";
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
        sb.AppendLine("- Fill the full music plan, not only Script and Visual");
        sb.AppendLine("- Reuse a small set of cue names across many lines so the soundtrack is built from modular ~30-second blocks");
        sb.AppendLine("- Aim for 4-8 unique cues total unless the story truly requires fewer or more");
        sb.AppendLine("- Define each unique cue in the cue[] block");
        sb.AppendLine("- Output ONLY the code block, no other text");

        await Clipboard.SetTextAsync(sb.ToString());

        await DisplayAlert(
            "Prompt Copied!",
            "The conversion prompt has been copied to your clipboard.\n\n" +
            "Steps:\n" +
            "1. Paste this prompt to any AI (ChatGPT, Claude, etc.)\n" +
            "2. The AI will output the full music plan in the import format\n" +
            "3. Copy the AI's output\n" +
            "4. Use Import -> Paste from Clipboard to import",
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
            int cueCount = CountUniqueCueNames(importResult);
            if (importResult.Lines.Count == 0)
            {
                await DisplayAlert(
                    "No Lines Found",
                    "Could not parse any lines.\n\nExpected format:\nlines[1].Script = \"text\";\nlines[1].Visual = \"text\";\nlines[1].Emotion = \"tension\";\nlines[1].Cue = \"Main DNA\";",
                    "OK");
                return;
            }

            int nextVersion = await _musicService.GetNextDraftVersionAsync(_currentProject.Id);
            string defaultName = $"Draft v{nextVersion} (AI)";
            string? draftName = await DisplayPromptAsync(
                "Name This Draft",
                $"Found {importResult.Lines.Count} lines and {cueCount} cues.\nEdit the name or click Save:",
                accept: "Save",
                cancel: "Cancel",
                initialValue: defaultName,
                placeholder: "Draft name");

            if (string.IsNullOrWhiteSpace(draftName)) return;

            string? cueMode = await DisplayActionSheet(
                "How should cues be prepared?",
                "Cancel",
                null,
                "DNA first (recommended)",
                "Build all cues now");
            if (string.IsNullOrWhiteSpace(cueMode) || cueMode == "Cancel") return;

            bool generatePromptsForAllCues = cueMode == "Build all cues now";

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
                setAsLatest,
                generatePromptsForAllCues);

            int rootId = newDraft.ParentProjectId ?? newDraft.Id;
            await LoadDraftsAsync(rootId, newDraft.Id);
            await LoadLinesAsync();

            string modeMessage = cueCount == 0
                ? "No cues were included in the import."
                : generatePromptsForAllCues
                ? "All cue prompts were generated."
                : "DNA cue prompt generated - refine and confirm it works, then generate the variations.";
            await DisplayAlert(
                "Draft Imported!",
                $"Created \"{draftName}\" with {importResult.Lines.Count} lines and {cueCount} cues.\n\n{modeMessage}" + (setAsLatest ? "\n\nSet as latest." : ""),
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MUSIC] Import error: {ex.Message}");
            await DisplayAlert("Import Error", $"Failed to import: {ex.Message}", "OK");
        }
    }

    private static int CountUniqueCueNames(MusicImportResult importResult)
    {
        var names = new HashSet<string>(importResult.Cues.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var line in importResult.Lines)
        {
            if (!string.IsNullOrWhiteSpace(line.CueName))
                names.Add(line.CueName.Trim());
        }
        return names.Count;
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
        _cues.Clear();
        _currentLines.Clear();
        _draftPicker.Items.Clear();
        _draftPicker.IsVisible = false;
        UpdateDraftControls();
        RenderCueLibrary();
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
