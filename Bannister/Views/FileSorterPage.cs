using Bannister.Models;
using Bannister.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Storage;

namespace Bannister.Views;

public class FileSorterPage : ContentPage
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v"
    };

    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private List<SortFolder> _sortFolders = new();
    private List<SortSet> _sortSets = new();
    private int? _activeSetId = null;
    private List<string> _files = new();
    private int _currentIndex;
    private string? _lastMovedFrom;
    private string? _lastMovedTo;
    private string? _sourceFolder;

    private MediaElement _mediaElement = null!;
    private Image _imageView = null!;
    private Label _noFileLabel = null!;
    private Label _fileNameLabel = null!;
    private Label _progressLabel = null!;
    private Label _statusLabel = null!;
    private ScrollView _buttonScroll = null!;
    private HorizontalStackLayout _buttonRow = null!;
    private HorizontalStackLayout _setRow = null!;
    private VerticalStackLayout _mainStack = null!;
    private Button _skipBtn = null!;
    private Button _undoBtn = null!;
    private Button _backBtn = null!;
    private Button _openFolderBtn = null!;

    public FileSorterPage(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
        Title = "File Sorter";
        BuildUI();
    }

    private void BuildUI()
    {
        _mediaElement = new MediaElement
        {
            IsVisible = false,
            ShouldAutoPlay = true,
            ShouldLoopPlayback = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _imageView = new Image
        {
            IsVisible = false,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _noFileLabel = new Label
        {
            Text = "Pick a source folder to begin",
            TextColor = Color.FromArgb("#90A4AE"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var previewGrid = new Grid();
        previewGrid.Children.Add(_mediaElement);
        previewGrid.Children.Add(_imageView);
        previewGrid.Children.Add(_noFileLabel);

        _fileNameLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        };
        _progressLabel = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#546E7A")
        };
        var infoRow = new VerticalStackLayout
        {
            Padding = new Thickness(8, 4),
            Children = { _fileNameLabel, _progressLabel }
        };

        _openFolderBtn = CreateControlButton(" Source Folder", "#1565C0");
        _openFolderBtn.Clicked += async (_, _) => await OnPickSourceFolderAsync();
        _undoBtn = CreateControlButton("↩ Undo", "#F57F17");
        _undoBtn.IsEnabled = false;
        _undoBtn.Clicked += async (_, _) => await OnUndoAsync();
        _skipBtn = CreateControlButton("Next ▶", "#546E7A");
        _skipBtn.Clicked += async (_, _) => await AdvanceAsync();
        _backBtn = CreateControlButton("◀ Prev", "#546E7A");
        _backBtn.Clicked += async (_, _) => await GoBackAsync();
        var addFolderBtn = CreateControlButton("+ Add Folder", "#2E7D32");
        addFolderBtn.Clicked += async (_, _) => await OnAddFolderAsync();

        var firstSubRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _openFolderBtn, _undoBtn, _backBtn, _skipBtn, addFolderBtn }
        };
        _statusLabel = new Label
        {
            FontSize = 11,
            TextColor = Color.FromArgb("#90A4AE")
        };
        _buttonRow = new HorizontalStackLayout { Spacing = 8 };
        _buttonScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _buttonRow
        };
        _setRow = new HorizontalStackLayout { Spacing = 8 };
        var setScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _setRow
        };
        _mainStack = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 8,
            Children = { firstSubRow, _statusLabel, setScroll, _buttonScroll }
        };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(previewGrid, 0, 0);
        layout.Add(infoRow, 0, 1);
        layout.Add(_mainStack, 0, 2);
        Content = layout;
    }

    private static Button CreateControlButton(string text, string backgroundColor) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(backgroundColor),
        TextColor = Colors.White
    };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var conn = await _db.GetConnectionAsync();
        _sortSets = await conn.Table<SortSet>()
            .Where(set => set.Username == _auth.CurrentUsername)
            .OrderBy(set => set.SortOrder)
            .ToListAsync();
        _sortFolders = await conn.Table<SortFolder>()
            .Where(folder => folder.Username == _auth.CurrentUsername)
            .OrderBy(folder => folder.SortOrder)
            .ToListAsync();
        RebuildSetRow();
        RebuildSortButtons();
    }

    private async Task OnPickSourceFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful)
            return;

        _sourceFolder = result.Folder.Path;
        _files = Directory.EnumerateFiles(_sourceFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _currentIndex = 0;
        await ShowCurrentFileAsync();
    }

    private Task ShowCurrentFileAsync()
    {
        if (_files.Count == 0 || _currentIndex >= _files.Count)
        {
            _mediaElement.IsVisible = false;
            _imageView.IsVisible = false;
            _noFileLabel.IsVisible = true;
            _noFileLabel.Text = _files.Count == 0
                ? "No media files found in folder."
                : "All files sorted!";
            _fileNameLabel.Text = "";
            _progressLabel.Text = "";
            return Task.CompletedTask;
        }

        string path = _files[_currentIndex];
        string ext = Path.GetExtension(path).ToLowerInvariant();
        bool isVideo = ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".m4v";

        _noFileLabel.IsVisible = false;
        _fileNameLabel.Text = Path.GetFileName(path);
        _progressLabel.Text = $"{_currentIndex + 1} / {_files.Count}";

        if (isVideo)
        {
            _imageView.IsVisible = false;
            _mediaElement.IsVisible = true;
            _mediaElement.Source = MediaSource.FromFile(path);
            _mediaElement.Play();
        }
        else
        {
            _mediaElement.IsVisible = false;
            _mediaElement.Stop();
            _imageView.IsVisible = true;
            _imageView.Source = ImageSource.FromFile(path);
        }

        _statusLabel.Text = "";
        return Task.CompletedTask;
    }

    private async Task MoveCurrentFileAsync(SortFolder folder)
    {
        if (_files.Count == 0 || _currentIndex < 0 || _currentIndex >= _files.Count)
            return;

        string src = _files[_currentIndex];
        string dest = Path.Combine(folder.Path, Path.GetFileName(src));
        if (File.Exists(dest))
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(src);
            string ext2 = Path.GetExtension(src);
            dest = Path.Combine(folder.Path, $"{nameNoExt}_{DateTime.Now:yyyyMMddHHmmss}{ext2}");
        }

        _mediaElement.Stop();
        File.Move(src, dest);
        _lastMovedFrom = dest;
        _lastMovedTo = src;
        _undoBtn.IsEnabled = true;
        _files.RemoveAt(_currentIndex);
        if (_currentIndex >= _files.Count) _currentIndex = _files.Count - 1;
        if (_currentIndex < 0) _currentIndex = 0;
        await ShowCurrentFileAsync();
    }

    private async Task AdvanceAsync()
    {
        _mediaElement.Stop();
        _currentIndex++;
        if (_currentIndex >= _files.Count) _currentIndex = _files.Count - 1;
        if (_currentIndex < 0) _currentIndex = 0;
        await ShowCurrentFileAsync();
    }

    private async Task GoBackAsync()
    {
        _mediaElement.Stop();
        if (_currentIndex > 0) _currentIndex--;
        await ShowCurrentFileAsync();
    }

    private async Task OnUndoAsync()
    {
        if (_lastMovedFrom == null || _lastMovedTo == null)
            return;

        if (File.Exists(_lastMovedFrom))
        {
            File.Move(_lastMovedFrom, _lastMovedTo);
            _files.Insert(_currentIndex, _lastMovedTo);
            _lastMovedFrom = null;
            _lastMovedTo = null;
            _undoBtn.IsEnabled = false;
            await ShowCurrentFileAsync();
        }
    }

    private async Task OnAddFolderAsync()
    {
        string? name = await DisplayPromptAsync("Add Folder", "Button label:");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful)
            return;

        string? setChoice = await DisplayActionSheet(
            "Add to set",
            "Cancel",
            null,
            new[] { "No Set" }.Concat(_sortSets.Select(set => set.Name)).ToArray());
        if (setChoice == null || setChoice == "Cancel")
            return;
        int? setId = setChoice == "No Set"
            ? null
            : _sortSets.FirstOrDefault(set => set.Name == setChoice)?.Id;

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(new SortFolder
        {
            Username = _auth.CurrentUsername,
            Name = name.Trim(),
            Path = result.Folder.Path,
            SortOrder = _sortFolders.Count,
            SetId = setId
        });
        await LoadAsync();
    }

    private void RebuildSortButtons()
    {
        _buttonRow.Children.Clear();
        var visible = _activeSetId == null
            ? _sortFolders
            : _sortFolders.Where(folder => folder.SetId == _activeSetId).ToList();
        foreach (SortFolder folder in visible)
        {
            var btn = new Button
            {
                Text = folder.Name,
                BackgroundColor = Color.FromArgb("#1A237E"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 48,
                Padding = new Thickness(16, 0),
                FontSize = 13
            };
            btn.Clicked += async (_, _) => await MoveCurrentFileAsync(folder);

            var menuBtn = new Button
            {
                Text = "⋮",
                FontSize = 14,
                WidthRequest = 28,
                HeightRequest = 48,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#283593"),
                TextColor = Colors.White,
                CornerRadius = 8
            };
            menuBtn.Clicked += async (_, _) => await EditSortFolderAsync(folder);
            _buttonRow.Children.Add(btn);
            _buttonRow.Children.Add(menuBtn);
        }
    }

    private void RebuildSetRow()
    {
        _setRow.Children.Clear();

        var addSetBtn = new Button
        {
            Text = "+ Set",
            FontSize = 11,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 16
        };
        addSetBtn.Clicked += async (_, _) =>
        {
            string? name = await DisplayPromptAsync("New Set", "Set name:");
            if (string.IsNullOrWhiteSpace(name)) return;
            var conn = await _db.GetConnectionAsync();
            await conn.InsertAsync(new SortSet
            {
                Username = _auth.CurrentUsername,
                Name = name.Trim(),
                SortOrder = _sortSets.Count
            });
            await LoadAsync();
        };
        _setRow.Children.Add(addSetBtn);

        foreach (SortSet set in _sortSets)
        {
            bool isActive = _activeSetId == set.Id;
            var chip = new Button
            {
                Text = set.Name,
                FontSize = 11,
                HeightRequest = 32,
                Padding = new Thickness(10, 0),
                BackgroundColor = isActive
                    ? Color.FromArgb("#1565C0")
                    : Color.FromArgb("#ECEFF1"),
                TextColor = isActive ? Colors.White : Color.FromArgb("#263238"),
                CornerRadius = 16
            };
            chip.Clicked += (_, _) =>
            {
                _activeSetId = isActive ? null : set.Id;
                RebuildSetRow();
                RebuildSortButtons();
            };

            var menuBtn = new Button
            {
                Text = "⋮",
                FontSize = 14,
                WidthRequest = 28,
                HeightRequest = 32,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#CFD8DC"),
                TextColor = Color.FromArgb("#263238"),
                CornerRadius = 8
            };
            menuBtn.Clicked += async (_, _) =>
            {
                string? action = await DisplayActionSheet(
                    set.Name, "Cancel", null, "Rename", "Delete");
                if (action == "Rename")
                {
                    string? newName = await DisplayPromptAsync(
                        "Rename Set", "New name:", initialValue: set.Name);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    set.Name = newName.Trim();
                    var conn = await _db.GetConnectionAsync();
                    await conn.UpdateAsync(set);
                    await LoadAsync();
                }
                else if (action == "Delete")
                {
                    bool confirm = await DisplayAlert(
                        "Delete Set",
                        $"Delete '{set.Name}'? Folders in it become unassigned.",
                        "Delete",
                        "Cancel");
                    if (!confirm) return;
                    var conn = await _db.GetConnectionAsync();
                    var affected = _sortFolders.Where(folder => folder.SetId == set.Id).ToList();
                    foreach (SortFolder folder in affected)
                    {
                        folder.SetId = null;
                        await conn.UpdateAsync(folder);
                    }
                    await conn.DeleteAsync(set);
                    if (_activeSetId == set.Id) _activeSetId = null;
                    await LoadAsync();
                }
            };
            _setRow.Children.Add(chip);
            _setRow.Children.Add(menuBtn);
        }
    }

    private async Task EditSortFolderAsync(SortFolder folder)
    {
        string action = await DisplayActionSheet(
            folder.Name,
            "Cancel",
            null,
            "Rename",
            "Move to Set",
            "Remove");
        var conn = await _db.GetConnectionAsync();

        if (action == "Rename")
        {
            string? newName = await DisplayPromptAsync("Rename Folder", "New button label:", initialValue: folder.Name);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                folder.Name = newName.Trim();
                await conn.UpdateAsync(folder);
                await LoadAsync();
            }
        }
        else if (action == "Move to Set")
        {
            string? setChoice = await DisplayActionSheet(
                "Move to Set",
                "Cancel",
                null,
                new[] { "No Set" }.Concat(_sortSets.Select(set => set.Name)).ToArray());
            if (setChoice == null || setChoice == "Cancel") return;
            folder.SetId = setChoice == "No Set"
                ? null
                : _sortSets.FirstOrDefault(set => set.Name == setChoice)?.Id;
            await conn.UpdateAsync(folder);
            await LoadAsync();
        }
        else if (action == "Remove" &&
                 await DisplayAlert("Remove Folder", $"Remove '{folder.Name}'?", "Remove", "Cancel"))
        {
            await conn.DeleteAsync(folder);
            await LoadAsync();
        }
    }
}
