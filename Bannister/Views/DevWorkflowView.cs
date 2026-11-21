using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DevWorkflowView : ContentView
{
    private readonly Func<string> _getUsername;
    private readonly DevWorkflowService _service;
    private readonly bool _compact;
    private readonly Func<string, string, Task>? _showAlert;
    private readonly VerticalStackLayout _content = new() { Spacing = 12 };
    private readonly ScrollView _runScroll;
    private List<DevWorkflow> _workflows = new();
    private List<DevWorkflowStep> _steps = new();
    private List<DevProgrammingConstraint> _constraints = new();
    private List<DevWorkflowProject> _projects = new();
    private readonly Dictionary<int, string> _projectFolders = new();
    private DevWorkflow? _selected;
    private DevWorkflowProject? _selectedProject;
    private string _projectFolder = "";
    private readonly Dictionary<int, (string State, string Message, bool Behind)> _runStates = new();
    private int _runIndex = -1;

    public DevWorkflowView(Func<string> getUsername, DevWorkflowService service,
        bool compact = false, Func<string, string, Task>? showAlert = null)
    {
        _getUsername = getUsername; _service = service; _compact = compact; _showAlert = showAlert;
        BackgroundColor = compact ? Color.FromArgb("#F5F5F5") : Colors.Transparent;
        _runScroll = new ScrollView { Content = _content };
        Content = _runScroll;
    }

    private string Username => _getUsername();

    public async Task ActivateAsync()
    {
        await _service.InitAsync();
        _workflows = await _service.GetWorkflowsAsync(Username);
        int? lastId = await _service.GetLastUsedWorkflowIdAsync();
        _selected = lastId.HasValue ? _workflows.FirstOrDefault(x => x.Id == lastId) : null;
        if (lastId.HasValue && _selected == null) await _service.SetLastUsedWorkflowIdAsync(null);
        _projects = await _service.GetProjectsAsync(Username);
        int? lastProjectId = await _service.GetLastUsedProjectIdAsync();
        _selectedProject = lastProjectId.HasValue
            ? _projects.FirstOrDefault(x => x.Id == lastProjectId)
            : null;
        if (lastProjectId.HasValue && _selectedProject == null)
            await _service.SetLastUsedProjectIdAsync(null);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _workflows = await _service.GetWorkflowsAsync(Username);
        if (_selected != null) _selected = _workflows.FirstOrDefault(x => x.Id == _selected.Id);
        _steps = _selected == null ? new() : await _service.GetStepsAsync(Username, _selected.Id);
        _constraints = await _service.GetConstraintsAsync(Username);
        _projects = await _service.GetProjectsAsync(Username);
        if (_selectedProject != null)
            _selectedProject = _projects.FirstOrDefault(x => x.Id == _selectedProject.Id);
        _projectFolders.Clear();
        foreach (var project in _projects)
            _projectFolders[project.Id] = await _service.GetProjectFolderAsync(project.Id);
        _projectFolder = _selectedProject != null && _projectFolders.TryGetValue(_selectedProject.Id, out var folder)
            ? folder : "";
        Content = _runScroll;
        BuildRunUI();
    }

    private void BuildRunUI()
    {
        _content.Children.Clear(); _content.Padding = _compact ? 8 : 16;
        _content.Children.Add(new Label { Text = "Dev Workflow", FontSize = _compact ? 21 : 26, FontAttributes = FontAttributes.Bold, TextColor = _compact ? Color.FromArgb("#2D1B6B") : Color.FromArgb("#222") });
        BuildHeader();
        if (_selected != null)
        {
            _content.Children.Add(BuildRunControls());
            if (_steps.Count == 0) _content.Children.Add(new Label { Text = "This workflow has no steps. Use Edit to add some.", TextColor = Color.FromArgb("#777") });
            foreach (var step in _steps) _content.Children.Add(BuildRunStep(step));
        }
        BuildSettingsArea();
    }

    private View BuildRunControls()
    {
        var row = new HorizontalStackLayout { Spacing = 7 };
        var run = Button("Run", "#6A1B9A");
        run.Clicked += async (_, _) => await StartRunAsync();
        row.Children.Add(run);
        if (_projects.Count == 0)
        {
            var add = Button("Add a project", "#5B63EE");
            add.Clicked += async (_, _) => await AddProjectAsync();
            row.Children.Add(add);
        }
        else
        {
            var picker = new Picker
            {
                Title = "Project",
                ItemsSource = _projects,
                ItemDisplayBinding = new Binding(nameof(DevWorkflowProject.Name)),
                SelectedItem = _selectedProject,
                WidthRequest = 180
            };
            picker.SelectedIndexChanged += async (_, _) =>
            {
                if (picker.SelectedItem is not DevWorkflowProject project || project.Id == _selectedProject?.Id) return;
                _selectedProject = project;
                _projectFolder = _projectFolders.TryGetValue(project.Id, out var path) ? path : "";
                _runStates.Clear(); _runIndex = -1;
                await _service.SetLastUsedProjectIdAsync(project.Id);
                BuildRunUI();
            };
            row.Children.Add(picker);
        }
        return row;
    }

    private void BuildHeader()
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(new Label { Text = _selected?.Name ?? "Select a workflow", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        var picker = new Picker { Title = "Workflow", ItemsSource = _workflows, ItemDisplayBinding = new Binding(nameof(DevWorkflow.Name)), SelectedItem = _selected };
        picker.SelectedIndexChanged += async (_, _) =>
        {
            if (picker.SelectedItem is not DevWorkflow workflow || workflow.Id == _selected?.Id) return;
            _selected = workflow; _runStates.Clear(); _runIndex = -1;
            await _service.SetLastUsedWorkflowIdAsync(workflow.Id); await RefreshAsync();
        };
        stack.Children.Add(picker);
        var actions = new HorizontalStackLayout { Spacing = 6 };
        if (_selected != null) { var edit = Button("Edit", "#1565C0"); edit.Clicked += async (_, _) => await ShowEditorAsync(_selected); actions.Children.Add(edit); }
        var create = Button(_workflows.Count == 0 ? "Create Workflow" : "New Workflow", "#2E7D32"); create.Clicked += async (_, _) => await ShowEditorAsync(null); actions.Children.Add(create);
        stack.Children.Add(actions); _content.Children.Add(Card(stack));
    }

    private async Task ShowEditorAsync(DevWorkflow? workflow)
    {
        var editor = new DevWorkflowEditorView(Username, _service, workflow, _compact);
        editor.Saved += async saved => { _selected = saved; _runStates.Clear(); _runIndex = -1; await RefreshAsync(); };
        editor.Cancelled += async () => await RefreshAsync();
        editor.Deleted += async () => { _selected = null; _runStates.Clear(); _runIndex = -1; await RefreshAsync(); };
        Content = editor; await editor.ActivateAsync();
    }

    private View BuildRunStep(DevWorkflowStep step)
    {
        string text = step.StepType == "Check" ? "Git up to date" : step.UseAllConstraints ? "All programming constraints" : step.Text;
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Children.Add(new Label { Text = $"{step.SortOrder + 1}. [{step.StepType}] {text}", FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap });
        var state = _runStates.TryGetValue(step.Id, out var value) ? value : ("pending", "Not started", false);
        stack.Children.Add(new Label { Text = $"{state.Item1}: {state.Item2}", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = StateColor(state.Item1) });
        if (step.StepType == "Check" && NeedsProjectFolder(state.Item2))
        {
            var setFolder = Small("Set Folder", "#5B63EE");
            setFolder.Clicked += async (_, _) => await SetProjectFolderAndRecheckAsync(step);
            stack.Children.Add(setFolder);
        }
        if (state.Item1 == "failed")
        {
            var actions = new HorizontalStackLayout { Spacing = 5 };
            var retry = Small("Re-check"); retry.Clicked += async (_, _) => await RunCurrentStepAsync(); actions.Children.Add(retry);
            if (state.Item3) { var pull = Small("Pull", "#1565C0"); pull.Clicked += async (_, _) => await PullAndRecheckAsync(); actions.Children.Add(pull); }
            var over = Small("Override", "#E65100"); over.Clicked += async (_, _) => await OverrideAsync(step); actions.Children.Add(over); stack.Children.Add(actions);
        }
        else if (state.Item1 == "pending" && step.StepType == "Manual" && _runIndex >= 0 && _runIndex < _steps.Count && _steps[_runIndex].Id == step.Id)
        {
            var check = new CheckBox { Color = Color.FromArgb("#2E7D32") }; check.CheckedChanged += async (_, e) => { if (e.Value) await CompleteManualAsync(step); };
            stack.Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { check, new Label { Text = "Complete", VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#2E7D32") } } });
        }
        return new Border { Content = stack, Padding = 10, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private void BuildSettingsArea()
    {
        var toggle = Button("⋮ Settings / Constraints", "#607D8B");
        var panel = new VerticalStackLayout { Spacing = 10, IsVisible = false }; toggle.Clicked += (_, _) => panel.IsVisible = !panel.IsVisible;
        panel.Children.Add(BuildProjects()); panel.Children.Add(BuildConstraints()); panel.Children.Add(BuildOverrideLog());
        _content.Children.Add(toggle); _content.Children.Add(panel);
    }

    private View BuildProjects()
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(new Label { Text = "Projects", FontSize = 17, FontAttributes = FontAttributes.Bold });
        foreach (var project in _projects)
        {
            string path = _projectFolders.TryGetValue(project.Id, out var saved) ? saved : "";
            var item = new VerticalStackLayout { Spacing = 4 };
            item.Children.Add(new Label { Text = project.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
            item.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(path) ? "Not set on this machine" : path,
                FontSize = 11, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap
            });
            var actions = new HorizontalStackLayout { Spacing = 4 };
            var folder = Small(string.IsNullOrWhiteSpace(path) ? "Set Folder" : "Change Folder", "#5B63EE");
            folder.Clicked += async (_, _) => await SetProjectFolderAndRefreshAsync(project);
            var rename = Small("Rename"); rename.Clicked += async (_, _) => await RenameProjectAsync(project);
            var delete = Small("Delete", "#C62828"); delete.Clicked += async (_, _) => await DeleteProjectAsync(project);
            actions.Children.Add(folder); actions.Children.Add(rename); actions.Children.Add(delete);
            item.Children.Add(actions); stack.Children.Add(item);
        }
        var add = Button("Add Project", "#2E7D32"); add.Clicked += async (_, _) => await AddProjectAsync(); stack.Children.Add(add);
        return Card(stack);
    }

    private View BuildConstraints()
    {
        var stack = new VerticalStackLayout { Spacing = 6 }; stack.Children.Add(new Label { Text = "Programming Constraints", FontSize = 17, FontAttributes = FontAttributes.Bold });
        foreach (var item in _constraints)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            row.Add(new Label { Text = item.Text, FontSize = 12, VerticalOptions = LayoutOptions.Center }, 0, 0);
            var controls = new HorizontalStackLayout { Spacing = 2 }; int index = _constraints.IndexOf(item);
            var edit = Small("Edit"); edit.Clicked += async (_, _) => { var text = await PromptAsync("Edit Constraint", "One-line constraint", item.Text); if (string.IsNullOrWhiteSpace(text)) return; item.Text = text.Trim(); await _service.SaveConstraintAsync(item); await RefreshAsync(); };
            var up = Small("↑"); var down = Small("↓"); up.IsEnabled = index > 0; down.IsEnabled = index < _constraints.Count - 1; up.Clicked += async (_, _) => await SwapConstraintsAsync(index, index - 1); down.Clicked += async (_, _) => await SwapConstraintsAsync(index, index + 1);
            var delete = Small("×", "#C62828"); delete.Clicked += async (_, _) => { await _service.DeleteConstraintAsync(item); await RefreshAsync(); };
            controls.Children.Add(edit); controls.Children.Add(up); controls.Children.Add(down); controls.Children.Add(delete); row.Add(controls, 1, 0); stack.Children.Add(row);
        }
        var entry = new Entry { Placeholder = "One programming constraint" }; var add = Button("Add", "#2E7D32"); add.Clicked += async (_, _) => { if (string.IsNullOrWhiteSpace(entry.Text)) return; await _service.SaveConstraintAsync(new DevProgrammingConstraint { Username = Username, Text = entry.Text.Trim(), SortOrder = _constraints.Count }); await RefreshAsync(); };
        var copy = Button("Copy all", "#1565C0"); copy.Clicked += async (_, _) => await Clipboard.SetTextAsync(ConstraintBlock());
        stack.Children.Add(entry); stack.Children.Add(new HorizontalStackLayout { Spacing = 5, Children = { add, copy } }); return Card(stack);
    }

    private View BuildOverrideLog()
    {
        var stack = new VerticalStackLayout { Spacing = 5 }; var toggle = Button("View Override Log", "#78909C"); var list = new VerticalStackLayout { Spacing = 4, IsVisible = false };
        toggle.Clicked += async (_, _) => { list.IsVisible = !list.IsVisible; if (!list.IsVisible) return; list.Children.Clear(); foreach (var item in await _service.GetOverridesAsync(Username)) list.Children.Add(new Label { Text = $"{item.OverriddenAt.ToLocalTime():dd MMM yyyy HH:mm} — {item.WorkflowName} / {item.StepName}", FontSize = 11 }); };
        stack.Children.Add(toggle); stack.Children.Add(list); return Card(stack);
    }

    private async Task StartRunAsync()
    {
        if (_projects.Count == 0) { await AddProjectAsync(); return; }
        if (_selectedProject == null) { await AlertAsync("Dev Workflow", "Select a project before running."); return; }
        _runStates.Clear(); foreach (var step in _steps) _runStates[step.Id] = ("pending", "Waiting", false); _runIndex = 0; BuildRunUI(); await RunCurrentStepAsync();
    }
    private async Task RunCurrentStepAsync()
    {
        while (_runIndex >= 0 && _runIndex < _steps.Count)
        {
            var step = _steps[_runIndex];
            if (step.StepType == "Manual") { _runStates[step.Id] = ("pending", "Complete this item", false); BuildRunUI(); return; }
            if (step.StepType == "Copy") { await Clipboard.SetTextAsync(step.UseAllConstraints ? ConstraintBlock() : step.Text); _runStates[step.Id] = ("passed", "Copied", false); _runIndex++; BuildRunUI(); continue; }
            if (_selectedProject == null)
            {
                _runStates[step.Id] = ("skipped", "No project selected", false); _runIndex++; BuildRunUI(); continue;
            }
            if (string.IsNullOrWhiteSpace(_projectFolder))
            {
                _runStates[step.Id] = ("skipped", $"No folder set for {_selectedProject.Name} on this machine", false); _runIndex++; BuildRunUI(); continue;
            }
            var result = await _service.CheckGitAsync(_projectFolder);
            if (!result.IsAvailable) { _runStates[step.Id] = ("skipped", result.Message, false); _runIndex++; BuildRunUI(); continue; }
            if (!result.Passed) { _runStates[step.Id] = ("failed", result.Message, result.IsBehind); BuildRunUI(); return; }
            _runStates[step.Id] = ("passed", result.Message, false); _runIndex++; BuildRunUI();
        }
        _runIndex = -1; BuildRunUI();
    }

    private async Task CompleteManualAsync(DevWorkflowStep step) { _runStates[step.Id] = ("passed", "Completed", false); _runIndex++; BuildRunUI(); await RunCurrentStepAsync(); }
    private async Task PullAndRecheckAsync() { if (_runIndex < 0 || _runIndex >= _steps.Count) return; var result = await _service.PullAsync(_projectFolder); _runStates[_steps[_runIndex].Id] = (result.Passed ? "passed" : result.IsAvailable ? "failed" : "skipped", result.Message, result.IsBehind); if (result.Passed || !result.IsAvailable) _runIndex++; BuildRunUI(); if (_runIndex >= 0) await RunCurrentStepAsync(); }
    private async Task OverrideAsync(DevWorkflowStep step) { await _service.SaveOverrideAsync(new DevWorkflowOverride { Username = Username, WorkflowId = _selected!.Id, StepId = step.Id, WorkflowName = _selected.Name, StepName = step.Text.Length > 0 ? step.Text : "Git up to date" }); _runStates[step.Id] = ("passed", "Overridden", false); _runIndex++; BuildRunUI(); await RunCurrentStepAsync(); }
    private string ConstraintBlock() => string.Join(Environment.NewLine, _constraints.Select(x => x.Text));
    private async Task SwapConstraintsAsync(int a, int b) { (_constraints[a].SortOrder, _constraints[b].SortOrder) = (_constraints[b].SortOrder, _constraints[a].SortOrder); await _service.SaveConstraintAsync(_constraints[a]); await _service.SaveConstraintAsync(_constraints[b]); await RefreshAsync(); }

    private async Task AddProjectAsync()
    {
        string? name = await PromptAsync("Add Project", "Project name", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var project = new DevWorkflowProject { Username = Username, Name = name.Trim(), SortOrder = _projects.Count };
        await _service.SaveProjectAsync(project);
        _selectedProject = project;
        await _service.SetLastUsedProjectIdAsync(project.Id);
        await _service.PickAndSaveProjectFolderAsync(project.Id);
        await RefreshAsync();
    }

    private async Task RenameProjectAsync(DevWorkflowProject project)
    {
        string? name = await PromptAsync("Rename Project", "Project name", project.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        project.Name = name.Trim(); await _service.SaveProjectAsync(project); await RefreshAsync();
    }

    private async Task DeleteProjectAsync(DevWorkflowProject project)
    {
        var page = HostPage();
        if (page == null || !await page.DisplayAlert("Delete Project", $"Delete '{project.Name}'?", "Delete", "Cancel")) return;
        await _service.DeleteProjectAsync(project);
        if (_selectedProject?.Id == project.Id) { _selectedProject = null; _projectFolder = ""; }
        await RefreshAsync();
    }

    private async Task SetProjectFolderAndRefreshAsync(DevWorkflowProject project)
    {
        string? selected = await _service.PickAndSaveProjectFolderAsync(project.Id);
        if (selected != null) await RefreshAsync();
    }

    private async Task SetProjectFolderAndRecheckAsync(DevWorkflowStep step)
    {
        if (_selectedProject == null) return;
        string? selected = await _service.PickAndSaveProjectFolderAsync(_selectedProject.Id);
        if (selected == null) return;
        _projectFolder = selected;
        _projectFolders[_selectedProject.Id] = selected;
        var result = await _service.CheckGitAsync(_projectFolder);
        _runStates[step.Id] = result.IsAvailable
            ? (result.Passed ? "passed" : "failed", result.Message, result.IsBehind)
            : ("skipped", result.Message, false);
        BuildRunUI();
    }

    private static bool NeedsProjectFolder(string message) =>
        message.StartsWith("No folder set for ", StringComparison.OrdinalIgnoreCase) ||
        message.StartsWith("Repo folder does not exist", StringComparison.OrdinalIgnoreCase);

    private Page? HostPage() => Application.Current?.Windows.FirstOrDefault()?.Page;
    private async Task<string?> PromptAsync(string title, string message, string initial) => HostPage() == null ? null : await HostPage()!.DisplayPromptAsync(title, message, "Save", "Cancel", initialValue: initial);
    private async Task AlertAsync(string title, string message) { if (_showAlert != null) await _showAlert(title, message); else if (HostPage() != null) await HostPage()!.DisplayAlert(title, message, "OK"); }
    private static Color StateColor(string state) => state switch { "passed" => Color.FromArgb("#2E7D32"), "failed" => Color.FromArgb("#C62828"), "skipped" => Color.FromArgb("#777777"), _ => Color.FromArgb("#E65100") };
    private static Frame Card(View content) => new() { Content = content, Padding = 10, CornerRadius = 9, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
    private static Button Button(string text, string color) => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(10, 0) };
    private static Button Small(string text, string color = "#607D8B") => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 10, CornerRadius = 5, HeightRequest = 30, Padding = new Thickness(7, 0) };
}
