using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SequenceTaskPage : ContentPage
{
    private const string TutorialTemplate =
        "I just completed the following development task:\n\n" +
        "{task}\n\n" +
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

    public SequenceTaskPage(
        SequenceTaskService service,
        AuthService auth,
        TaskService tasks,
        IdeasService? ideasService)
    {
        _service = service;
        _auth = auth;
        _tasks = tasks;
        _ideasService = ideasService;
        Title = "Sequence Tasks";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        _content = new VerticalStackLayout
        {
            Padding = new Thickness(16, 16, 16, 32),
            Spacing = 12
        };
        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        await _service.InitAsync();
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "Sequence Tasks",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });

        if (_openGroup != null)
        {
            var current = (await _service.GetAllGroupsAsync())
                .FirstOrDefault(g => g.Id == _openGroup.Id && !g.IsArchived);
            if (current == null)
                _openGroup = null;
            else
            {
                _openGroup = current;
                _content.Children.Add(await BuildOpenGroupAsync(current));
                return;
            }
        }

        var groups = await _service.GetActiveGroupsAsync();
        _content.Children.Add(new Label
        {
            Text = "Active Groups",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        foreach (var group in groups)
            _content.Children.Add(await BuildGroupCardAsync(group));

        _content.Children.Add(BuildCreateForm());
    }

    private async Task<View> BuildGroupCardAsync(SequenceTaskGroup group)
    {
        var items = await _service.GetItemsAsync(group.Id);
        var completed = items.Count(i => i.IsCompleted);
        var body = new VerticalStackLayout { Spacing = 6 };
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        row.Add(new Label
        {
            Text = group.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        var open = MakeButton("Open", "#E3F2FD", "#1565C0");
        open.Clicked += async (_, _) =>
        {
            _openGroup = group;
            await RefreshAsync();
        };
        row.Add(open, 1, 0);
        body.Children.Add(row);
        body.Children.Add(new Label
        {
            Text = $"{items.Count} task{(items.Count == 1 ? "" : "s")} · {completed} completed",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });
        var card = Card(body);
        var cardTap = new TapGestureRecognizer();
        cardTap.Tapped += async (_, _) =>
        {
            _openGroup = group;
            await RefreshAsync();
        };
        card.GestureRecognizers.Add(cardTap);
        return card;
    }

    private async Task<View> BuildOpenGroupAsync(SequenceTaskGroup group)
    {
        var items = await _service.GetItemsAsync(group.Id);
        var body = new VerticalStackLayout { Spacing = 10 };
        body.Children.Add(new Label
        {
            Text = group.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        body.Children.Add(new Label
        {
            Text = $"{items.Count(i => i.IsCompleted)} of {items.Count} completed",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        foreach (var item in items)
            body.Children.Add(BuildItemRow(group, item));

        var addTaskButton = MakeButton(
            "+ Add Task", "#E8F5E9", "#2E7D32");
        addTaskButton.Clicked += async (_, _) =>
        {
            var newTask =
                await TaskCreationHelper
                    .ShowCreateTaskAsync(
                        this,
                        _auth,
                        _tasks,
                        _ideasService);
            if (newTask != null)
            {
                await _service.SaveItemAsync(
                    new SequenceTaskItem
                    {
                        GroupId = group.Id,
                        Description = newTask.Title,
                        CreatedDate = DateTime.Now
                    });
                await RefreshAsync();
            }
        };
        body.Children.Add(addTaskButton);

        var close = MakeButton("Close Group", "#ECEFF1", "#37474F");
        close.Clicked += async (_, _) =>
        {
            _openGroup = null;
            await RefreshAsync();
        };
        body.Children.Add(close);

        var archive = MakeButton("Archive Group", "#FFEBEE", "#C62828");
        archive.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Archive Group", $"Archive \"{group.Name}\"?", "Archive", "Cancel")) return;
            await _service.ArchiveGroupAsync(group.Id);
            _openGroup = null;
            await RefreshAsync();
        };
        body.Children.Add(archive);
        return Card(body);
    }

    private View BuildItemRow(SequenceTaskGroup group, SequenceTaskItem item)
    {
        var body = new VerticalStackLayout { Spacing = 4 };
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(8, 6)
        };
        row.Add(new Label
        {
            Text = item.IsCompleted ? $"✓ {item.Description}" : item.Description,
            FontSize = 14,
            TextColor = item.IsCompleted ? Color.FromArgb("#8A8A8A") : Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        if (!item.IsCompleted)
        {
            var done = MakeButton("Done", "#E8F5E9", "#2E7D32");
            done.Clicked += async (_, _) =>
            {
                if (_busy) return;
                _busy = true;
                try
                {
                    item.IsCompleted = true;
                    item.CompletedDate = DateTime.Now;
                    await _service.UpdateItemAsync(item);
                    var prompt = (group.ExportPromptTemplate ?? "")
                        .Replace("{task}", item.Description);
                    await Clipboard.SetTextAsync(prompt);
                    done.Text = "✓ Copied!";
                    await DisplayAlert(
                        "Prompt Copied",
                        "✓ Prompt copied to clipboard — paste into your LLM before continuing to the next task.",
                        "OK");
                }
                finally { _busy = false; }
                await RefreshAsync();
            };
            row.Add(done, 1, 0);
        }
        body.Children.Add(row);
        if (item.IsCompleted)
            body.Children.Add(new Label
            {
                Text = $"Completed {item.CompletedDate:dd MMM yyyy}",
                FontSize = 11,
                TextColor = Color.FromArgb("#999"),
                Margin = new Thickness(8, -4, 0, 0)
            });
        return new Border
        {
            Content = body,
            Stroke = Color.FromArgb("#E0E0E0"),
            StrokeThickness = 1,
            BackgroundColor = item.IsCompleted ? Color.FromArgb("#F2F2F2") : Colors.White,
            Padding = 2
        };
    }

    private View BuildCreateForm()
    {
        var form = new VerticalStackLayout { Spacing = 8 };
        form.Children.Add(new Label
        {
            Text = "Create New Group",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        _nameEntry = new Entry
        {
            Placeholder = "Group name",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        _templateEditor = new Editor
        {
            Placeholder = "Wrap around each completed task. Use {task} where the task description should appear.",
            HeightRequest = 140,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        _tasksEditor = new Editor
        {
            Placeholder = "Paste tasks, one per line...",
            HeightRequest = 180,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        form.Children.Add(_nameEntry);
        form.Children.Add(_templateEditor);
        var tutorial = MakeButton("Use Tutorial Check Template", "#E8EAF6", "#3949AB");
        tutorial.Clicked += (_, _) => _templateEditor.Text = TutorialTemplate;
        form.Children.Add(tutorial);
        form.Children.Add(_tasksEditor);
        var save = MakeButton("Save & Start", "#E8F5E9", "#2E7D32");
        save.Clicked += async (_, _) => await SaveGroupAsync();
        form.Children.Add(save);
        return Card(form);
    }

    private async Task SaveGroupAsync()
    {
        if (_busy || string.IsNullOrWhiteSpace(_nameEntry.Text) || string.IsNullOrWhiteSpace(_templateEditor.Text)) return;
        _busy = true;
        try
        {
            var group = new SequenceTaskGroup
            {
                Name = _nameEntry.Text.Trim(),
                ExportPromptTemplate = _templateEditor.Text.Trim(),
                CreatedDate = DateTime.Now
            };
            await _service.SaveGroupAsync(group);
            var lines = (_tasksEditor.Text ?? "")
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0);
            foreach (var line in lines)
                await _service.SaveItemAsync(new SequenceTaskItem
                {
                    GroupId = group.Id,
                    Description = line,
                    CreatedDate = DateTime.Now
                });
            _openGroup = group;
        }
        finally { _busy = false; }
        await RefreshAsync();
    }

    private static Frame Card(View content) => new()
    {
        Content = content,
        Padding = 12,
        CornerRadius = 8,
        HasShadow = false,
        BorderColor = Color.FromArgb("#DDDDDD"),
        BackgroundColor = Color.FromArgb("#FAFAFA")
    };

    private static Button MakeButton(string text, string background, string foreground) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(background),
        TextColor = Color.FromArgb(foreground),
        CornerRadius = 6,
        FontSize = 12,
        HeightRequest = 36,
        Padding = new Thickness(10, 0)
    };
}
