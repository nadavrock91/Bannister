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
    private readonly HashSet<int> _selectedTaskIds = new();
    private Button? _doneSelectedButton;

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
        {
            if (tasks.TryGetValue(link.TaskItemId, out var task) &&
                !task.IsCompleted)
                body.Children.Add(BuildItemRow(group, link, task));
        }

        var completedLinks = links
            .Where(link => tasks.TryGetValue(
                link.TaskItemId, out var task) &&
                task.IsCompleted)
            .ToList();
        var completedSection = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = false
        };
        foreach (var link in completedLinks)
        {
            if (tasks.TryGetValue(link.TaskItemId, out var task))
                completedSection.Children.Add(
                    BuildItemRow(group, link, task));
        }
        var completedToggle = MakeButton(
            $"✓ Completed ({completedLinks.Count})",
            "#ECEFF1",
            "#616161");
        completedToggle.IsVisible = completedLinks.Count > 0;
        completedToggle.Clicked += (_, _) =>
            completedSection.IsVisible =
                !completedSection.IsVisible;
        body.Children.Add(completedToggle);
        body.Children.Add(completedSection);

        var exceptions = await _service.GetExceptionsAsync(group.Id);
        var exceptionPanel = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = false
        };
        var exceptionList = new VerticalStackLayout { Spacing = 4 };
        foreach (var exception in exceptions)
        {
            var exceptionRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            exceptionRow.Add(new Label
            {
                Text = exception.Label,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#333")
            }, 0, 0);
            var deleteException = MakeButton("Delete", "#FFEBEE", "#C62828");
            deleteException.Clicked += async (_, _) =>
            {
                await _service.DeleteExceptionAsync(exception.Id);
                await RefreshAsync();
            };
            exceptionRow.Add(deleteException, 1, 0);
            exceptionList.Children.Add(exceptionRow);
        }
        exceptionPanel.Children.Add(exceptionList);
        var exceptionInput = new Entry
        {
            Placeholder = "Exception type label",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        var addException = MakeButton("Add", "#E8EAF6", "#3949AB");
        addException.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(exceptionInput.Text)) return;
            await _service.SaveExceptionAsync(new SequenceTaskException
            {
                GroupId = group.Id,
                Label = exceptionInput.Text.Trim()
            });
            await RefreshAsync();
        };
        var exceptionAddRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        exceptionAddRow.Add(exceptionInput, 0, 0);
        exceptionAddRow.Add(addException, 1, 0);
        exceptionPanel.Children.Add(exceptionAddRow);
        var exceptionToggle = MakeButton("Exception Types", "#ECEFF1", "#37474F");
        exceptionToggle.Clicked += (_, _) =>
            exceptionPanel.IsVisible = !exceptionPanel.IsVisible;
        body.Children.Add(exceptionToggle);
        body.Children.Add(exceptionPanel);

        var exceptionButtons = new HorizontalStackLayout { Spacing = 6 };
        foreach (var exception in exceptions)
        {
            var exceptionButton = MakeButton(
                exception.Label, "#FFF3E0", "#E65100");
            exceptionButton.Clicked += async (_, _) =>
            {
                var incomplete = links
                    .Where(l => tasks.TryGetValue(l.TaskItemId, out var t) && !t.IsCompleted)
                    .Select(l => tasks[l.TaskItemId])
                    .ToList();
                var beforeTaskId =
                    await ShowInsertPositionOverlayAsync(incomplete);
                if (!beforeTaskId.HasValue) return;

                var newTask = await TaskCreationHelper.ShowCreateTaskAsync(
                    this, _auth, _tasks, _ideasService);
                if (newTask == null) return;

                await _service.InsertTaskItemAtAsync(
                    group.Id, newTask.Id, beforeTaskId.Value);
                await RefreshAsync();
            };
            exceptionButtons.Children.Add(exceptionButton);
        }
        if (exceptions.Count > 0)
            body.Children.Add(exceptionButtons);

        var add = MakeButton("+ Add Task", "#E8F5E9", "#2E7D32");
        add.Clicked += async (_, _) =>
        {
            var task = await TaskCreationHelper.ShowCreateTaskAsync(this, _auth, _tasks, _ideasService);
            if (task != null) { await _service.AddTaskItemAsync(group.Id, task.Id); await RefreshAsync(); }
        };
        var selectionActions = new HorizontalStackLayout { Spacing = 6 };
        var selectAll = MakeButton("Select All", "#E3F2FD", "#1565C0");
        selectAll.Clicked += async (_, _) =>
        {
            foreach (var link in links)
                if (tasks.TryGetValue(link.TaskItemId, out var task) && !task.IsCompleted)
                    _selectedTaskIds.Add(task.Id);
            await RefreshAsync();
        };
        _doneSelectedButton = MakeButton($"Done Selected ({_selectedTaskIds.Count})", "#E8F5E9", "#2E7D32");
        _doneSelectedButton.IsEnabled = _selectedTaskIds.Count > 0;
        _doneSelectedButton.Clicked += async (_, _) => await CompleteSelectedAsync(group, links, tasks);
        selectionActions.Children.Add(selectAll);
        selectionActions.Children.Add(_doneSelectedButton);
        body.Children.Add(selectionActions);
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
        var deleteGroup = MakeButton("Delete Group", "#FFEBEE", "#B71C1C");
        deleteGroup.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert(
                "Delete Group",
                "Delete this group? This will remove the group and all its task links. Tasks themselves will not be deleted.",
                "Delete", "Cancel");
            if (!confirm) return;
            await _service.DeleteGroupAsync(group.Id);
            _openGroup = null;
            await RefreshAsync();
        };
        body.Children.Add(deleteGroup);
        return Card(body);
    }

    private async Task<int?> ShowInsertPositionOverlayAsync(
        List<TaskItem> incompleteTasks)
    {
        var tcs = new TaskCompletionSource<int?>();
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000")
        };

        var title = new Label
        {
            Text = "Insert before which task?",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            HorizontalOptions = LayoutOptions.Center
        };

        Grid rootGrid = null!;
        View? originalContent = null;
        var list = new VerticalStackLayout { Spacing = 8 };
        var topButton = MakeButton(
            "⬆ Insert at Top", "#E3F2FD", "#1565C0");
        topButton.Clicked += (_, _) => Close(0);
        list.Children.Add(topButton);

        foreach (var task in incompleteTasks)
        {
            var capturedTask = task;
            var taskButton = MakeButton(
                capturedTask.Title, "#F5F5F5", "#222222");
            taskButton.HorizontalOptions = LayoutOptions.Fill;
            taskButton.LineBreakMode = LineBreakMode.WordWrap;
            taskButton.MinimumHeightRequest = 44;
            taskButton.Clicked += (_, _) =>
                Close(capturedTask.Id);
            list.Children.Add(taskButton);
        }

        var cancelButton = MakeButton(
            "Cancel", "#ECEFF1", "#616161");
        cancelButton.Clicked += (_, _) => Close(null);

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 16,
            WidthRequest = 360,
            MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    title,
                    new ScrollView
                    {
                        MaximumHeightRequest = 440,
                        Content = list
                    },
                    cancelButton
                }
            }
        };

        if (Content is Grid existingGrid)
        {
            rootGrid = existingGrid;
        }
        else
        {
            originalContent = Content;
            rootGrid = new Grid();
            if (originalContent != null)
            {
                Content = null;
                rootGrid.Children.Add(originalContent);
            }
            Content = rootGrid;
        }

        void Close(int? result)
        {
            rootGrid.Children.Remove(overlay);
            tcs.TrySetResult(result);
        }

        overlay.Children.Add(card);
        rootGrid.Children.Add(overlay);

        var selected = await tcs.Task;
        if (originalContent != null &&
            ReferenceEquals(Content, rootGrid))
        {
            rootGrid.Children.Remove(originalContent);
            Content = null;
            Content = originalContent;
        }

        return selected;
    }

    private async Task CompleteSelectedAsync(SequenceTaskGroup group, List<SequenceTaskItem> links, Dictionary<int, TaskItem> tasks)
    {
        var selected = links
            .Where(link => _selectedTaskIds.Contains(link.TaskItemId) && tasks.TryGetValue(link.TaskItemId, out var task) && !task.IsCompleted)
            .Select(link => tasks[link.TaskItemId])
            .ToList();
        if (selected.Count == 0) return;
        var numbered = string.Join("\n", selected.Select((task, index) => $"{index + 1}. {task.Title}"));
        var prompt = (group.ExportPromptTemplate ?? "{task}").Replace("{task}", numbered);
        await Clipboard.SetTextAsync(prompt);
        _busy = true;
        try
        {
            foreach (var task in selected)
                await _tasks.CompleteTaskAsync(task);
        }
        finally { _busy = false; }
        _selectedTaskIds.Clear();
        await DisplayAlert("Prompt Copied", $"Prompt copied for {selected.Count} tasks — paste into LLM before continuing", "OK");
        await RefreshAsync();
    }

    private View BuildItemRow(SequenceTaskGroup group, SequenceTaskItem link, TaskItem task)
    {
        var body = new VerticalStackLayout { Spacing = 4 };
        if (!task.IsCompleted)
        {
            var selectRow = new HorizontalStackLayout { Spacing = 6 };
            var check = new CheckBox { IsChecked = _selectedTaskIds.Contains(task.Id), VerticalOptions = LayoutOptions.Center };
            check.CheckedChanged += (_, e) =>
            {
                if (e.Value) _selectedTaskIds.Add(task.Id); else _selectedTaskIds.Remove(task.Id);
                if (_doneSelectedButton != null)
                {
                    _doneSelectedButton.Text = $"Done Selected ({_selectedTaskIds.Count})";
                    _doneSelectedButton.IsEnabled = _selectedTaskIds.Count > 0;
                }
            };
            selectRow.Children.Add(check);
            selectRow.Children.Add(new Label { Text = "Select this task", VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#555") });
            body.Children.Add(selectRow);
        }
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
