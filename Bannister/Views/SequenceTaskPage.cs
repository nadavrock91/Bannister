using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SequenceTaskPage : ContentPage
{
    private const string TutorialTemplate =
        "I have completed the following development tasks:\n\n" +
        "{tasks}\n\n" +
        "Please evaluate whether this work collectively warrants creating a video tutorial. " +
        "Consider: is there a substantial new feature a user would need guidance on? " +
        "Is there enough new functionality to fill a meaningful tutorial? " +
        "Return your verdict and reasoning.";

    private readonly SequenceTaskService _service;
    private readonly VerticalStackLayout _content;
    private int? _expandedGroupId;
    private Entry _groupNameEntry = null!;
    private Editor _templateEditor = null!;
    private bool _busy;

    public SequenceTaskPage(SequenceTaskService service)
    {
        _service = service;
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
        var groups = await _service.GetActiveGroupsAsync();
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "Sequence Tasks",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        _content.Children.Add(new Label
        {
            Text = "Collect related work, then export it as a numbered LLM prompt.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        if (groups.Count == 0)
        {
            _content.Children.Add(new Label
            {
                Text = "No active sequence groups yet.",
                FontSize = 13,
                TextColor = Color.FromArgb("#777"),
                FontAttributes = FontAttributes.Italic
            });
        }
        else
        {
            foreach (var group in groups)
                _content.Children.Add(await BuildGroupCardAsync(group));
        }

        _content.Children.Add(BuildCreateForm());
    }

    private async Task<View> BuildGroupCardAsync(SequenceTaskGroup group)
    {
        var items = await _service.GetItemsAsync(group.Id);
        var inner = new VerticalStackLayout { Spacing = 8 };
        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        header.Add(new Label
        {
            Text = group.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        var openButton = MakeButton(_expandedGroupId == group.Id ? "Close" : "Open", "#E3F2FD", "#1565C0");
        openButton.Clicked += async (_, _) =>
        {
            _expandedGroupId = _expandedGroupId == group.Id ? null : group.Id;
            await RefreshAsync();
        };
        header.Add(openButton, 1, 0);
        inner.Children.Add(header);
        inner.Children.Add(new Label
        {
            Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")}",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });

        if (_expandedGroupId == group.Id)
        {
            foreach (var item in items)
            {
                var itemRow = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Padding = new Thickness(8, 4),
                    BackgroundColor = Colors.White
                };
                itemRow.Add(new Label
                {
                    Text = item.Description,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#333"),
                    VerticalOptions = LayoutOptions.Center
                }, 0, 0);
                var deleteButton = MakeButton("Delete", "#FFEBEE", "#C62828");
                deleteButton.Clicked += async (_, _) =>
                {
                    await _service.DeleteItemAsync(item.Id);
                    await RefreshAsync();
                };
                itemRow.Add(deleteButton, 1, 0);
                inner.Children.Add(itemRow);
            }

            var addEntry = new Entry
            {
                Placeholder = "Add a task...",
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#222"),
                HorizontalOptions = LayoutOptions.Fill
            };
            var addButton = MakeButton("Add", "#E8F5E9", "#2E7D32");
            var addRow = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            addRow.Add(addEntry, 0, 0);
            addRow.Add(addButton, 1, 0);
            addButton.Clicked += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(addEntry.Text)) return;
                await _service.SaveItemAsync(new SequenceTaskItem
                {
                    GroupId = group.Id,
                    Description = addEntry.Text.Trim(),
                    CreatedDate = DateTime.Now
                });
                await RefreshAsync();
            };
            inner.Children.Add(addRow);

            var exportButton = MakeButton("Export & Archive", "#FFF3E0", "#E65100");
            exportButton.Clicked += async (_, _) =>
            {
                if (items.Count == 0)
                {
                    await DisplayAlert("No Tasks", "Add at least one task before exporting.", "OK");
                    return;
                }
                var numbered = string.Join("\n", items.Select((i, index) => $"{index + 1}. {i.Description}"));
                var prompt = (group.ExportPromptTemplate ?? "").Replace("{tasks}", numbered);
                await Clipboard.SetTextAsync(prompt);
                await _service.ArchiveGroupAsync(group.Id);
                _expandedGroupId = null;
                await DisplayAlert("Copied", "The sequence prompt was copied and the group archived.", "OK");
                await RefreshAsync();
            };
            inner.Children.Add(exportButton);
        }

        return new Frame
        {
            Content = inner,
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#DDDDDD"),
            BackgroundColor = Color.FromArgb("#FAFAFA")
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
        _groupNameEntry = new Entry
        {
            Placeholder = "Group name",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        _templateEditor = new Editor
        {
            Placeholder = "Describe what you want the LLM to do with these tasks. Use {tasks} where the task list should appear.",
            HeightRequest = 140,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        form.Children.Add(_groupNameEntry);
        form.Children.Add(_templateEditor);
        var tutorialButton = MakeButton("Use Tutorial Check Template", "#E8EAF6", "#3949AB");
        tutorialButton.Clicked += (_, _) => _templateEditor.Text = TutorialTemplate;
        form.Children.Add(tutorialButton);
        var saveButton = MakeButton("Save", "#E8F5E9", "#2E7D32");
        saveButton.Clicked += async (_, _) =>
        {
            if (_busy || string.IsNullOrWhiteSpace(_groupNameEntry.Text) || string.IsNullOrWhiteSpace(_templateEditor.Text)) return;
            _busy = true;
            try
            {
                await _service.SaveGroupAsync(new SequenceTaskGroup
                {
                    Name = _groupNameEntry.Text.Trim(),
                    ExportPromptTemplate = _templateEditor.Text.Trim(),
                    CreatedDate = DateTime.Now
                });
            }
            finally { _busy = false; }
            await RefreshAsync();
        };
        form.Children.Add(saveButton);
        return new Frame
        {
            Content = form,
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#DDDDDD"),
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };
    }

    private static Button MakeButton(string text, string background, string foreground)
        => new()
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
