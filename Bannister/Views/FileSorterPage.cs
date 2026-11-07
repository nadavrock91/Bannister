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
    private List<FavoriteSourceFolder> _favFolders = new();
    private List<string> _files = new();
    private int _currentIndex;
    private string? _lastMovedFrom;
    private string? _lastMovedTo;
    private string? _sourceFolder;
    private bool _muteVideos = false;

    private MediaElement _mediaElement = null!;
    private Image _imageView = null!;
    private Label _noFileLabel = null!;
    private Label _fileNameLabel = null!;
    private Label _progressLabel = null!;
    private Label _statusLabel = null!;
    private ScrollView _buttonScroll = null!;
    private HorizontalStackLayout _buttonRow = null!;
    private HorizontalStackLayout _copyButtonRow = null!;
    private ScrollView _copyButtonScroll = null!;
    private HorizontalStackLayout _setRow = null!;
    private HorizontalStackLayout _favRow = null!;
    private VerticalStackLayout _mainStack = null!;
    private Button _skipBtn = null!;
    private Button _undoBtn = null!;
    private Button _backBtn = null!;
    private Button _openFolderBtn = null!;
    private Button _muteBtn = null!;
    private Grid _manageOverlay = null!;
    private VerticalStackLayout _manageList = null!;

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
        _muteBtn = new Button
        {
            Text = " Mute",
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8
        };
        _muteBtn.Clicked += (_, _) =>
        {
            _muteVideos = !_muteVideos;
            _mediaElement.ShouldMute = _muteVideos;
            _muteBtn.Text = _muteVideos ? " Mute" : " Mute";
            _muteBtn.BackgroundColor = _muteVideos
                ? Color.FromArgb("#B71C1C")
                : Color.FromArgb("#ECEFF1");
            _muteBtn.TextColor = _muteVideos
                ? Colors.White
                : Color.FromArgb("#37474F");
        };
        var manageBtn = new Button
        {
            Text = "⚙ Manage",
            FontSize = 13,
            HeightRequest = 36,
            Padding = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#37474F"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        manageBtn.Clicked += (_, _) =>
        {
            RebuildManageList();
            _manageOverlay.IsVisible = true;
        };

        var firstSubRow = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                Children = { _muteBtn, _openFolderBtn, _undoBtn, _backBtn, _skipBtn, addFolderBtn, manageBtn }
            }
        };
        var favoriteLabel = new Label
        {
            Text = "Source Folders:",
            FontSize = 11,
            TextColor = Color.FromArgb("#546E7A")
        };
        _favRow = new HorizontalStackLayout { Spacing = 8 };
        var favoriteScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _favRow
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
        var moveLabel = new Label
        {
            Text = "Move to:",
            FontSize = 11,
            TextColor = Color.FromArgb("#546E7A")
        };
        var copyLabel = new Label
        {
            Text = "Copy to:",
            FontSize = 11,
            TextColor = Color.FromArgb("#546E7A")
        };
        _copyButtonRow = new HorizontalStackLayout { Spacing = 8 };
        _copyButtonScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _copyButtonRow
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
            Children =
            {
                favoriteLabel, favoriteScroll, firstSubRow, _statusLabel,
                setScroll, moveLabel, _buttonScroll, copyLabel, _copyButtonScroll
            }
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

        _manageOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#CC000000"),
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var manageCard = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = new Thickness(16),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(16),
            HasShadow = true
        };
        var manageInner = new VerticalStackLayout { Spacing = 8 };
        var manageHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        manageHeader.Add(new Label
        {
            Text = "Manage Buttons",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        var closeManageBtn = new Button
        {
            Text = "✕",
            FontSize = 14,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8
        };
        closeManageBtn.Clicked += (_, _) => _manageOverlay.IsVisible = false;
        manageHeader.Add(closeManageBtn, 1, 0);
        manageInner.Children.Add(manageHeader);
        _manageList = new VerticalStackLayout { Spacing = 6 };
        manageInner.Children.Add(new ScrollView
        {
            Content = _manageList,
            MaximumHeightRequest = 400
        });
        manageCard.Content = manageInner;
        _manageOverlay.Children.Add(manageCard);
        Grid.SetRowSpan(_manageOverlay, 3);
        layout.Children.Add(_manageOverlay);
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
        _favFolders = await conn.Table<FavoriteSourceFolder>()
            .Where(folder => folder.Username == _auth.CurrentUsername)
            .OrderBy(folder => folder.SortOrder)
            .ToListAsync();
        _sortFolders = await conn.Table<SortFolder>()
            .Where(folder => folder.Username == _auth.CurrentUsername)
            .OrderBy(folder => folder.SortOrder)
            .ToListAsync();
        RebuildFavRow();
        RebuildSetRow();
        RebuildSortButtons();
    }

    private async Task OnPickSourceFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful)
            return;

        _sourceFolder = result.Folder.Path;
        var files = Directory.EnumerateFiles(_sourceFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .ToList();
        string? sortChoice = await DisplayActionSheet(
            "Sort files by", "Cancel", null,
            "Name A→Z",
            "Name Z→A",
            "Date newest first",
            "Date oldest first");
        if (sortChoice == null || sortChoice == "Cancel") return;

        _files = sortChoice switch
        {
            "Name A→Z"          => files.OrderBy(file => System.IO.Path.GetFileName(file)).ToList(),
            "Name Z→A"          => files.OrderByDescending(file => System.IO.Path.GetFileName(file)).ToList(),
            "Date newest first" => files.OrderByDescending(file => System.IO.File.GetLastWriteTime(file)).ToList(),
            "Date oldest first" => files.OrderBy(file => System.IO.File.GetLastWriteTime(file)).ToList(),
            _                   => files.OrderBy(file => file).ToList()
        };
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
            _mediaElement.ShouldMute = _muteVideos;
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

        if (!Directory.Exists(folder.Path))
        {
            bool retry = await DisplayAlert("Folder not found",
                $"'{folder.Name}' folder no longer exists:\n{folder.Path}\n\nPick a new location?",
                "Pick Folder", "Cancel");
            if (!retry) return;
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (!result.IsSuccessful) return;
            folder.Path = result.Folder.Path;
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(folder);
            dest = System.IO.Path.Combine(folder.Path,
                System.IO.Path.GetFileName(src));
            if (File.Exists(dest))
            {
                string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(src);
                string ext2 = System.IO.Path.GetExtension(src);
                dest = System.IO.Path.Combine(folder.Path,
                    $"{nameNoExt}_{DateTime.Now:yyyyMMddHHmmss}{ext2}");
            }
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

    private async Task CopyCurrentFileAsync(SortFolder folder)
    {
        if (_files.Count == 0 || _currentIndex >= _files.Count) return;
        string src = _files[_currentIndex];
        string dest = System.IO.Path.Combine(folder.Path,
            System.IO.Path.GetFileName(src));
        if (File.Exists(dest))
        {
            string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(src);
            string ext = System.IO.Path.GetExtension(src);
            dest = System.IO.Path.Combine(folder.Path,
                $"{nameNoExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
        }
        if (!Directory.Exists(folder.Path))
        {
            bool retry = await DisplayAlert("Folder not found",
                $"'{folder.Name}' folder no longer exists:\n{folder.Path}\n\nPick a new location?",
                "Pick Folder", "Cancel");
            if (!retry) return;
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (!result.IsSuccessful) return;
            folder.Path = result.Folder.Path;
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(folder);
            dest = System.IO.Path.Combine(folder.Path,
                System.IO.Path.GetFileName(src));
            if (File.Exists(dest))
            {
                string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(src);
                string ext2 = System.IO.Path.GetExtension(src);
                dest = System.IO.Path.Combine(folder.Path,
                    $"{nameNoExt}_{DateTime.Now:yyyyMMddHHmmss}{ext2}");
            }
        }
        try
        {
            File.Copy(src, dest);
            _statusLabel.Text = $"Copied to {folder.Name}";
            _statusLabel.TextColor = Color.FromArgb("#1B5E20");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Copy failed: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
        }
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
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful)
            return;

        string defaultName = System.IO.Path.GetFileName(
            result.Folder.Path.TrimEnd('\\', '/'));
        string? name = await DisplayPromptAsync(
            "Add Folder", "Button label:", initialValue: defaultName);
        if (string.IsNullOrWhiteSpace(name))
            return;

        int? setId = _activeSetId;
        if (setId == null && _sortSets.Count > 0)
        {
            string? setChoice = await DisplayActionSheet(
                "Add to set", "Cancel", null,
                new[] { "No Set" }.Concat(_sortSets.Select(set => set.Name)).ToArray());
            if (setChoice == null || setChoice == "Cancel") return;
            setId = setChoice == "No Set" ? null
                : _sortSets.FirstOrDefault(set => set.Name == setChoice)?.Id;
        }

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
            _buttonRow.Children.Add(btn);
        }

        _copyButtonRow.Children.Clear();
        var visibleCopy = _activeSetId == null
            ? _sortFolders
            : _sortFolders.Where(folder => folder.SetId == _activeSetId).ToList();
        foreach (SortFolder folder in visibleCopy)
        {
            var btn = new Button
            {
                Text = folder.Name,
                BackgroundColor = Color.FromArgb("#1B5E20"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 48,
                Padding = new Thickness(16, 0),
                FontSize = 13
            };
            btn.Clicked += async (_, _) => await CopyCurrentFileAsync(folder);
            _copyButtonRow.Children.Add(btn);
        }
    }

    private void RebuildManageList()
    {
        _manageList.Children.Clear();

        // ── SETS SECTION ─────────────────────────────────────────────
        _manageList.Children.Add(new Label
        {
            Text = "Sets",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (_sortSets.Count == 0)
            _manageList.Children.Add(new Label
            {
                Text = "No sets yet. Use '+ Set' to create one.",
                FontSize = 12,
                TextColor = Color.FromArgb("#90A4AE")
            });

        foreach (var set in _sortSets)
        {
            var setRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 4,
                Margin = new Thickness(0, 2)
            };
            setRow.Add(new Label
            {
                Text = set.Name,
                FontSize = 13,
                TextColor = Color.FromArgb("#263238"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var renameSetBtn = new Button
            {
                Text = "✏️", FontSize = 12, WidthRequest = 34, HeightRequest = 34,
                Padding = 0, CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0")
            };
            renameSetBtn.Clicked += async (_, _) =>
            {
                string? newName = await DisplayPromptAsync(
                    "Rename Set", "New name:", initialValue: set.Name);
                if (string.IsNullOrWhiteSpace(newName)) return;
                set.Name = newName.Trim();
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(set);
                await LoadAsync(); RebuildManageList();
            };
            setRow.Add(renameSetBtn, 1, 0);

            var deleteSetBtn = new Button
            {
                Text = "", FontSize = 12, WidthRequest = 34, HeightRequest = 34,
                Padding = 0, CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828")
            };
            deleteSetBtn.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert("Delete Set",
                    $"Delete '{set.Name}'? Folders in it become unassigned.",
                    "Delete", "Cancel");
                if (!confirm) return;
                var conn = await _db.GetConnectionAsync();
                var affected = _sortFolders.Where(f => f.SetId == set.Id).ToList();
                foreach (var f in affected) { f.SetId = null; await conn.UpdateAsync(f); }
                await conn.DeleteAsync(set);
                if (_activeSetId == set.Id) _activeSetId = null;
                await LoadAsync(); RebuildManageList();
            };
            setRow.Add(deleteSetBtn, 2, 0);
            _manageList.Children.Add(setRow);
        }

        _manageList.Children.Add(new Label
        {
            Text = "Folders",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            Margin = new Thickness(0, 12, 0, 4)
        });

        void AddCollapsibleGroup(string title, int? setId, List<SortFolder> group)
        {
            if (group.Count == 0) return;

            var folderContainer = new VerticalStackLayout
            {
                Spacing = 4,
                IsVisible = false
            };
            var headerRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 6,
                Margin = new Thickness(0, 4, 0, 0),
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                Padding = new Thickness(4, 2)
            };
            var arrow = new Label
            {
                Text = "▶",
                FontSize = 12,
                TextColor = Color.FromArgb("#1565C0"),
                VerticalOptions = LayoutOptions.Center
            };
            headerRow.Add(arrow, 0, 0);
            var titleLabel = new Label
            {
                Text = $"{title} ({group.Count})",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                VerticalOptions = LayoutOptions.Center
            };
            headerRow.Add(titleLabel, 1, 0);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) =>
            {
                folderContainer.IsVisible = !folderContainer.IsVisible;
                arrow.Text = folderContainer.IsVisible ? "▼" : "▶";
            };
            headerRow.GestureRecognizers.Add(tapGesture);

            if (_sortSets.Count > 0)
            {
                var assignBtn = new Button
                {
                    Text = "Assign all →",
                    FontSize = 11,
                    HeightRequest = 28,
                    Padding = new Thickness(8, 0),
                    BackgroundColor = Color.FromArgb("#1565C0"),
                    TextColor = Colors.White,
                    CornerRadius = 6
                };
                assignBtn.Clicked += async (_, _) =>
                {
                    string? setChoice = await DisplayActionSheet(
                        "Assign all to set", "Cancel", null,
                        new[] { "No Set" }.Concat(_sortSets.Select(set => set.Name)).ToArray());
                    if (setChoice == null || setChoice == "Cancel") return;
                    int? newSetId = setChoice == "No Set" ? null
                        : _sortSets.FirstOrDefault(set => set.Name == setChoice)?.Id;
                    var conn = await _db.GetConnectionAsync();
                    foreach (SortFolder folder in group)
                    {
                        folder.SetId = newSetId;
                        await conn.UpdateAsync(folder);
                    }
                    await LoadAsync();
                    RebuildManageList();
                };
                headerRow.Add(assignBtn, 2, 0);
            }

            _manageList.Children.Add(headerRow);

            for (int i = 0; i < group.Count; i++)
            {
                SortFolder folder = group[i];
                int capturedI = i;
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 4,
                    Margin = new Thickness(16, 0, 0, 0)
                };
                row.Add(new Label
                {
                    Text = folder.Name,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#263238"),
                    VerticalOptions = LayoutOptions.Center
                }, 0, 0);

                var upBtn = new Button
                {
                    Text = "▲", FontSize = 12, WidthRequest = 34, HeightRequest = 34,
                    Padding = 0, CornerRadius = 6,
                    BackgroundColor = Color.FromArgb("#ECEFF1"),
                    TextColor = Color.FromArgb("#37474F"),
                    IsEnabled = capturedI > 0
                };
                upBtn.Clicked += async (_, _) =>
                {
                    await SwapSortOrderAsync(group[capturedI], group[capturedI - 1]);
                    await LoadAsync(); RebuildManageList();
                };
                row.Add(upBtn, 1, 0);

                var downBtn = new Button
                {
                    Text = "▼", FontSize = 12, WidthRequest = 34, HeightRequest = 34,
                    Padding = 0, CornerRadius = 6,
                    BackgroundColor = Color.FromArgb("#ECEFF1"),
                    TextColor = Color.FromArgb("#37474F"),
                    IsEnabled = capturedI < group.Count - 1
                };
                downBtn.Clicked += async (_, _) =>
                {
                    await SwapSortOrderAsync(group[capturedI], group[capturedI + 1]);
                    await LoadAsync(); RebuildManageList();
                };
                row.Add(downBtn, 2, 0);

                var menuBtn = new Button
                {
                    Text = "⋮", FontSize = 14, WidthRequest = 34, HeightRequest = 34,
                    Padding = 0, CornerRadius = 6,
                    BackgroundColor = Color.FromArgb("#ECEFF1"),
                    TextColor = Color.FromArgb("#37474F")
                };
                menuBtn.Clicked += async (_, _) =>
                {
                    await EditSortFolderAsync(folder);
                    await LoadAsync(); RebuildManageList();
                };
                row.Add(menuBtn, 3, 0);
                folderContainer.Children.Add(row);
            }

            _manageList.Children.Add(folderContainer);
        }

        var unassigned = _sortFolders.Where(folder => folder.SetId == null)
            .OrderBy(folder => folder.SortOrder).ToList();
        AddCollapsibleGroup("No Set", null, unassigned);

        foreach (SortSet set in _sortSets)
        {
            var inSet = _sortFolders.Where(folder => folder.SetId == set.Id)
                .OrderBy(folder => folder.SortOrder).ToList();
            AddCollapsibleGroup(set.Name, set.Id, inSet);
        }
    }

    private async Task SwapSortOrderAsync(SortFolder a, SortFolder b)
    {
        int tmp = a.SortOrder;
        a.SortOrder = b.SortOrder;
        b.SortOrder = tmp;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(a);
        await conn.UpdateAsync(b);
    }

    private void RebuildFavRow()
    {
        _favRow.Children.Clear();

        var saveBtn = new Button
        {
            Text = "☆ Save",
            FontSize = 11,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#F9A825"),
            TextColor = Colors.White,
            CornerRadius = 16
        };
        saveBtn.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_sourceFolder))
            {
                await DisplayAlert("No folder", "Pick a source folder first.", "OK");
                return;
            }
            string defaultName = System.IO.Path.GetFileName(
                _sourceFolder.TrimEnd('\\', '/'));
            string? name = await DisplayPromptAsync(
                "Save Source Folder", "Name:", initialValue: defaultName);
            if (string.IsNullOrWhiteSpace(name)) return;
            var conn = await _db.GetConnectionAsync();
            await conn.InsertAsync(new FavoriteSourceFolder
            {
                Username = _auth.CurrentUsername,
                Name = name.Trim(),
                Path = _sourceFolder,
                SortOrder = _favFolders.Count
            });
            await LoadAsync();
        };
        _favRow.Children.Add(saveBtn);

        foreach (FavoriteSourceFolder fav in _favFolders)
        {
            var chip = new Button
            {
                Text = fav.Name,
                FontSize = 11,
                HeightRequest = 32,
                Padding = new Thickness(10, 0),
                BackgroundColor = Color.FromArgb("#FFF8E1"),
                TextColor = Color.FromArgb("#E65100"),
                CornerRadius = 16
            };
            chip.Clicked += async (_, _) =>
            {
                if (!System.IO.Directory.Exists(fav.Path))
                {
                    await DisplayAlert("Not found",
                        $"Folder no longer exists:\n{fav.Path}", "OK");
                    return;
                }
                _sourceFolder = fav.Path;
                var files = System.IO.Directory
                    .GetFiles(fav.Path)
                    .Where(file => SupportedExtensions.Contains(
                        System.IO.Path.GetExtension(file)))
                    .ToList();
                string? sortChoice = await DisplayActionSheet(
                    "Sort files by", "Cancel", null,
                    "Name A→Z",
                    "Name Z→A",
                    "Date newest first",
                    "Date oldest first");
                if (sortChoice == null || sortChoice == "Cancel") return;

                _files = sortChoice switch
                {
                    "Name A→Z"          => files.OrderBy(file => System.IO.Path.GetFileName(file)).ToList(),
                    "Name Z→A"          => files.OrderByDescending(file => System.IO.Path.GetFileName(file)).ToList(),
                    "Date newest first" => files.OrderByDescending(file => System.IO.File.GetLastWriteTime(file)).ToList(),
                    "Date oldest first" => files.OrderBy(file => System.IO.File.GetLastWriteTime(file)).ToList(),
                    _                   => files.OrderBy(file => file).ToList()
                };
                _currentIndex = 0;
                await ShowCurrentFileAsync();
            };
            _favRow.Children.Add(chip);

            var menuBtn = new Button
            {
                Text = "⋮",
                FontSize = 14,
                WidthRequest = 28,
                HeightRequest = 32,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#FFE082"),
                TextColor = Color.FromArgb("#E65100"),
                CornerRadius = 8
            };
            menuBtn.Clicked += async (_, _) =>
            {
                string? action = await DisplayActionSheet(
                    fav.Name, "Cancel", null, "Rename", "Remove");
                if (action == "Rename")
                {
                    string? newName = await DisplayPromptAsync(
                        "Rename", "New name:", initialValue: fav.Name);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    fav.Name = newName.Trim();
                    var conn = await _db.GetConnectionAsync();
                    await conn.UpdateAsync(fav);
                    await LoadAsync();
                }
                else if (action == "Remove")
                {
                    bool confirm = await DisplayAlert(
                        "Remove", $"Remove '{fav.Name}' from favorites?",
                        "Remove", "Cancel");
                    if (!confirm) return;
                    var conn = await _db.GetConnectionAsync();
                    await conn.DeleteAsync(fav);
                    await LoadAsync();
                }
            };
            _favRow.Children.Add(menuBtn);
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
            _setRow.Children.Add(chip);
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
