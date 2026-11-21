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
    private readonly Dictionary<int, StepChecklistState> _runStates = new();

    private sealed class StepChecklistState
    {
        public bool Done { get; set; }
        public bool Skipped { get; set; }
        public string Message { get; set; } = "";
        public bool Behind { get; set; }
        public string LastInput { get; set; } = "";
    }

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
            if (_steps.Count > 0 && _steps.All(IsFinished))
                _content.Children.Add(new Label { Text = "Workflow complete", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32") });
        }
        BuildSettingsArea();
    }

    private View BuildRunControls()
    {
        var row = new HorizontalStackLayout { Spacing = 7 };
        var reset = Button("Reset", "#607D8B");
        reset.Clicked += (_, _) => { ResetRunState(); BuildRunUI(); };
        row.Children.Add(reset);
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
                ResetRunState();
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
            _selected = workflow; ResetRunState();
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
        editor.Saved += async saved => { _selected = saved; ResetRunState(); await RefreshAsync(); };
        editor.Cancelled += async () => await RefreshAsync();
        editor.Deleted += async () => { _selected = null; ResetRunState(); await RefreshAsync(); };
        Content = editor; await editor.ActivateAsync();
    }

    private View BuildRunStep(DevWorkflowStep step)
    {
        string text = step.StepType == "Check" ? "Git up to date" : step.UseAllConstraints ? "All programming constraints" : step.Text;
        var state = StateFor(step);
        bool current = CurrentStep()?.Id == step.Id;
        var stack = new VerticalStackLayout { Spacing = 5 };
        var heading = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        var checkBox = new CheckBox { IsChecked = state.Done, Color = Color.FromArgb("#2E7D32"), VerticalOptions = LayoutOptions.Center };
        checkBox.CheckedChanged += async (_, e) => await SetDoneAsync(step, e.Value);
        heading.Add(checkBox, 0, 0);
        heading.Add(new Label
        {
            Text = $"{step.SortOrder + 1}. [{step.StepType}] {text}", FontSize = 14,
            FontAttributes = current ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center
        }, 1, 0);
        var headingActions = new HorizontalStackLayout { Spacing = 4 };
        if (step.StepType is "Check" or "Wrap & Copy")
        {
            var run = Small(string.IsNullOrWhiteSpace(state.Message) ? "Run" : "Re-run", "#6A1B9A");
            run.IsEnabled = !state.Skipped;
            run.Clicked += async (_, _) =>
            {
                if (step.StepType == "Check") await RunCheckAsync(step);
                else await RunWrapAndCopyAsync(step);
            };
            headingActions.Children.Add(run);
        }
        var skip = Small(state.Skipped ? "Unskip" : "Skip");
        skip.IsEnabled = !state.Done;
        skip.Clicked += async (_, _) => await ToggleSkipAsync(step);
        headingActions.Children.Add(skip);
        heading.Add(headingActions, 2, 0); stack.Children.Add(heading);

        if (state.Skipped)
            stack.Children.Add(new Label { Text = "skipped", FontSize = 12, TextColor = Color.FromArgb("#777777") });
        else if (!string.IsNullOrWhiteSpace(state.Message))
            stack.Children.Add(new Label { Text = state.Message, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = state.Done ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828") });

        var actions = new HorizontalStackLayout { Spacing = 5 };
        if (step.StepType == "Copy" && !state.Done && !state.Skipped)
        {
            var copy = Small("Copy", "#1565C0"); copy.Clicked += async (_, _) => await CopyAndCompleteAsync(step); actions.Children.Add(copy);
        }
        if (step.StepType == "Check" && !state.Done && !state.Skipped && !string.IsNullOrWhiteSpace(state.Message))
        {
            if (state.Behind) { var pull = Small("Pull", "#1565C0"); pull.Clicked += async (_, _) => await PullAndRecheckAsync(step); actions.Children.Add(pull); }
            if (NeedsProjectFolder(state.Message)) { var folder = Small("Set Folder", "#5B63EE"); folder.Clicked += async (_, _) => await SetProjectFolderAndRecheckAsync(step); actions.Children.Add(folder); }
        }
        if (actions.Children.Count > 0) stack.Children.Add(actions);
        return new Border
        {
            Content = stack, Padding = 10,
            Stroke = current ? Color.FromArgb("#6A1B9A") : Color.FromArgb("#DDDDDD"),
            StrokeThickness = current ? 2 : 1,
            BackgroundColor = current ? Color.FromArgb("#F3E5F5") : Colors.White
        };
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

    private StepChecklistState StateFor(DevWorkflowStep step)
    {
        if (!_runStates.TryGetValue(step.Id, out var state))
        {
            state = new StepChecklistState();
            _runStates[step.Id] = state;
        }
        return state;
    }

    private bool IsFinished(DevWorkflowStep step) { var state = StateFor(step); return state.Done || state.Skipped; }
    private DevWorkflowStep? CurrentStep() => _steps.FirstOrDefault(x => !IsFinished(x));

    private async Task SetDoneAsync(DevWorkflowStep step, bool done)
    {
        var state = StateFor(step);
        if (state.Done == done) return;
        if (done && step.StepType == "Check" && !string.IsNullOrWhiteSpace(state.Message))
        {
            await LogOverrideAsync(step);
            state.Message = "Overridden";
        }
        else if (!done)
        {
            state.Message = "";
            state.Behind = false;
        }
        state.Done = done; state.Skipped = false;
        BuildRunUI();
    }

    private async Task ToggleSkipAsync(DevWorkflowStep step)
    {
        var state = StateFor(step);
        if (state.Done) return;
        if (!state.Skipped && step.StepType == "Check" && !string.IsNullOrWhiteSpace(state.Message))
            await LogOverrideAsync(step);
        state.Skipped = !state.Skipped;
        if (!state.Skipped) { state.Message = ""; state.Behind = false; }
        BuildRunUI();
    }

    private async Task CopyAndCompleteAsync(DevWorkflowStep step)
    {
        await Clipboard.SetTextAsync(step.UseAllConstraints ? ConstraintBlock() : step.Text);
        var state = StateFor(step); state.Done = true; state.Skipped = false; state.Message = "Copied";
        BuildRunUI();
    }

    private async Task RunCheckAsync(DevWorkflowStep step)
    {
        var state = StateFor(step);
        state.Done = false; state.Skipped = false; state.Behind = false;
        if (_selectedProject == null) { state.Message = "No project selected"; state.Behind = false; BuildRunUI(); return; }
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            state.Message = $"No folder set for {_selectedProject.Name} on this machine"; state.Behind = false; BuildRunUI(); return;
        }
        var result = await _service.CheckGitAsync(_projectFolder);
        state.Message = result.Message; state.Behind = result.IsBehind;
        if (result.IsAvailable && result.Passed)
        {
            state.Done = true; state.Skipped = false;
            BuildRunUI();
        }
        else BuildRunUI();
    }

    private async Task RunWrapAndCopyAsync(DevWorkflowStep step)
    {
        var state = StateFor(step);
        string? input = await ShowMultilineInputAsync("Paste your task", state.LastInput);
        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(step.WrapPrefix)) parts.Add(step.WrapPrefix.Trim());
        parts.Add(input.Trim());
        if (!string.IsNullOrWhiteSpace(step.WrapSuffix)) parts.Add(step.WrapSuffix.Trim());
        if (step.AppendConstraints && !string.IsNullOrWhiteSpace(ConstraintBlock()))
            parts.Add(ConstraintBlock().Trim());
        string result = string.Join(Environment.NewLine + Environment.NewLine, parts);
        await Clipboard.SetTextAsync(result);
        state.LastInput = input;
        state.Done = true;
        state.Skipped = false;
        state.Message = $"Copied ({result.Length} characters)";
        BuildRunUI();
    }

    private async Task<string?> ShowMultilineInputAsync(string title, string initialValue)
    {
        var navigation = HostPage()?.Navigation;
        if (navigation == null) return null;
        var page = new DevWorkflowTextInputPage(title, initialValue);
        await navigation.PushModalAsync(page);
        return await page.WaitForResultAsync();
    }

    private async Task PullAndRecheckAsync(DevWorkflowStep step)
    {
        var state = StateFor(step);
        var result = await _service.PullAsync(_projectFolder);
        state.Message = result.Message; state.Behind = result.IsBehind;
        if (result.IsAvailable && result.Passed)
        {
            state.Done = true;
            BuildRunUI();
        }
        else BuildRunUI();
    }

    private async Task LogOverrideAsync(DevWorkflowStep step) =>
        await _service.SaveOverrideAsync(new DevWorkflowOverride
        {
            Username = Username, WorkflowId = _selected!.Id, StepId = step.Id,
            WorkflowName = _selected.Name,
            StepName = step.Text.Length > 0 ? step.Text : "Git up to date"
        });

    private void ResetRunState() => _runStates.Clear();
    private string ConstraintBlock() => string.Join(Environment.NewLine, _constraints.Select(x => x.Text));
    private async Task SwapConstraintsAsync(int a, int b) { (_constraints[a].SortOrder, _constraints[b].SortOrder) = (_constraints[b].SortOrder, _constraints[a].SortOrder); await _service.SaveConstraintAsync(_constraints[a]); await _service.SaveConstraintAsync(_constraints[b]); await RefreshAsync(); }

    private async Task AddProjectAsync()
    {
        string? name = await PromptAsync("Add Project", "Project name", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var project = new DevWorkflowProject { Username = Username, Name = name.Trim(), SortOrder = _projects.Count };
        await _service.SaveProjectAsync(project);
        _selectedProject = project;
        ResetRunState();
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
        if (_selectedProject?.Id == project.Id) { _selectedProject = null; _projectFolder = ""; ResetRunState(); }
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
        await RunCheckAsync(step);
    }

    private static bool NeedsProjectFolder(string message) =>
        message.StartsWith("No folder set for ", StringComparison.OrdinalIgnoreCase) ||
        message.StartsWith("Repo folder does not exist", StringComparison.OrdinalIgnoreCase);

    private Page? HostPage() => Application.Current?.Windows.FirstOrDefault()?.Page;
    private async Task<string?> PromptAsync(string title, string message, string initial) => HostPage() == null ? null : await HostPage()!.DisplayPromptAsync(title, message, "Save", "Cancel", initialValue: initial);
    private async Task AlertAsync(string title, string message) { if (_showAlert != null) await _showAlert(title, message); else if (HostPage() != null) await HostPage()!.DisplayAlert(title, message, "OK"); }
    private static Frame Card(View content) => new() { Content = content, Padding = 10, CornerRadius = 9, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
    private static Button Button(string text, string color) => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(10, 0) };
    private static Button Small(string text, string color = "#607D8B") => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 10, CornerRadius = 5, HeightRequest = 30, Padding = new Thickness(7, 0) };

    private sealed class DevWorkflowTextInputPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _result = new();

        public DevWorkflowTextInputPage(string title, string initialValue)
        {
            BackgroundColor = Color.FromArgb("#80000000");
            var editor = new Editor
            {
                Placeholder = "Paste your task",
                Text = initialValue,
                HeightRequest = 260,
                AutoSize = EditorAutoSizeOption.Disabled,
                BackgroundColor = Color.FromArgb("#FAFAFA")
            };
            var paste = new Button { Text = "Paste", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White };
            var ok = new Button { Text = "OK", BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White };
            var cancel = new Button { Text = "Cancel", BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#333") };
            paste.Clicked += async (_, _) =>
            {
                try
                {
                    if (!Clipboard.HasText) return;
                    string? clipboardText = await Clipboard.GetTextAsync();
                    if (string.IsNullOrEmpty(clipboardText)) return;
                    string current = editor.Text ?? "";
                    int start = Math.Clamp(editor.CursorPosition, 0, current.Length);
                    int length = Math.Clamp(editor.SelectionLength, 0, current.Length - start);
                    editor.Text = current.Remove(start, length).Insert(start, clipboardText);
                    editor.CursorPosition = start + clipboardText.Length;
                    editor.SelectionLength = 0;
                }
                catch
                {
                    // Clipboard access can be unavailable; leave the input unchanged.
                }
                finally
                {
                    editor.Focus();
                }
            };
            ok.Clicked += (_, _) => Close(editor.Text);
            cancel.Clicked += (_, _) => Close(null);
            var buttons = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            buttons.Add(paste, 0, 0); buttons.Add(ok, 1, 0); buttons.Add(cancel, 2, 0);
            Content = new Grid
            {
                Padding = 20,
                Children =
                {
                    new Frame
                    {
                        WidthRequest = 560, Padding = 20, CornerRadius = 12,
                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold },
                                editor,
                                buttons
                            }
                        }
                    }
                }
            };
        }

        public Task<string?> WaitForResultAsync() => _result.Task;

        private async void Close(string? value)
        {
            if (!_result.TrySetResult(value)) return;
            await Navigation.PopModalAsync();
        }

        protected override void OnDisappearing()
        {
            _result.TrySetResult(null);
            base.OnDisappearing();
        }
    }
}
