using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls.Shapes;

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
    private Button _addCardButton = null!;

    private List<MusicProject> _projects = new();
    private List<MusicProject> _drafts = new();
    private MusicProject? _currentProject;
    private MusicProject? _compareToProject;
    private HashSet<int> _changedLineOrders = new();
    private bool _isLoadingProjects;
    private bool _isLoadingDrafts;

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
        var pageGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Padding = 16,
            RowSpacing = 10
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
        Grid.SetRow(topFrame, 0);
        pageGrid.Children.Add(topFrame);

        _linesContainer = new VerticalStackLayout { Spacing = 10 };
        var scroll = new ScrollView { Content = _linesContainer };
        Grid.SetRow(scroll, 1);
        pageGrid.Children.Add(scroll);

        Content = pageGrid;
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
        _addCardButton.IsVisible = IsMaster;
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
        if (_compareToProject != null)
            _changedLineOrders = await _musicService.GetChangedLineOrdersAsync(_currentProject.Id, _compareToProject.Id);

        UpdateDraftControls();

        string categoryText = string.IsNullOrWhiteSpace(_currentProject.ProjectCategory)
            ? ""
            : $" - {_currentProject.ProjectCategory}";
        _statusLabel.Text = $"{lines.Count} card{(lines.Count == 1 ? "" : "s")}{categoryText}";

        if (lines.Count == 0)
        {
            _linesContainer.Children.Add(InfoLabel(IsMaster ? "No cards yet. Add one to start." : "No cards in this draft."));
            return;
        }

        foreach (var line in lines)
        {
            bool isChanged = _changedLineOrders.Contains(line.LineOrder);
            _linesContainer.Children.Add(CreateLineCard(line, lines.Count, isChanged));
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
        sb.AppendLine("- Split the story into logical lines/scenes/beats");
        sb.AppendLine("- If the story has no explicit visual descriptions, create appropriate ones based on the narration");
        sb.AppendLine("- Lines with no narration should use Script = \"\" (empty string)");
        sb.AppendLine("- Keep the original narration as close to verbatim as possible for the Script fields");
        sb.AppendLine("- Number lines sequentially starting at 1");
        sb.AppendLine("- Output ONLY the code block, no other text");

        await Clipboard.SetTextAsync(sb.ToString());

        await DisplayAlert(
            "Prompt Copied!",
            "The conversion prompt has been copied to your clipboard.\n\n" +
            "Steps:\n" +
            "1. Paste this prompt to any AI (ChatGPT, Claude, etc.)\n" +
            "2. The AI will output the story in the import format\n" +
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
            var importedLines = _musicService.ParseMusicImport(content);
            if (importedLines.Count == 0)
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
                $"Found {importedLines.Count} lines.\nEdit the name or click Save:",
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
                importedLines,
                draftName.Trim(),
                setAsLatest);

            int rootId = newDraft.ParentProjectId ?? newDraft.Id;
            await LoadDraftsAsync(rootId, newDraft.Id);
            await LoadLinesAsync();

            await DisplayAlert(
                "Draft Imported!",
                $"Created \"{draftName}\" with {importedLines.Count} lines." + (setAsLatest ? "\n\nSet as latest." : ""),
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

    private async Task<string?> ShowPasteDialogAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        var overlay = CreateOverlay();
        var card = CreateOverlayCard(width: 600, height: 500);
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
            Placeholder = "// NARRATION\nlines[1].Script = \"...\";\nlines[1].Visual = \"...\";",
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
        var importBtn = ActionButton("Import", Color.FromArgb("#4CAF50"), Colors.White);

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
