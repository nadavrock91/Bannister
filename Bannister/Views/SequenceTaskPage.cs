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
    private readonly PrivacyModeService _privacyMode;
    private readonly TaskService _tasks;
    private readonly IdeasService? _ideasService;
    private readonly VerticalStackLayout _content;
    private SequenceTaskGroup? _openGroup;
    private Entry _nameEntry = null!;
    private Editor _templateEditor = null!;
    private Editor _tasksEditor = null!;
    private Entry _capEntry = null!;
    private bool _busy;
    private readonly HashSet<int> _selectedTaskIds = new();
    private Button? _doneSelectedButton;
    private Grid _root = null!;

    public SequenceTaskPage(SequenceTaskService service, AuthService auth,
        TaskService tasks, IdeasService? ideasService)
    {
        _service = service; _auth = auth; _tasks = tasks; _ideasService = ideasService;
        _privacyMode = Application.Current?.Handler?.MauiContext?.Services
            .GetService<PrivacyModeService>()
            ?? throw new InvalidOperationException("PrivacyModeService not available");
        Title = "Sequence Tasks"; BackgroundColor = Color.FromArgb("#F5F5F5");
        _content = new VerticalStackLayout { Padding = new Thickness(16, 16, 16, 32), Spacing = 12 };
        _root = new Grid();
        _root.Children.Add(new ScrollView { Content = _content });
        Content = _root;
    }

    protected override async void OnAppearing()
    { base.OnAppearing(); await RefreshAsync(); }

    private bool IsPublicMode() =>
        _privacyMode.GetDisplayMode(_auth.CurrentUsername)
            == ActivityDisplayMode.PublicOnly;

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

    private async Task<TaskItem?> ChooseExistingTaskAsync(List<TaskItem> allTasks)
    {
        if (allTasks.Count == 0)
        {
            await DisplayAlert("No Tasks", "No active tasks found.", "OK");
            return null;
        }

        var completion = new TaskCompletionSource<TaskItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Margin = new Thickness(-20),
            ZIndex = 100
        };
        Grid.SetRow(overlay, 0);
        Grid.SetRowSpan(overlay, 4);

        void Complete(TaskItem? selected)
        {
            _root.Children.Remove(overlay);
            completion.TrySetResult(selected);
        }

        var filtered = allTasks.ToList();
        filtered = filtered.OrderByDescending(t => t.CreatedAt).ToList();

        var items = new VerticalStackLayout
        {
            Padding = new Thickness(12, 8),
            Spacing = 6
        };

        // Collect distinct categories in the order they first appear after sorting
        var allCategories = filtered
            .Select(t => t.Category ?? "")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        string selectedCategory = "";  // "" means All

        var searchEntry = new Entry
        {
            Placeholder = "Search tasks…",
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            Margin = new Thickness(0, 0, 0, 8)
        };

        void RebuildButtons(string query)
        {
            items.Children.Clear();
            var matches = filtered
                .Where(t =>
                    (string.IsNullOrWhiteSpace(selectedCategory) ||
                     string.Equals(t.Category ?? "", selectedCategory, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(query) ||
                     t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     (t.Category ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count == 0)
            {
                items.Children.Add(new Label
                {
                    Text = "No results",
                    TextColor = Color.FromArgb("#999"),
                    FontSize = 13,
                    Margin = new Thickness(4, 8)
                });
                return;
            }

            foreach (var task in matches)
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(80) }
                    },
                    Margin = new Thickness(0, 2)
                };

                var btn = new Button
                {
                    Text = $"{task.Title}  [{task.Category}]",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#222"),
                    BackgroundColor = Color.FromArgb("#F5F5F5"),
                    CornerRadius = 7,
                    Padding = new Thickness(12, 8),
                    HorizontalOptions = LayoutOptions.Fill,
                    LineBreakMode = LineBreakMode.WordWrap
                };
                btn.Clicked += (_, _) => Complete(task);

                var dateLabel = new Label
                {
                    Text = task.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#999"),
                    VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(6, 0, 4, 0)
                };

                Grid.SetColumn(btn, 0);
                Grid.SetColumn(dateLabel, 1);
                row.Children.Add(btn);
                row.Children.Add(dateLabel);
                items.Children.Add(row);
            }
        }

        var categoryPicker = new Picker
        {
            Title = "All Categories",
            FontSize = 13,
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            Margin = new Thickness(0, 0, 0, 6)
        };
        categoryPicker.Items.Add("All");
        foreach (var cat in allCategories)
            categoryPicker.Items.Add(cat);
        categoryPicker.SelectedIndex = 0;
        categoryPicker.SelectedIndexChanged += (_, _) =>
        {
            selectedCategory = categoryPicker.SelectedIndex <= 0 ? "" : allCategories[categoryPicker.SelectedIndex - 1];
            RebuildButtons(searchEntry.Text ?? "");
        };

        searchEntry.TextChanged += (_, e) => RebuildButtons(e.NewTextValue ?? "");

        RebuildButtons("");

        var cancel = new Button
        {
            Text = "Cancel",
            FontSize = 14,
            TextColor = Color.FromArgb("#555"),
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            CornerRadius = 7,
            Padding = new Thickness(12, 8),
            HorizontalOptions = LayoutOptions.Fill
        };
        cancel.Clicked += (_, _) => Complete(null);

        var cardContent = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Add Existing Task",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                categoryPicker,
                searchEntry,
                new ScrollView
                {
                    MaximumHeightRequest = 340,
                    Content = items
                },
                cancel
            }
        };

        overlay.Children.Add(new Frame
        {
            Content = cardContent,
            Padding = 18,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            MaximumWidthRequest = 520,
            MaximumHeightRequest = 600,
            Margin = new Thickness(20),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        });

        _root.Children.Add(overlay);
        return await completion.Task;
    }

    private async Task<View> BuildOpenGroupAsync(SequenceTaskGroup group)
    {
        var links = await _service.GetLinksAsync(group.Id);
        var tasks = await LoadLinkedTasksAsync(links);
        async Task<bool> IsCapReachedAsync(TaskItem? task = null)
        {
            if (group.TaskCap <= 0) return false;
            var currentLinks = await _service.GetLinksAsync(group.Id);
            var allActive = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
            var incompleteCount = currentLinks.Count(l =>
                allActive.Any(t => t.Id == l.TaskItemId && !t.IsCompleted));
            if (incompleteCount < group.TaskCap) return false;

            if (task != null)
            {
                bool addToTopCandidates = await DisplayAlert(
                    "Cap Reached",
                    $"This group has a cap of {group.TaskCap} incomplete tasks.\n\nAdd \"{task.Title}\" to Top Candidates instead so you can add it later?",
                    "Add to Top Candidates", "Cancel");
                if (addToTopCandidates)
                {
                    await _service.AddCandidateAsync(group.Id, task.Id);
                    await DisplayAlert("Added to Top Candidates", $"\"{task.Title}\" will appear in the Top Candidates section below.", "OK");
                }
            }
            else
            {
                await DisplayAlert("Cap Reached",
                    $"This group has a cap of {group.TaskCap} incomplete tasks. Complete or remove a task to make room.",
                    "OK");
            }
            return true;
        }
        var body = new VerticalStackLayout { Spacing = 10 };
        var headingRow = new HorizontalStackLayout { Spacing = 8 };
        headingRow.Children.Add(new Label { Text = group.Name, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center });
        var editTemplate = MakeButton("Edit Template", "#E8EAF6", "#3949AB");
        headingRow.Children.Add(editTemplate);
        var editCap = MakeButton(group.TaskCap > 0 ? $"Cap: {group.TaskCap}" : "Set Cap", "#FFF3E0", "#E65100");
        var capPanel = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        var capEditEntry = new Entry
        {
            Text = group.TaskCap > 0 ? group.TaskCap.ToString() : "",
            Placeholder = "Task cap (0 = no cap)",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            Keyboard = Keyboard.Numeric
        };
        var saveCap = MakeButton("Save Cap", "#E8F5E9", "#2E7D32");
        saveCap.Clicked += async (_, _) =>
        {
            int newCap = int.TryParse(capEditEntry.Text, out var c) ? c : 0;
            group.TaskCap = newCap;
            await _service.SaveGroupAsync(group);
            capPanel.IsVisible = false;
            await RefreshAsync();
        };
        capPanel.Children.Add(capEditEntry);
        capPanel.Children.Add(saveCap);
        editCap.Clicked += (_, _) => capPanel.IsVisible = !capPanel.IsVisible;
        headingRow.Children.Add(editCap);
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
        body.Children.Add(capPanel);

        body.Children.Add(new Label { Text = $"{tasks.Values.Count(t => t.IsCompleted)} of {links.Count} completed", FontSize = 13, TextColor = Color.FromArgb("#666") });
        var selectionActions = new HorizontalStackLayout { Spacing = 6 };
        var selectAll = MakeButton("Select All", "#E3F2FD", "#1565C0");
        selectAll.Clicked += async (_, _) =>
        {
            foreach (var link in links)
                if (tasks.TryGetValue(link.TaskItemId, out var task) && !task.IsCompleted && !link.IsPendingTest)
                    _selectedTaskIds.Add(task.Id);
            await RefreshAsync();
        };
        _doneSelectedButton = MakeButton($"Done Selected ({_selectedTaskIds.Count})", "#E8F5E9", "#2E7D32");
        _doneSelectedButton.IsEnabled = _selectedTaskIds.Count > 0;
        _doneSelectedButton.Clicked += async (_, _) => await CompleteSelectedAsync(group, links, tasks);
        selectionActions.Children.Add(selectAll);
        selectionActions.Children.Add(_doneSelectedButton);
        body.Children.Add(selectionActions);
        foreach (var link in links)
        {
            if (tasks.TryGetValue(link.TaskItemId, out var task) &&
                !task.IsCompleted && !link.IsPendingTest)
                body.Children.Add(BuildItemRow(group, link, task));
        }

        var pendingLinks = links.Where(link => link.IsPendingTest &&
            tasks.TryGetValue(link.TaskItemId, out var task) && !task.IsCompleted).ToList();
        var pendingSection = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        foreach (var link in pendingLinks)
            if (tasks.TryGetValue(link.TaskItemId, out var task))
                pendingSection.Children.Add(BuildItemRow(group, link, task, true));
        var pendingToggle = MakeButton($"⏳ Pending Test ({pendingLinks.Count})", "#FFF3E0", "#E65100");
        pendingToggle.IsVisible = pendingLinks.Count > 0;
        pendingToggle.Clicked += (_, _) => pendingSection.IsVisible = !pendingSection.IsVisible;
        body.Children.Add(pendingToggle);
        body.Children.Add(pendingSection);

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
                    .Where(l => tasks.TryGetValue(l.TaskItemId, out var t) && !t.IsCompleted && !l.IsPendingTest)
                    .Select(l => tasks[l.TaskItemId])
                    .ToList();
                var beforeTaskId = await ShowInsertPositionOverlayAsync(incomplete);
                if (!beforeTaskId.HasValue) return;

                bool fromExisting = await DisplayAlert("Add Task", "Add a new task or pick from existing?", "From Existing", "New Task");
                if (fromExisting)
                {
                    var allTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
                    var existingTask = await ChooseExistingTaskAsync(allTasks);
                    if (existingTask == null) return;
                    var duplicate = (await _service.GetLinksAsync(group.Id))
                        .Count(i => i.TaskItemId == existingTask.Id);
                    if (duplicate > 0)
                    {
                        await DisplayAlert("Already Added", $"\"{existingTask.Title}\" is already in this group.", "OK");
                        return;
                    }
                    await _service.InsertTaskItemAtAsync(group.Id, existingTask.Id, beforeTaskId.Value);
                }
                else
                {
                    var newTask = await TaskCreationHelper.ShowCreateTaskAsync(
                        this, _auth, _tasks, _ideasService);
                    if (newTask == null) return;
                    await _service.InsertTaskItemAtAsync(group.Id, newTask.Id, beforeTaskId.Value);
                }
                await RefreshAsync();
            };
            exceptionButtons.Children.Add(exceptionButton);
        }
        if (exceptions.Count > 0)
            body.Children.Add(exceptionButtons);

        var add = MakeButton("+ Add Task", "#E8F5E9", "#2E7D32");
        add.Clicked += async (_, _) =>
        {
            bool fromExisting = await DisplayAlert("Add Task", "Add a new task or pick from existing?", "From Existing", "New Task");
            if (fromExisting)
            {
                var allTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
                var existingTask = await ChooseExistingTaskAsync(allTasks);
                if (existingTask == null) return;
                var duplicate = (await _service.GetLinksAsync(group.Id))
                    .Count(i => i.TaskItemId == existingTask.Id);
                if (duplicate > 0)
                {
                    await DisplayAlert("Already Added", $"\"{existingTask.Title}\" is already in this group.", "OK");
                    return;
                }
                if (await IsCapReachedAsync(existingTask)) return;
                bool insertMode = await DisplayAlert("Add Task", "Where would you like to add this task?", "Insert at position", "Add to end");
                if (insertMode)
                {
                    var currentLinks = await _service.GetLinksAsync(group.Id);
                    var currentTaskIds = currentLinks.Select(l => l.TaskItemId).ToList();
                    var allActiveTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
                    var incompleteTasks = currentTaskIds
                        .Select(id => allActiveTasks.FirstOrDefault(t => t.Id == id))
                        .Where(t => t != null)
                        .Cast<TaskItem>()
                        .ToList();
                    var insertBeforeTaskId = await ShowInsertPositionOverlayAsync(incompleteTasks);
                    if (insertBeforeTaskId == null) return;
                    if (insertBeforeTaskId == 0)
                        await _service.InsertTaskItemAtAsync(group.Id, existingTask.Id, 0);
                    else
                        await _service.InsertTaskItemBeforeAsync(group.Id, existingTask.Id, insertBeforeTaskId.Value);
                }
                else
                {
                    await _service.AddTaskItemAsync(group.Id, existingTask.Id);
                }
                await RefreshAsync();
            }
            else
            {
                var task = await TaskCreationHelper.ShowCreateTaskAsync(this, _auth, _tasks, _ideasService);
                if (task != null)
                {
                    if (await IsCapReachedAsync(task)) return;
                    await _service.AddTaskItemAsync(group.Id, task.Id);
                    await RefreshAsync();
                }
            }
        };

        var addExisting = MakeButton("+ From Existing", "#E3F2FD", "#1565C0");
        addExisting.Clicked += async (_, _) =>
        {
            var allTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
            var existingTask = await ChooseExistingTaskAsync(allTasks);
            if (existingTask == null) return;
            var duplicate = (await _service.GetLinksAsync(group.Id))
                .Count(i => i.TaskItemId == existingTask.Id);
            if (duplicate > 0)
            {
                await DisplayAlert("Already Added", $"\"{existingTask.Title}\" is already in this group.", "OK");
                return;
            }
            if (await IsCapReachedAsync(existingTask)) return;
            bool insertMode = await DisplayAlert("Add Task", "Where would you like to add this task?", "Insert at position", "Add to end");
            if (insertMode)
            {
                var currentLinks = await _service.GetLinksAsync(group.Id);
                var currentTaskIds = currentLinks.Select(l => l.TaskItemId).ToList();
                var allActiveTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
                var incompleteTasks = currentTaskIds
                    .Select(id => allActiveTasks.FirstOrDefault(t => t.Id == id))
                    .Where(t => t != null)
                    .Cast<TaskItem>()
                    .ToList();
                var insertBeforeTaskId = await ShowInsertPositionOverlayAsync(incompleteTasks);
                if (insertBeforeTaskId == null) return;
                if (insertBeforeTaskId == 0)
                    await _service.InsertTaskItemAtAsync(group.Id, existingTask.Id, 0);
                else
                    await _service.InsertTaskItemBeforeAsync(group.Id, existingTask.Id, insertBeforeTaskId.Value);
            }
            else
            {
                await _service.AddTaskItemAsync(group.Id, existingTask.Id);
            }
            await RefreshAsync();
        };

        var addRow = new HorizontalStackLayout { Spacing = 8 };
        addRow.Children.Add(add);
        addRow.Children.Add(addExisting);
        body.Children.Add(addRow);

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

        var candidates = await _service.GetCandidatesAsync(group.Id);
        var allTasksForCandidates = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
        var groupLinkedIds2 = links.Select(l => l.TaskItemId).ToHashSet();
        var candidateTasks = candidates
            .Select(c => allTasksForCandidates.FirstOrDefault(t => t.Id == c.TaskItemId))
            .Where(t => t != null && !groupLinkedIds2.Contains(t.Id))
            .Cast<TaskItem>()
            .ToList();

        if (candidateTasks.Count > 0)
        {
            bool tcExpanded = false;
            var tcBody = new VerticalStackLayout { Spacing = 6, IsVisible = false };
            var tcHeaderLabel = new Label
            {
                Text = $"▶ ⭐ Top Candidates ({candidateTasks.Count})",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF9800"),
                Margin = new Thickness(0, 8, 0, 2)
            };
            var tcHeaderTap = new TapGestureRecognizer();
            tcHeaderTap.Tapped += (_, _) =>
            {
                tcExpanded = !tcExpanded;
                tcBody.IsVisible = tcExpanded;
                tcHeaderLabel.Text = tcExpanded
                    ? $"▼ ⭐ Top Candidates ({candidateTasks.Count})"
                    : $"▶ ⭐ Top Candidates ({candidateTasks.Count})";
            };
            tcHeaderLabel.GestureRecognizers.Add(tcHeaderTap);

            foreach (var candidate in candidateTasks)
            {
                var capturedCandidate = candidate;
                var tcRow = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    Margin = new Thickness(0, 2)
                };
                tcRow.Children.Add(new Label
                {
                    Text = $"{capturedCandidate.Title}  [{capturedCandidate.Category}]",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#444"),
                    VerticalOptions = LayoutOptions.Center
                });
                var addBtn = MakeButton("+ Add", "#E8F5E9", "#2E7D32");
                addBtn.Clicked += async (_, _) =>
                {
                    var duplicate = (await _service.GetLinksAsync(group.Id))
                        .Count(i => i.TaskItemId == capturedCandidate.Id);
                    if (duplicate > 0)
                    {
                        await DisplayAlert("Already Added", $"\"{capturedCandidate.Title}\" is already in this group.", "OK");
                        return;
                    }
                    if (await IsCapReachedAsync(capturedCandidate)) return;
                    bool insertMode = await DisplayAlert("Add Task", "Where would you like to add this task?", "Insert at position", "Add to end");
                    if (insertMode)
                    {
                        var currentLinks3 = await _service.GetLinksAsync(group.Id);
                        var currentTaskIds3 = currentLinks3.Select(l => l.TaskItemId).ToList();
                        var allActiveTasks3 = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
                        var incompleteTasks3 = currentTaskIds3
                            .Select(id => allActiveTasks3.FirstOrDefault(t => t.Id == id))
                            .Where(t => t != null)
                            .Cast<TaskItem>()
                            .ToList();
                        var insertBeforeTaskId3 = await ShowInsertPositionOverlayAsync(incompleteTasks3);
                        if (insertBeforeTaskId3 == null) return;
                        if (insertBeforeTaskId3 == 0)
                            await _service.InsertTaskItemAtAsync(group.Id, capturedCandidate.Id, 0);
                        else
                            await _service.InsertTaskItemBeforeAsync(group.Id, capturedCandidate.Id, insertBeforeTaskId3.Value);
                    }
                    else
                    {
                        await _service.AddTaskItemAsync(group.Id, capturedCandidate.Id);
                    }
                    await _service.RemoveCandidateAsync(group.Id, capturedCandidate.Id);
                    await RefreshAsync();
                };
                var removeBtn = MakeButton("✕", "#FFEBEE", "#C62828");
                removeBtn.Clicked += async (_, _) =>
                {
                    await _service.RemoveCandidateAsync(group.Id, capturedCandidate.Id);
                    await RefreshAsync();
                };
                Grid.SetColumn(addBtn, 1);
                Grid.SetColumn(removeBtn, 2);
                tcRow.Children.Add(addBtn);
                tcRow.Children.Add(removeBtn);
                tcBody.Children.Add(tcRow);
            }

            body.Children.Add(tcHeaderLabel);
            body.Children.Add(tcBody);
        }
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
            var publicMode = IsPublicMode();
            var buttonLabel = (publicMode && capturedTask.IsPrivate)
                ? (string.IsNullOrWhiteSpace(capturedTask.Impact) ? "(private)" : capturedTask.Impact)
                : capturedTask.Title;
            var taskButton = MakeButton(
                buttonLabel, "#F5F5F5", "#222222");
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
            .Where(link => _selectedTaskIds.Contains(link.TaskItemId) && !link.IsPendingTest && tasks.TryGetValue(link.TaskItemId, out var task) && !task.IsCompleted)
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

    private async Task InsertTaskBeforeAsync(SequenceTaskGroup group, int beforeTaskItemId)
    {
        var newTask = await TaskCreationHelper.ShowCreateTaskAsync(
            this, _auth, _tasks, _ideasService);
        if (newTask == null) return;
        await _service.InsertTaskItemAtAsync(
            group.Id, newTask.Id, beforeTaskItemId);
        await RefreshAsync();
    }

    private View BuildItemRow(SequenceTaskGroup group, SequenceTaskItem link, TaskItem task, bool pendingTest = false)
    {
        var body = new VerticalStackLayout { Spacing = 4 };
        if (!task.IsCompleted && !pendingTest)
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
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(8, 6) };
        var publicMode = IsPublicMode();
        var displayText = (publicMode && task.IsPrivate)
            ? (string.IsNullOrWhiteSpace(task.Impact) ? "(private)" : task.Impact)
            : task.Title;
        row.Add(new Label { Text = task.IsCompleted ? $"✓ {displayText}" : displayText, FontSize = 14, TextColor = task.IsCompleted ? Color.FromArgb("#8A8A8A") : Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var copy = MakeButton("Copy", "#E3F2FD", "#1565C0");
        copy.Clicked += async (_, _) =>
        {
            var publicMode = IsPublicMode();
            var copyText = (publicMode && task.IsPrivate)
                ? (string.IsNullOrWhiteSpace(task.Impact) ? "(private)" : task.Impact)
                : task.Title;
            await Clipboard.SetTextAsync(copyText);
        };
        row.Add(copy, 1, 0);
        if (!task.IsCompleted && pendingTest)
        {
            var moveBack = MakeButton("↩ Move Back", "#ECEFF1", "#37474F");
            moveBack.Clicked += async (_, _) => { await _service.UpdateItemPendingTestAsync(link.Id, false); await RefreshAsync(); };
            row.Add(moveBack, 2, 0);
            var pendingDone = MakeButton("✓ Done", "#E8F5E9", "#2E7D32");
            pendingDone.Clicked += async (_, _) => { await _tasks.CompleteTaskAsync(task); await RefreshAsync(); };
            row.Add(pendingDone, 3, 0);
        }
        else if (!task.IsCompleted)
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
            var pending = MakeButton("Done (Pending Test)", "#FFF3E0", "#E65100");
            pending.Clicked += async (_, _) =>
            {
                var prompt = (group.ExportPromptTemplate ?? "{task}").Replace("{task}", task.Title);
                await Clipboard.SetTextAsync(prompt);
                await DisplayAlert("Prompt Copied", "✓ Prompt copied to clipboard — paste into your LLM before continuing to the next task.", "OK");
                await _service.UpdateItemPendingTestAsync(link.Id, true);
                await RefreshAsync();
            };
            row.Add(pending, 3, 0);
            var exception = MakeButton("＋", "#FFF3E0", "#E65100");
            exception.Clicked += async (_, _) => await InsertTaskBeforeAsync(group, task.Id);
            row.Add(exception, 4, 0);
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
        row.Add(edit, pendingTest ? 4 : 5, 0);
        var delete = MakeButton("Delete", "#FFEBEE", "#C62828");
        delete.Clicked += async (_, _) => { await _service.DeleteItemAsync(link.Id); await RefreshAsync(); };
        row.Add(delete, pendingTest ? 5 : 6, 0);
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
        _capEntry = new Entry { Placeholder = "Task cap (0 = no cap)", BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222"), Keyboard = Keyboard.Numeric };
        form.Children.Add(_nameEntry);
        form.Children.Add(_capEntry);
        form.Children.Add(_templateEditor);
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
            var group = new SequenceTaskGroup { Name = _nameEntry.Text.Trim(), ExportPromptTemplate = _templateEditor.Text.Trim(), CreatedDate = DateTime.Now, TaskCap = int.TryParse(_capEntry.Text, out var cap) ? cap : 0 };
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
