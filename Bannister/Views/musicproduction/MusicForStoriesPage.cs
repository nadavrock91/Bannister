using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Bannister.Views;

public class MusicForStoriesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MusicProductionService _musicService;
    private readonly DatabaseService _db;

    private readonly VerticalStackLayout _projectsContainer = new() { Spacing = 10 };
    private readonly VerticalStackLayout _linesContainer = new() { Spacing = 10 };
    private readonly Label _headerLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _newProjectButton = new();
    private readonly Button _addLineButton = new();
    private readonly Button _settingsButton = new();
    private readonly Button _backToDraftsButton = new();

    private MusicProject? _currentProject;
    private List<MusicProject> _projects = new();
    private List<MusicLine> _lines = new();

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

        if (_currentProject == null)
            await LoadProjectsAsync();
        else
            await LoadLinesAsync();
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
        _headerLabel.Text = "Music for Stories";
        _headerLabel.FontSize = 24;
        _headerLabel.FontAttributes = FontAttributes.Bold;
        _headerLabel.TextColor = Color.FromArgb("#3949AB");
        topStack.Children.Add(_headerLabel);

        _statusLabel.FontSize = 13;
        _statusLabel.TextColor = Color.FromArgb("#666");
        topStack.Children.Add(_statusLabel);

        var buttonRow = new HorizontalStackLayout { Spacing = 8 };

        _backToDraftsButton.Text = "Drafts";
        _backToDraftsButton.BackgroundColor = Color.FromArgb("#ECEFF1");
        _backToDraftsButton.TextColor = Color.FromArgb("#333");
        _backToDraftsButton.CornerRadius = 8;
        _backToDraftsButton.Padding = new Thickness(14, 8);
        _backToDraftsButton.IsVisible = false;
        _backToDraftsButton.Clicked += async (s, e) => await ShowDraftsAsync();
        buttonRow.Children.Add(_backToDraftsButton);

        _newProjectButton.Text = "+ New Draft";
        _newProjectButton.BackgroundColor = Color.FromArgb("#3949AB");
        _newProjectButton.TextColor = Colors.White;
        _newProjectButton.CornerRadius = 8;
        _newProjectButton.Padding = new Thickness(14, 8);
        _newProjectButton.IsVisible = IsMaster;
        _newProjectButton.Clicked += OnNewProjectClicked;
        buttonRow.Children.Add(_newProjectButton);

        _settingsButton.Text = "Settings";
        _settingsButton.BackgroundColor = Color.FromArgb("#E0F7FA");
        _settingsButton.TextColor = Color.FromArgb("#006064");
        _settingsButton.CornerRadius = 8;
        _settingsButton.Padding = new Thickness(14, 8);
        _settingsButton.IsVisible = false;
        _settingsButton.Clicked += OnProjectSettingsClicked;
        buttonRow.Children.Add(_settingsButton);

        _addLineButton.Text = "+ Add Card";
        _addLineButton.BackgroundColor = Color.FromArgb("#3949AB");
        _addLineButton.TextColor = Colors.White;
        _addLineButton.CornerRadius = 8;
        _addLineButton.Padding = new Thickness(14, 8);
        _addLineButton.IsVisible = false;
        _addLineButton.Clicked += OnAddLineClicked;
        buttonRow.Children.Add(_addLineButton);

        topStack.Children.Add(buttonRow);
        topFrame.Content = topStack;
        Grid.SetRow(topFrame, 0);
        pageGrid.Children.Add(topFrame);

        var scroll = new ScrollView();
        var contentStack = new VerticalStackLayout { Spacing = 12 };
        contentStack.Children.Add(_projectsContainer);
        contentStack.Children.Add(_linesContainer);
        scroll.Content = contentStack;
        Grid.SetRow(scroll, 1);
        pageGrid.Children.Add(scroll);

        Content = pageGrid;
    }

    private async Task LoadProjectsAsync()
    {
        _currentProject = null;
        _projects = await _musicService.GetActiveProjectsAsync(_auth.CurrentUsername);
        _projects = _projects.Where(p => p.ParentProjectId == null).ToList();

        _headerLabel.Text = "Music for Stories";
        _statusLabel.Text = _db.IsReadOnly
            ? "Secondary mode: drafts can be viewed but not edited."
            : $"{_projects.Count} draft{(_projects.Count == 1 ? "" : "s")}";

        _newProjectButton.IsVisible = IsMaster;
        _backToDraftsButton.IsVisible = false;
        _settingsButton.IsVisible = false;
        _addLineButton.IsVisible = false;

        _linesContainer.Children.Clear();
        _projectsContainer.Children.Clear();

        if (_projects.Count == 0)
        {
            _projectsContainer.Children.Add(new Label
            {
                Text = IsMaster ? "No music drafts yet. Create one to start." : "No music drafts found.",
                FontSize = 14,
                TextColor = Color.FromArgb("#666"),
                Margin = new Thickness(4, 12)
            });
            return;
        }

        foreach (var project in _projects)
        {
            _projectsContainer.Children.Add(await CreateProjectCardAsync(project));
        }
    }

    private async Task<View> CreateProjectCardAsync(MusicProject project)
    {
        var stats = await _musicService.GetLinesAsync(project.Id);
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add(new Label
        {
            Text = project.DisplayName,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        textStack.Children.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(project.ProjectCategory)
                ? $"{stats.Count} card{(stats.Count == 1 ? "" : "s")}"
                : $"{project.ProjectCategory} • {stats.Count} card{(stats.Count == 1 ? "" : "s")}",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            textStack.Children.Add(new Label
            {
                Text = project.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#555"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        var actions = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        var openButton = SmallButton("Open", Color.FromArgb("#3949AB"), Colors.White);
        openButton.Clicked += async (s, e) => await OpenProjectAsync(project);
        actions.Children.Add(openButton);

        if (IsMaster)
        {
            var deleteButton = SmallButton("Delete", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
            deleteButton.Clicked += async (s, e) => await DeleteProjectAsync(project);
            actions.Children.Add(deleteButton);
        }

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        frame.Content = grid;
        return frame;
    }

    private async Task OpenProjectAsync(MusicProject project)
    {
        _currentProject = project;
        await LoadLinesAsync();
    }

    private async Task ShowDraftsAsync()
    {
        await LoadProjectsAsync();
    }

    private async Task LoadLinesAsync()
    {
        if (_currentProject == null)
            return;

        _currentProject = await _musicService.GetProjectByIdAsync(_currentProject.Id) ?? _currentProject;
        _lines = await _musicService.GetLinesAsync(_currentProject.Id);

        _headerLabel.Text = _currentProject.DisplayName;
        _statusLabel.Text = string.IsNullOrWhiteSpace(_currentProject.ProjectCategory)
            ? $"{_lines.Count} card{(_lines.Count == 1 ? "" : "s")}"
            : $"{_currentProject.ProjectCategory} • {_lines.Count} card{(_lines.Count == 1 ? "" : "s")}";

        _newProjectButton.IsVisible = false;
        _backToDraftsButton.IsVisible = true;
        _settingsButton.IsVisible = IsMaster;
        _addLineButton.IsVisible = IsMaster;

        _projectsContainer.Children.Clear();
        _linesContainer.Children.Clear();

        if (_lines.Count == 0)
        {
            _linesContainer.Children.Add(new Label
            {
                Text = IsMaster ? "No cards yet. Add one to start." : "No cards in this draft.",
                FontSize = 14,
                TextColor = Color.FromArgb("#666"),
                Margin = new Thickness(4, 12)
            });
            return;
        }

        foreach (var line in _lines)
        {
            _linesContainer.Children.Add(CreateLineCard(line, _lines.Count));
        }
    }

    private View CreateLineCard(MusicLine line, int totalLines)
    {
        var frame = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true
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

        headerGrid.Children.Add(new Label
        {
            Text = $"Card {line.LineOrder}",
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
                _statusLabel.Text = $"Saved card {line.LineOrder}.";
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

        var border = new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = 4,
            BackgroundColor = Colors.White,
            Content = editor
        };

        stack.Children.Add(border);
        return stack;
    }

    private string GetEditorText(View columnView)
    {
        if (columnView is not VerticalStackLayout stack)
            return "";

        var border = stack.Children.OfType<Border>().FirstOrDefault();
        return border?.Content is Editor editor ? editor.Text?.Trim() ?? "" : "";
    }

    private async void OnNewProjectClicked(object? sender, EventArgs e)
    {
        if (!IsMaster) return;

        var title = await DisplayPromptAsync(
            "New Music Draft",
            "Draft title:",
            placeholder: "Story soundtrack draft");

        if (string.IsNullOrWhiteSpace(title)) return;

        var category = await DisplayPromptAsync(
            "Category",
            "Optional category:",
            placeholder: "series, client, project type");

        try
        {
            var project = await _musicService.CreateProjectAsync(
                _auth.CurrentUsername,
                title.Trim(),
                category?.Trim() ?? "");

            await OpenProjectAsync(project);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Create Failed", ex.Message, "OK");
        }
    }

    private async void OnProjectSettingsClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        var title = await DisplayPromptAsync(
            "Draft Title",
            "Update the draft title:",
            initialValue: _currentProject.Name);
        if (title == null) return;

        var category = await DisplayPromptAsync(
            "Category",
            "Update the project category:",
            initialValue: _currentProject.ProjectCategory);
        if (category == null) return;

        _currentProject.Name = title.Trim();
        _currentProject.ProjectCategory = category.Trim();
        await _musicService.UpdateProjectAsync(_currentProject);
        await LoadLinesAsync();
    }

    private async Task DeleteProjectAsync(MusicProject project)
    {
        if (!IsMaster) return;

        bool delete = await DisplayAlert(
            "Delete Draft",
            $"Delete '{project.DisplayName}' and all its cards?",
            "Delete",
            "Cancel");

        if (!delete) return;

        await _musicService.DeleteProjectAsync(project.Id);
        await LoadProjectsAsync();
    }

    private async void OnAddLineClicked(object? sender, EventArgs e)
    {
        if (!IsMaster || _currentProject == null) return;

        await _musicService.AddLineAsync(_currentProject.Id);
        await LoadLinesAsync();
    }

    private async Task DeleteLineAsync(MusicLine line)
    {
        if (!IsMaster) return;

        bool delete = await DisplayAlert(
            "Delete Card",
            $"Delete card {line.LineOrder}?",
            "Delete",
            "Cancel");

        if (!delete) return;

        await _musicService.DeleteLineAsync(line.Id);
        await LoadLinesAsync();
    }

    private static Button SmallButton(string text, Color background, Color textColor)
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
}
