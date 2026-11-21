using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DevWorkflowEditorView : ContentView
{
    private readonly string _username;
    private readonly DevWorkflowService _service;
    private readonly bool _compact;
    private readonly DevWorkflow? _source;
    private readonly VerticalStackLayout _content = new() { Spacing = 10 };
    private readonly List<DevWorkflowStep> _steps = new();
    private Entry _name = null!;
    private string _draftName = "";
    private DevWorkflowStep? _editingStep;

    public event Func<DevWorkflow, Task>? Saved;
    public event Func<Task>? Cancelled;
    public event Func<Task>? Deleted;

    public DevWorkflowEditorView(
        string username,
        DevWorkflowService service,
        DevWorkflow? workflow,
        bool compact = false)
    {
        _username = username;
        _service = service;
        _source = workflow;
        _compact = compact;
        Content = new ScrollView { Content = _content };
    }

    public async Task ActivateAsync()
    {
        _steps.Clear();
        _draftName = _source?.Name ?? "";
        if (_source != null)
        {
            foreach (var item in await _service.GetStepsAsync(_username, _source.Id))
                _steps.Add(Clone(item));
        }
        BuildUI();
    }

    private void BuildUI()
    {
        _content.Children.Clear();
        _content.Padding = _compact ? 8 : 16;
        _content.Children.Add(new Label
        {
            Text = _source == null ? "New Workflow" : "Edit Workflow",
            FontSize = _compact ? 21 : 25,
            FontAttributes = FontAttributes.Bold,
            TextColor = _compact ? Color.FromArgb("#2D1B6B") : Color.FromArgb("#222")
        });
        _name = new Entry
        {
            Placeholder = "Workflow name",
            Text = _draftName,
            FontSize = 14
        };
        _name.TextChanged += (_, e) => _draftName = e.NewTextValue ?? "";
        _content.Children.Add(_name);
        _content.Children.Add(new Label { Text = "Steps", FontSize = 18, FontAttributes = FontAttributes.Bold });
        for (int i = 0; i < _steps.Count; i++)
            _content.Children.Add(BuildStepRow(_steps[i], i));
        _content.Children.Add(BuildStepForm());

        var save = Button("Save", "#2E7D32");
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = Button("Cancel", "#607D8B");
        cancel.Clicked += async (_, _) => { if (Cancelled != null) await Cancelled.Invoke(); };
        var actions = new HorizontalStackLayout { Spacing = 7, Children = { save, cancel } };
        if (_source != null)
        {
            var delete = Button("Delete Workflow", "#C62828");
            delete.Clicked += async (_, _) => await DeleteAsync();
            actions.Children.Add(delete);
        }
        _content.Children.Add(actions);
    }

    private View BuildStepRow(DevWorkflowStep step, int index)
    {
        string text = step.StepType == "Check"
            ? "Git up to date"
            : step.UseAllConstraints ? "All programming constraints" : step.Text;
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Children.Add(new Label
        {
            Text = $"{index + 1}. [{step.StepType}] {text}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        });
        var buttons = new HorizontalStackLayout { Spacing = 4 };
        var edit = Small("Edit"); edit.Clicked += (_, _) => { _editingStep = step; BuildUI(); };
        var delete = Small("Delete", "#C62828"); delete.Clicked += (_, _) => { _steps.Remove(step); if (_editingStep == step) _editingStep = null; BuildUI(); };
        var up = Small("↑"); up.IsEnabled = index > 0; up.Clicked += (_, _) => { Move(index, index - 1); };
        var down = Small("↓"); down.IsEnabled = index < _steps.Count - 1; down.Clicked += (_, _) => { Move(index, index + 1); };
        buttons.Children.Add(edit); buttons.Children.Add(delete); buttons.Children.Add(up); buttons.Children.Add(down);
        stack.Children.Add(buttons);
        return new Border { Content = stack, Padding = 8, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Colors.White };
    }

    private View BuildStepForm()
    {
        var current = _editingStep;
        var stepTypes = new[] { "Manual", "Copy", "Check", "Wrap & Copy" };
        var type = new Picker
        {
            Title = "Step type",
            ItemsSource = stepTypes,
            SelectedIndex = Math.Max(0, Array.IndexOf(stepTypes, current?.StepType ?? "Manual"))
        };
        var text = new Entry
        {
            Placeholder = "Step text",
            Text = current?.Text ?? ""
        };
        var all = new CheckBox { IsChecked = current?.UseAllConstraints ?? false };
        var allRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                all,
                new Label { Text = "Copy all programming constraints", VerticalOptions = LayoutOptions.Center }
            }
        };
        var checkPicker = new Picker
        {
            Title = "Check",
            ItemsSource = new[] { "Git up to date" },
            SelectedIndex = 0
        };
        var prefix = new Editor
        {
            Placeholder = "Prefix",
            Text = current?.WrapPrefix ?? "",
            HeightRequest = 90,
            AutoSize = EditorAutoSizeOption.Disabled
        };
        var suffix = new Editor
        {
            Placeholder = "Suffix",
            Text = current?.WrapSuffix ?? "",
            HeightRequest = 90,
            AutoSize = EditorAutoSizeOption.Disabled
        };
        var appendConstraints = new CheckBox { IsChecked = current?.AppendConstraints ?? false };
        var appendConstraintsRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                appendConstraints,
                new Label { Text = "Append programming constraints", VerticalOptions = LayoutOptions.Center }
            }
        };
        void UpdateFields()
        {
            string selected = type.SelectedItem?.ToString() ?? "Manual";
            text.IsVisible = selected != "Check";
            text.Placeholder = selected == "Wrap & Copy" ? "Step name" : "Step text";
            allRow.IsVisible = selected == "Copy";
            checkPicker.IsVisible = selected == "Check";
            prefix.IsVisible = selected == "Wrap & Copy";
            suffix.IsVisible = selected == "Wrap & Copy";
            appendConstraintsRow.IsVisible = selected == "Wrap & Copy";
        }
        type.SelectedIndexChanged += (_, _) => UpdateFields();
        UpdateFields();
        var apply = Button(current == null ? "Add Step" : "Update Step", "#1565C0");
        apply.Clicked += (_, _) =>
        {
            string selected = type.SelectedItem?.ToString() ?? "Manual";
            if (selected == "Wrap & Copy" &&
                (string.IsNullOrWhiteSpace(text.Text) ||
                 (string.IsNullOrWhiteSpace(prefix.Text) && string.IsNullOrWhiteSpace(suffix.Text) && !appendConstraints.IsChecked))) return;
            if (selected != "Check" && !(selected == "Copy" && all.IsChecked) && string.IsNullOrWhiteSpace(text.Text)) return;
            var item = current ?? new DevWorkflowStep();
            item.StepType = selected;
            item.Text = text.Text?.Trim() ?? "";
            item.UseAllConstraints = selected == "Copy" && all.IsChecked;
            item.CheckKind = selected == "Check" ? "GitUpToDate" : "";
            item.WrapPrefix = selected == "Wrap & Copy" ? prefix.Text?.Trim() ?? "" : "";
            item.WrapSuffix = selected == "Wrap & Copy" ? suffix.Text?.Trim() ?? "" : "";
            item.AppendConstraints = selected == "Wrap & Copy" && appendConstraints.IsChecked;
            if (current == null) _steps.Add(item);
            _editingStep = null;
            BuildUI();
        };
        var form = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = current == null ? "Add Step" : "Edit Step", FontSize = 16, FontAttributes = FontAttributes.Bold },
                type, text, allRow, checkPicker, prefix, suffix, appendConstraintsRow, apply
            }
        };
        if (current != null)
        {
            var cancel = Small("Cancel edit");
            cancel.Clicked += (_, _) => { _editingStep = null; BuildUI(); };
            form.Children.Add(cancel);
        }
        return Card(form);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_draftName)) return;
        var workflow = _source ?? new DevWorkflow
        {
            Username = _username,
            SortOrder = (await _service.GetWorkflowsAsync(_username)).Count
        };
        workflow.Name = _draftName.Trim();
        await _service.SaveWorkflowWithStepsAsync(workflow, _steps);
        await _service.SetLastUsedWorkflowIdAsync(workflow.Id);
        if (Saved != null) await Saved.Invoke(workflow);
    }

    private async Task DeleteAsync()
    {
        if (_source == null) return;
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null || !await page.DisplayAlert(
                "Delete Workflow", $"Delete '{_source.Name}'?", "Delete", "Cancel")) return;
        await _service.DeleteWorkflowAsync(_source);
        await _service.SetLastUsedWorkflowIdAsync(null);
        if (Deleted != null) await Deleted.Invoke();
    }

    private void Move(int from, int to)
    {
        var item = _steps[from];
        _steps.RemoveAt(from);
        _steps.Insert(to, item);
        BuildUI();
    }

    private static DevWorkflowStep Clone(DevWorkflowStep x) => new()
    {
        Id = x.Id, Username = x.Username, WorkflowId = x.WorkflowId,
        SortOrder = x.SortOrder, StepType = x.StepType, Text = x.Text,
        UseAllConstraints = x.UseAllConstraints, CheckKind = x.CheckKind,
        WrapPrefix = x.WrapPrefix, WrapSuffix = x.WrapSuffix,
        AppendConstraints = x.AppendConstraints
    };

    private static Frame Card(View content) => new() { Content = content, Padding = 10, CornerRadius = 9, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
    private static Button Button(string text, string color) => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(10, 0) };
    private static Button Small(string text, string color = "#607D8B") => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 10, CornerRadius = 5, HeightRequest = 30, Padding = new Thickness(7, 0) };
}
