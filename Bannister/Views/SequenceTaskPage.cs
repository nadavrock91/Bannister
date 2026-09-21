using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SequenceTaskPage : ContentPage
{
    private const string TutorialTemplate =
        "I just completed the following development task:\n\n{task}\n\n" +
        "Please evaluate whether this warrants creating a video tutorial. " +
        "Consider: is there a substantial new feature a user would need guidance on? " +
        "Return your verdict and reasoning.";
    private readonly SequenceTaskService _service;
    private readonly AuthService _auth;
    private readonly TaskService _tasks;
    private readonly IdeasService? _ideasService;
    private readonly VerticalStackLayout _content;
    private SequenceTaskGroup? _openGroup;
    private Entry _nameEntry = null!;
    private Editor _templateEditor = null!;
    private Editor _tasksEditor = null!;
    private bool _busy;

    public SequenceTaskPage(SequenceTaskService service, AuthService auth,
        TaskService tasks, IdeasService? ideasService)
    {
        _service = service; _auth = auth; _tasks = tasks; _ideasService = ideasService;
        Title = "Sequence Tasks"; BackgroundColor = Color.FromArgb("#F5F5F5");
        _content = new VerticalStackLayout { Padding = new Thickness(16, 16, 16, 32), Spacing = 12 };
        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    { base.OnAppearing(); await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        await _service.InitAsync();
        _content.Children.Clear();
        _content.Children.Add(new Label { Text = "Sequence Tasks", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        if (_openGroup != null)
        {
            var current = (await _service.GetAllGroupsAsync()).FirstOrDefault(g => g.Id == _openGroup.Id && !g.IsArchived);
            if (current != null) { _openGroup = current; _content.Children.Add(await BuildOpenGroupAsync(current)); return; }
            _openGroup = null;
        }
        _content.Children.Add(new Label { Text = "Active Groups", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var group in await _service.GetActiveGroupsAsync()) _content.Children.Add(await BuildGroupCardAsync(group));
        _content.Children.Add(BuildCreateForm());
    }

    private async Task<Dictionary<int, TaskItem>> LoadLinkedTasksAsync(IEnumerable<SequenceTaskItem> links)
    {
        var active = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
        var completed = await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername);
        var wanted = links.Select(l => l.TaskItemId).ToHashSet();
        return active.Concat(completed).Where(t => wanted.Contains(t.Id)).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<View> BuildGroupCardAsync(SequenceTaskGroup group)
    {
        var links = await _service.GetLinksAsync(group.Id);
        var tasks = await LoadLinkedTasksAsync(links);
        var body = new VerticalStackLayout { Spacing = 6 };
        body.Children.Add(new Label { Text = group.Name, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        body.Children.Add(new Label { Text = $"{links.Count} task{(links.Count == 1 ? "" : "s")} · {tasks.Values.Count(t => t.IsCompleted)} completed", FontSize = 12, TextColor = Color.FromArgb("#777") });
        var open = MakeButton("Open", "#E3F2FD", "#1565C0");
        open.Clicked += async (_, _) => { _openGroup = group; await RefreshAsync(); };
        body.Children.Add(open);
        var card = Card(body);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => { _openGroup = group; await RefreshAsync(); };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task<View> BuildOpenGroupAsync(SequenceTaskGroup group)
    {
        var links = await _service.GetLinksAsync(group.Id);
        var tasks = await LoadLinkedTasksAsync(links);
        var body = new VerticalStackLayout { Spacing = 10 };
        var headingRow = new HorizontalStackLayout { Spacing = 8 };
        headingRow.Children.Add(new Label { Text = group.Name, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center });
        var editTemplate = MakeButton("Edit Template", "#E8EAF6", "#3949AB");
        headingRow.Children.Add(editTemplate);
        body.Children.Add(headingRow);
        var templatePanel = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        var templateEditor = new Editor { Text = group.ExportPromptTemplate, HeightRequest = 140, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        var saveTemplate = MakeButton("Save Template", "#E8F5E9", "#2E7D32");
        saveTemplate.Clicked += async (_, _) =>
        {
            await _service.UpdateGroupTemplateAsync(group.Id, templateEditor.Text ?? "");
            templatePanel.IsVisible = false;
            await RefreshAsync();
        };
        templatePanel.Children.Add(templateEditor);
        templatePanel.Children.Add(saveTemplate);
        editTemplate.Clicked += (_, _) => templatePanel.IsVisible = !templatePanel.IsVisible;
        body.Children.Add(templatePanel);
        body.Children.Add(new Label { Text = $"{tasks.Values.Count(t => t.IsCompleted)} of {links.Count} completed", FontSize = 13, TextColor = Color.FromArgb("#666") });
        foreach (var link in links)
            if (tasks.TryGetValue(link.TaskItemId, out var task)) body.Children.Add(BuildItemRow(group, link, task));

        var add = MakeButton("+ Add Task", "#E8F5E9", "#2E7D32");
        add.Clicked += async (_, _) =>
        {
            var task = await TaskCreationHelper.ShowCreateTaskAsync(this, _auth, _tasks, _ideasService);
            if (task != null) { await _service.AddTaskItemAsync(group.Id, task.Id); await RefreshAsync(); }
        };
        body.Children.Add(add);

        var export = MakeButton("Export & Archive", "#FFF3E0", "#E65100");
        export.Clicked += async (_, _) =>
        {
            var taskLines = links.Where(l => tasks.ContainsKey(l.TaskItemId)).Select((l, i) => $"{i + 1}. {tasks[l.TaskItemId].Title}");
            var prompt = (group.ExportPromptTemplate ?? "{tasks}").Replace("{tasks}", string.Join("\n", taskLines));
            await Clipboard.SetTextAsync(prompt);
            await _service.ArchiveGroupAsync(group.Id); _openGroup = null;
            await DisplayAlert("Exported", "Prompt copied and group archived.", "OK"); await RefreshAsync();
        };
        body.Children.Add(export);
        var close = MakeButton("Close Group", "#ECEFF1", "#37474F");
        close.Clicked += async (_, _) => { _openGroup = null; await RefreshAsync(); };
        body.Children.Add(close);
        var archive = MakeButton("Archive Group", "#FFEBEE", "#C62828");
        archive.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Archive Group", $"Archive \"{group.Name}\"?", "Archive", "Cancel")) return;
            await _service.ArchiveGroupAsync(group.Id); _openGroup = null; await RefreshAsync();
        };
        body.Children.Add(archive);
        return Card(body);
    }

    private View BuildItemRow(SequenceTaskGroup group, SequenceTaskItem link, TaskItem task)
    {
        var body = new VerticalStackLayout { Spacing = 4 };
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(8, 6) };
        row.Add(new Label { Text = task.IsCompleted ? $"✓ {task.Title}" : task.Title, FontSize = 14, TextColor = task.IsCompleted ? Color.FromArgb("#8A8A8A") : Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var copy = MakeButton("Copy", "#E3F2FD", "#1565C0");
        copy.Clicked += async (_, _) => await Clipboard.SetTextAsync(task.Title);
        row.Add(copy, 1, 0);
        if (!task.IsCompleted)
        {
            var done = MakeButton("Done", "#E8F5E9", "#2E7D32");
            done.Clicked += async (_, _) =>
            {
                if (_busy) return; _busy = true;
                try
                {
                    await _tasks.CompleteTaskAsync(task);
                    var prompt = (group.ExportPromptTemplate ?? "").Replace("{task}", task.Title);
                    await Clipboard.SetTextAsync(prompt);
                    await DisplayAlert("Prompt Copied", "✓ Prompt copied to clipboard — paste into your LLM before continuing to the next task.", "OK");
                }
                finally { _busy = false; }
                await RefreshAsync();
            };
            row.Add(done, 2, 0);
        }
        var edit = MakeButton("Edit", "#FFF3E0", "#E65100");
        edit.Clicked += async (_, _) =>
        {
            var newTitle = await DisplayPromptAsync("Edit Task", "Task title:", "Save", "Cancel", initialValue: task.Title);
            if (string.IsNullOrWhiteSpace(newTitle)) return;
            task.Title = newTitle.Trim();
            await _tasks.UpdateTaskAsync(task);
            await RefreshAsync();
        };
        row.Add(edit, 3, 0);
        var delete = MakeButton("Delete", "#FFEBEE", "#C62828");
        delete.Clicked += async (_, _) => { await _service.DeleteItemAsync(link.Id); await RefreshAsync(); };
        row.Add(delete, 4, 0);
        body.Children.Add(row);
        if (task.IsCompleted) body.Children.Add(new Label { Text = $"Completed {task.CompletedAt:dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#999"), Margin = new Thickness(8, -4, 0, 0) });
        return new Border { Content = body, Stroke = Color.FromArgb("#E0E0E0"), StrokeThickness = 1, BackgroundColor = task.IsCompleted ? Color.FromArgb("#F2F2F2") : Colors.White, Padding = 2 };
    }

    private View BuildCreateForm()
    {
        var form = new VerticalStackLayout { Spacing = 8 };
        form.Children.Add(new Label { Text = "Create New Group", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222"), Margin = new Thickness(0, 8, 0, 0) });
        _nameEntry = new Entry { Placeholder = "Group name", BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        _templateEditor = new Editor { Placeholder = "Wrap around each completed task. Use {task} where the task description should appear.", HeightRequest = 140, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        _tasksEditor = new Editor { Placeholder = "Paste tasks, one per line...", HeightRequest = 180, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        form.Children.Add(_nameEntry); form.Children.Add(_templateEditor);
        var tutorial = MakeButton("Use Tutorial Check Template", "#E8EAF6", "#3949AB");
        tutorial.Clicked += (_, _) => _templateEditor.Text = TutorialTemplate;
        form.Children.Add(tutorial); form.Children.Add(_tasksEditor);
        var save = MakeButton("Save & Start", "#E8F5E9", "#2E7D32"); save.Clicked += async (_, _) => await SaveGroupAsync(); form.Children.Add(save);
        return Card(form);
    }

    private async Task SaveGroupAsync()
    {
        if (_busy || string.IsNullOrWhiteSpace(_nameEntry.Text) || string.IsNullOrWhiteSpace(_templateEditor.Text)) return;
        _busy = true;
        try
        {
            var group = new SequenceTaskGroup { Name = _nameEntry.Text.Trim(), ExportPromptTemplate = _templateEditor.Text.Trim(), CreatedDate = DateTime.Now };
            await _service.SaveGroupAsync(group);
            var lines = (_tasksEditor.Text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);
            foreach (var line in lines)
            {
                var task = await _tasks.CreateTaskAsync(_auth.CurrentUsername, line);
                await _service.AddTaskItemAsync(group.Id, task.Id);
            }
            _openGroup = group;
        }
        finally { _busy = false; }
        await RefreshAsync();
    }

    private static Frame Card(View content) => new() { Content = content, Padding = 12, CornerRadius = 8, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Color.FromArgb("#FAFAFA") };
    private static Button MakeButton(string text, string background, string foreground) => new() { Text = text, BackgroundColor = Color.FromArgb(background), TextColor = Color.FromArgb(foreground), CornerRadius = 6, FontSize = 12, HeightRequest = 36, Padding = new Thickness(10, 0) };
}
