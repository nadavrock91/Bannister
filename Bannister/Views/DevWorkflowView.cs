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
    private List<DevWorkflow> _workflows = new();
    private List<DevWorkflowStep> _steps = new();
    private List<DevProgrammingConstraint> _constraints = new();
    private DevWorkflow? _selected;
    private string _repoFolder = "";
    private readonly Dictionary<int, (string State, string Message, bool Behind)> _runStates = new();
    private int _runIndex = -1;

    public DevWorkflowView(
        Func<string> getUsername,
        DevWorkflowService service,
        bool compact = false,
        Func<string, string, Task>? showAlert = null)
    {
        _getUsername = getUsername;
        _service = service;
        _compact = compact;
        _showAlert = showAlert;
        BackgroundColor = compact ? Color.FromArgb("#F5F5F5") : Colors.Transparent;
        Content = new ScrollView { Content = _content };
    }

    private string Username => _getUsername();

    public async Task ActivateAsync()
    {
        await _service.InitAsync();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _workflows = await _service.GetWorkflowsAsync(Username);
        if (_selected != null)
            _selected = _workflows.FirstOrDefault(x => x.Id == _selected.Id);
        _selected ??= _workflows.FirstOrDefault();
        _steps = _selected == null ? new() : await _service.GetStepsAsync(Username, _selected.Id);
        _constraints = await _service.GetConstraintsAsync(Username);
        _repoFolder = await _service.GetRepoFolderAsync();
        BuildUI();
    }

    private void BuildUI()
    {
        _content.Children.Clear();
        _content.Padding = _compact ? 8 : 16;
        _content.Children.Add(new Label
        {
            Text = "Dev Workflow", FontSize = _compact ? 21 : 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = _compact ? Color.FromArgb("#2D1B6B") : Color.FromArgb("#222")
        });
        BuildRepoSection();
        BuildWorkflowSection();
        BuildConstraintSection();
        BuildOverrideLog();
    }

    private void BuildRepoSection()
    {
        var choose = Button("Set Repo Folder", "#5B63EE");
        choose.Clicked += async (_, _) => await PickRepoFolderAsync();
        _content.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = "Repository", FontSize = 16, FontAttributes = FontAttributes.Bold },
                new Label { Text = string.IsNullOrWhiteSpace(_repoFolder) ? "No folder selected" : _repoFolder, FontSize = 11, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap },
                choose
            }
        }));
    }

    private void BuildWorkflowSection()
    {
        var section = new VerticalStackLayout { Spacing = 8 };
        section.Children.Add(new Label { Text = "Workflows", FontSize = 18, FontAttributes = FontAttributes.Bold });
        var tabs = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row };
        foreach (var workflow in _workflows)
        {
            var open = Button(workflow.Name, workflow.Id == _selected?.Id ? "#3949AB" : "#78909C");
            open.Clicked += async (_, _) => { _selected = workflow; _runIndex = -1; _runStates.Clear(); await RefreshAsync(); };
            tabs.Children.Add(open);
        }
        section.Children.Add(tabs);
        var name = new Entry { Placeholder = "New workflow name", FontSize = 13 };
        var add = Button("Add Workflow", "#2E7D32");
        add.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) return;
            var item = new DevWorkflow { Username = Username, Name = name.Text.Trim(), SortOrder = _workflows.Count };
            await _service.SaveWorkflowAsync(item); _selected = item; await RefreshAsync();
        };
        section.Children.Add(name); section.Children.Add(add);
        if (_selected != null) BuildSelectedWorkflow(section);
        _content.Children.Add(Card(section));
    }

    private void BuildSelectedWorkflow(VerticalStackLayout section)
    {
        var actions = new HorizontalStackLayout { Spacing = 5 };
        var rename = Small("Rename");
        rename.Clicked += async (_, _) =>
        {
            var value = await PromptAsync("Rename Workflow", "Workflow name", _selected!.Name);
            if (string.IsNullOrWhiteSpace(value)) return;
            _selected.Name = value.Trim(); await _service.SaveWorkflowAsync(_selected); await RefreshAsync();
        };
        var up = Small("↑"); var down = Small("↓");
        int index = _workflows.FindIndex(x => x.Id == _selected.Id);
        up.IsEnabled = index > 0; down.IsEnabled = index >= 0 && index < _workflows.Count - 1;
        up.Clicked += async (_, _) => { await SwapWorkflowsAsync(index, index - 1); };
        down.Clicked += async (_, _) => { await SwapWorkflowsAsync(index, index + 1); };
        var delete = Small("Delete", "#C62828");
        delete.Clicked += async (_, _) =>
        {
            if (!await ConfirmAsync("Delete Workflow", $"Delete '{_selected!.Name}'?")) return;
            await _service.DeleteWorkflowAsync(_selected); _selected = null; await RefreshAsync();
        };
        actions.Children.Add(rename); actions.Children.Add(up); actions.Children.Add(down); actions.Children.Add(delete);
        section.Children.Add(actions);

        var run = Button("Run", "#6A1B9A");
        run.Clicked += async (_, _) => await StartRunAsync();
        section.Children.Add(run);
        foreach (var step in _steps) section.Children.Add(BuildStepCard(step));
        section.Children.Add(BuildAddStepForm());
    }

    private View BuildStepCard(DevWorkflowStep step)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        string label = step.StepType == "Check" ? "Git up to date" : step.UseAllConstraints ? "All programming constraints" : step.Text;
        stack.Children.Add(new Label { Text = $"{step.SortOrder + 1}. [{step.StepType}] {label}", FontAttributes = FontAttributes.Bold, FontSize = 13 });
        if (_runStates.TryGetValue(step.Id, out var state))
        {
            stack.Children.Add(new Label { Text = $"{state.State}: {state.Message}", FontSize = 12, TextColor = StateColor(state.State), FontAttributes = FontAttributes.Bold });
            if (state.State == "failed")
            {
                var row = new HorizontalStackLayout { Spacing = 5 };
                var retry = Small("Re-check"); retry.Clicked += async (_, _) => await RunCurrentStepAsync(); row.Children.Add(retry);
                if (state.Behind) { var pull = Small("Pull", "#1565C0"); pull.Clicked += async (_, _) => await PullAndRecheckAsync(); row.Children.Add(pull); }
                var over = Small("Override", "#E65100"); over.Clicked += async (_, _) => await OverrideAsync(step); row.Children.Add(over);
                stack.Children.Add(row);
            }
            else if (state.State == "pending" && step.StepType == "Manual" && _runIndex >= 0 && _runIndex < _steps.Count && _steps[_runIndex].Id == step.Id)
            {
                var check = new CheckBox { Color = Color.FromArgb("#2E7D32") };
                check.CheckedChanged += async (_, e) =>
                {
                    if (e.Value) await CompleteManualAsync(step);
                };
                stack.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        check,
                        new Label
                        {
                            Text = "Complete",
                            VerticalOptions = LayoutOptions.Center,
                            TextColor = Color.FromArgb("#2E7D32")
                        }
                    }
                });
            }
        }
        var controls = new HorizontalStackLayout { Spacing = 4 };
        var edit = Small("Edit"); edit.IsVisible = step.StepType != "Check";
        edit.Clicked += async (_, _) => { var value = await PromptAsync("Edit Step", "Step text", step.Text); if (value == null) return; step.Text = value; await _service.SaveStepAsync(step); await RefreshAsync(); };
        int i = _steps.IndexOf(step); var up = Small("↑"); var down = Small("↓"); up.IsEnabled = i > 0; down.IsEnabled = i < _steps.Count - 1;
        up.Clicked += async (_, _) => await SwapStepsAsync(i, i - 1); down.Clicked += async (_, _) => await SwapStepsAsync(i, i + 1);
        var del = Small("Delete", "#C62828"); del.Clicked += async (_, _) => { await _service.DeleteStepAsync(step); await RefreshAsync(); };
        controls.Children.Add(edit); controls.Children.Add(up); controls.Children.Add(down); controls.Children.Add(del); stack.Children.Add(controls);
        return new Border { Content = stack, Padding = 8, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private View BuildAddStepForm()
    {
        var type = new Picker { Title = "Step type", ItemsSource = new[] { "Manual", "Copy", "Check" }, SelectedIndex = 0 };
        var text = new Entry { Placeholder = "Step or custom copy text" };
        var all = new CheckBox();
        var allRow = new HorizontalStackLayout { Children = { all, new Label { Text = "Copy all programming constraints", VerticalOptions = LayoutOptions.Center } } };
        var add = Button("Add Step", "#2E7D32");
        add.Clicked += async (_, _) =>
        {
            string selectedType = type.SelectedItem?.ToString() ?? "Manual";
            if (selectedType != "Check" && !all.IsChecked && string.IsNullOrWhiteSpace(text.Text)) return;
            await _service.SaveStepAsync(new DevWorkflowStep { Username = Username, WorkflowId = _selected!.Id, SortOrder = _steps.Count, StepType = selectedType, Text = text.Text?.Trim() ?? "", UseAllConstraints = selectedType == "Copy" && all.IsChecked, CheckKind = selectedType == "Check" ? "GitUpToDate" : "" });
            await RefreshAsync();
        };
        return new VerticalStackLayout { Spacing = 5, Children = { new Label { Text = "Add Step", FontAttributes = FontAttributes.Bold }, type, text, allRow, add } };
    }

    private void BuildConstraintSection()
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label { Text = "Programming Constraints", FontSize = 18, FontAttributes = FontAttributes.Bold });
        foreach (var item in _constraints)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            row.Add(new Label { Text = item.Text, FontSize = 12, VerticalOptions = LayoutOptions.Center }, 0, 0);
            var buttons = new HorizontalStackLayout { Spacing = 2 }; int i = _constraints.IndexOf(item);
            var edit = Small("Edit"); edit.Clicked += async (_, _) => { var v = await PromptAsync("Edit Constraint", "One-line constraint", item.Text); if (string.IsNullOrWhiteSpace(v)) return; item.Text = v.Trim(); await _service.SaveConstraintAsync(item); await RefreshAsync(); };
            var up = Small("↑"); var down = Small("↓"); up.IsEnabled = i > 0; down.IsEnabled = i < _constraints.Count - 1; up.Clicked += async (_, _) => await SwapConstraintsAsync(i, i - 1); down.Clicked += async (_, _) => await SwapConstraintsAsync(i, i + 1);
            var del = Small("×", "#C62828"); del.Clicked += async (_, _) => { await _service.DeleteConstraintAsync(item); await RefreshAsync(); };
            buttons.Children.Add(edit); buttons.Children.Add(up); buttons.Children.Add(down); buttons.Children.Add(del); row.Add(buttons, 1, 0); stack.Children.Add(row);
        }
        var entry = new Entry { Placeholder = "One programming constraint" }; var add = Button("Add", "#2E7D32");
        add.Clicked += async (_, _) => { if (string.IsNullOrWhiteSpace(entry.Text)) return; await _service.SaveConstraintAsync(new DevProgrammingConstraint { Username = Username, Text = entry.Text.Trim(), SortOrder = _constraints.Count }); await RefreshAsync(); };
        var copy = Button("Copy all", "#1565C0"); copy.Clicked += async (_, _) => await Clipboard.SetTextAsync(ConstraintBlock());
        stack.Children.Add(entry); stack.Children.Add(new HorizontalStackLayout { Spacing = 5, Children = { add, copy } }); _content.Children.Add(Card(stack));
    }

    private void BuildOverrideLog()
    {
        var toggle = Button("View Override Log", "#78909C"); var list = new VerticalStackLayout { Spacing = 4, IsVisible = false };
        toggle.Clicked += async (_, _) => { list.IsVisible = !list.IsVisible; if (!list.IsVisible) return; list.Children.Clear(); foreach (var x in await _service.GetOverridesAsync(Username)) list.Children.Add(new Label { Text = $"{x.OverriddenAt.ToLocalTime():dd MMM yyyy HH:mm} — {x.WorkflowName} / {x.StepName}", FontSize = 11 }); };
        _content.Children.Add(toggle); _content.Children.Add(list);
    }

    private async Task StartRunAsync() { _runStates.Clear(); foreach (var s in _steps) _runStates[s.Id] = ("pending", "Waiting", false); _runIndex = 0; BuildUI(); await RunCurrentStepAsync(); }
    private async Task RunCurrentStepAsync()
    {
        while (_runIndex >= 0 && _runIndex < _steps.Count)
        {
            var step = _steps[_runIndex];
            if (step.StepType == "Manual") { _runStates[step.Id] = ("pending", "Complete this item", false); BuildUI(); return; }
            if (step.StepType == "Copy") { await Clipboard.SetTextAsync(step.UseAllConstraints ? ConstraintBlock() : step.Text); _runStates[step.Id] = ("passed", "Copied", false); _runIndex++; BuildUI(); continue; }
            var result = await _service.CheckGitAsync(_repoFolder);
            if (!result.IsAvailable) { _runStates[step.Id] = ("skipped", result.Message, false); _runIndex++; BuildUI(); continue; }
            if (!result.Passed) { _runStates[step.Id] = ("failed", result.Message, result.IsBehind); BuildUI(); return; }
            _runStates[step.Id] = ("passed", result.Message, false); _runIndex++; BuildUI();
        }
        _runIndex = -1; BuildUI();
    }

    private async Task CompleteManualAsync(DevWorkflowStep step) { _runStates[step.Id] = ("passed", "Completed", false); _runIndex++; BuildUI(); await RunCurrentStepAsync(); }
    private async Task PullAndRecheckAsync() { if (_runIndex < 0 || _runIndex >= _steps.Count) return; var r = await _service.PullAsync(_repoFolder); _runStates[_steps[_runIndex].Id] = (r.Passed ? "passed" : r.IsAvailable ? "failed" : "skipped", r.Message, r.IsBehind); if (r.Passed || !r.IsAvailable) _runIndex++; BuildUI(); if (_runIndex >= 0) await RunCurrentStepAsync(); }
    private async Task OverrideAsync(DevWorkflowStep step) { await _service.SaveOverrideAsync(new DevWorkflowOverride { Username = Username, WorkflowId = _selected!.Id, StepId = step.Id, WorkflowName = _selected.Name, StepName = step.Text.Length > 0 ? step.Text : "Git up to date" }); _runStates[step.Id] = ("passed", "Overridden", false); _runIndex++; BuildUI(); await RunCurrentStepAsync(); }

    private string ConstraintBlock() => string.Join(Environment.NewLine, _constraints.Select(x => x.Text));
    private async Task SwapWorkflowsAsync(int a, int b) { (_workflows[a].SortOrder, _workflows[b].SortOrder) = (_workflows[b].SortOrder, _workflows[a].SortOrder); await _service.SaveWorkflowAsync(_workflows[a]); await _service.SaveWorkflowAsync(_workflows[b]); await RefreshAsync(); }
    private async Task SwapStepsAsync(int a, int b) { (_steps[a].SortOrder, _steps[b].SortOrder) = (_steps[b].SortOrder, _steps[a].SortOrder); await _service.SaveStepAsync(_steps[a]); await _service.SaveStepAsync(_steps[b]); await RefreshAsync(); }
    private async Task SwapConstraintsAsync(int a, int b) { (_constraints[a].SortOrder, _constraints[b].SortOrder) = (_constraints[b].SortOrder, _constraints[a].SortOrder); await _service.SaveConstraintAsync(_constraints[a]); await _service.SaveConstraintAsync(_constraints[b]); await RefreshAsync(); }

    private async Task PickRepoFolderAsync()
    {
#if WINDOWS
        var picker = new Windows.Storage.Pickers.FolderPicker(); picker.FileTypeFilter.Add("*");
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView == null) return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window.Handler.PlatformView);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) { await _service.SetRepoFolderAsync(folder.Path); await RefreshAsync(); }
#else
        await AlertAsync("Repository", "Folder selection is not available on this device.");
#endif
    }

    private Page? HostPage() => Application.Current?.Windows.FirstOrDefault()?.Page;
    private async Task<string?> PromptAsync(string title, string message, string initial) => HostPage() == null ? null : await HostPage()!.DisplayPromptAsync(title, message, "Save", "Cancel", initialValue: initial);
    private async Task<bool> ConfirmAsync(string title, string message) => HostPage() != null && await HostPage()!.DisplayAlert(title, message, "Delete", "Cancel");
    private async Task AlertAsync(string title, string message) { if (_showAlert != null) await _showAlert(title, message); else if (HostPage() != null) await HostPage()!.DisplayAlert(title, message, "OK"); }
    private static Color StateColor(string state) => state switch { "passed" => Color.FromArgb("#2E7D32"), "failed" => Color.FromArgb("#C62828"), "skipped" => Color.FromArgb("#777777"), _ => Color.FromArgb("#E65100") };
    private static Frame Card(View content) => new() { Content = content, Padding = 10, CornerRadius = 9, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
    private static Button Button(string text, string color) => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(10, 0) };
    private static Button Small(string text, string color = "#607D8B") => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 10, CornerRadius = 5, HeightRequest = 30, Padding = new Thickness(7, 0) };
}
