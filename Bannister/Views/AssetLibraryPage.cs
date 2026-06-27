using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AssetLibraryPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly AssetLibraryService _assetService;

    private Label _rootFolderLabel = null!;
    private Label _lastScanLabel = null!;
    private Label _filesCountLabel = null!;
    private Label _progressLabel = null!;
    private Picker _categoryFilterPicker = null!;
    private VerticalStackLayout _listStack = null!;
    private Grid _bulkBar = null!;
    private Label _selectionCountLabel = null!;
    private Button _rescanButton = null!;

    private readonly HashSet<int> _selectedIds = new();
    private List<AssetLibraryItem> _currentItems = new();
    private List<string> _currentCategories = new();
    private string _selectedCategoryFilter = "All";
    private bool _isLoading;

    public AssetLibraryPage(AuthService auth, AssetLibraryService assetService)
    {
        _auth = auth;
        _assetService = assetService;
        Title = "Asset Library";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        stack.Children.Add(new Label
        {
            Text = "📁 Asset Library",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });

        stack.Children.Add(new Label
        {
            Text = "Index image and video files from a parent folder for reuse across productions.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });

        _rootFolderLabel = new Label { FontSize = 12, TextColor = Color.FromArgb("#555"), LineBreakMode = LineBreakMode.WordWrap };
        _lastScanLabel = new Label { FontSize = 11, TextColor = Color.FromArgb("#777") };
        _progressLabel = new Label { FontSize = 11, TextColor = Color.FromArgb("#00838F"), IsVisible = false };
        _filesCountLabel = new Label { FontSize = 12, TextColor = Color.FromArgb("#555") };

        var changeFolderButton = new Button
        {
            Text = "Change folder",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 40
        };
        changeFolderButton.Clicked += OnChangeFolderClicked;

        _rescanButton = new Button
        {
            Text = "Rescan",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40
        };
        _rescanButton.Clicked += OnRescanClicked;

        stack.Children.Add(new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Parent folder:", FontSize = 13, FontAttributes = FontAttributes.Bold },
                    _rootFolderLabel,
                    new HorizontalStackLayout { Spacing = 10, Children = { changeFolderButton, _rescanButton } },
                    _lastScanLabel,
                    _progressLabel,
                    _filesCountLabel
                }
            }
        });

        _categoryFilterPicker = new Picker
        {
            Title = "Category",
            HorizontalOptions = LayoutOptions.Fill
        };
        _categoryFilterPicker.SelectedIndexChanged += async (_, _) =>
        {
            if (_isLoading) return;
            _selectedCategoryFilter = _categoryFilterPicker.SelectedItem?.ToString() ?? "All";
            await LoadAsync();
        };

        stack.Children.Add(new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "Filter by category:", FontSize = 13, FontAttributes = FontAttributes.Bold },
                    _categoryFilterPicker
                }
            }
        });

        _selectionCountLabel = new Label
        {
            TextColor = Color.FromArgb("#263238"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        var categorizeSelectedButton = new Button
        {
            Text = "Categorize selected",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        categorizeSelectedButton.Clicked += async (_, _) => await CategorizeSelectedAsync();

        var cancelSelectionButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8
        };
        cancelSelectionButton.Clicked += (_, _) =>
        {
            _selectedIds.Clear();
            RenderList();
        };

        _bulkBar = new Grid
        {
            IsVisible = false,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Padding = 10,
            BackgroundColor = Color.FromArgb("#E0F2F1")
        };
        _bulkBar.Add(_selectionCountLabel, 0, 0);
        _bulkBar.Add(categorizeSelectedButton, 1, 0);
        _bulkBar.Add(cancelSelectionButton, 2, 0);
        stack.Children.Add(_bulkBar);

        _listStack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(_listStack);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var root = await GetRootFolderAsync();
            _rootFolderLabel.Text = string.IsNullOrWhiteSpace(root) ? "(not set)" : root;
            _rescanButton.IsEnabled = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);

            var lastScan = await GetLastScanAsync();
            if (lastScan == null)
            {
                _lastScanLabel.Text = "Never scanned";
            }
            else
            {
                int days = Math.Max(0, (int)(DateTime.UtcNow.Date - lastScan.Value.Date).TotalDays);
                _lastScanLabel.Text = days == 0 ? "Last scanned: today" : $"Last scanned: {days} days ago";
            }

            var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
            _currentCategories = await _assetService.GetDistinctCategoriesAsync(_auth.CurrentUsername);

            string previousFilter = _selectedCategoryFilter;
            _categoryFilterPicker.Items.Clear();
            _categoryFilterPicker.Items.Add("All");
            _categoryFilterPicker.Items.Add("Uncategorized");
            foreach (var category in _currentCategories)
                _categoryFilterPicker.Items.Add(category);

            int selectedIndex = _categoryFilterPicker.Items.IndexOf(previousFilter);
            if (selectedIndex < 0)
            {
                previousFilter = "All";
                selectedIndex = 0;
            }

            _selectedCategoryFilter = previousFilter;
            _categoryFilterPicker.SelectedIndex = selectedIndex;

            _currentItems = ApplyFilter(allItems);

            int uncategorizedCount = allItems.Count(i => string.IsNullOrWhiteSpace(i.Category));
            _filesCountLabel.Text = $"{allItems.Count} files indexed, {uncategorizedCount} uncategorized";

            RenderList();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private List<AssetLibraryItem> ApplyFilter(List<AssetLibraryItem> items)
    {
        if (_selectedCategoryFilter == "Uncategorized")
            return items.Where(i => string.IsNullOrWhiteSpace(i.Category)).ToList();

        if (_selectedCategoryFilter != "All")
            return items.Where(i => string.Equals(i.Category, _selectedCategoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        return items;
    }

    private void RenderList()
    {
        _listStack.Children.Clear();
        _bulkBar.IsVisible = _selectedIds.Count > 0;
        _selectionCountLabel.Text = $"{_selectedIds.Count} selected";

        if (_currentItems.Count == 0)
        {
            _listStack.Children.Add(new Label
            {
                Text = "(no assets in this filter)",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic,
                TextColor = Color.FromArgb("#777")
            });
            return;
        }

        var itemsToRender = _currentItems.Take(500).ToList();
        if (_currentItems.Count > 500)
        {
            _listStack.Children.Add(new Label
            {
                Text = $"Showing 500 of {_currentItems.Count}. Use category filter to narrow.",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic,
                TextColor = Color.FromArgb("#C62828")
            });
        }

        foreach (var item in itemsToRender)
            _listStack.Children.Add(BuildAssetRow(item));
    }

    private View BuildAssetRow(AssetLibraryItem item)
    {
        var checkBox = new CheckBox
        {
            IsChecked = _selectedIds.Contains(item.Id),
            VerticalOptions = LayoutOptions.Center
        };
        checkBox.CheckedChanged += (_, e) =>
        {
            if (e.Value)
                _selectedIds.Add(item.Id);
            else
                _selectedIds.Remove(item.Id);

            _bulkBar.IsVisible = _selectedIds.Count > 0;
            _selectionCountLabel.Text = $"{_selectedIds.Count} selected";
        };

        var name = string.IsNullOrWhiteSpace(item.DescriptiveName) ? item.FileName : item.DescriptiveName;
        var category = string.IsNullOrWhiteSpace(item.Category) ? "(uncategorized)" : item.Category;
        var categoryBadge = new Frame
        {
            BackgroundColor = string.IsNullOrWhiteSpace(item.Category) ? Color.FromArgb("#ECEFF1") : Color.FromArgb("#E0F2F1"),
            BorderColor = Colors.Transparent,
            Padding = new Thickness(8, 3),
            CornerRadius = 8,
            Content = new Label
            {
                Text = category,
                FontSize = 11,
                TextColor = string.IsNullOrWhiteSpace(item.Category) ? Color.FromArgb("#777") : Color.FromArgb("#00695C"),
                FontAttributes = string.IsNullOrWhiteSpace(item.Category) ? FontAttributes.Italic : FontAttributes.None
            }
        };

        var badges = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                categoryBadge,
                new Label
                {
                    Text = $"{(item.FileType == "image" ? "🖼️ image" : "🎞️ video")} • {FormatBytes(item.FileSizeBytes)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#666"),
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

        if (item.MissingSince != null)
        {
            badges.Children.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#FFCDD2"),
                BorderColor = Colors.Transparent,
                Padding = new Thickness(8, 3),
                CornerRadius = 8,
                Content = new Label
                {
                    Text = "MISSING",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#B71C1C")
                }
            });
        }

        var openButton = new Button
        {
            Text = "Open",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 36
        };
        openButton.Clicked += async (_, _) => await OpenAssetAsync(item);

        var menuButton = new Button
        {
            Text = "⋮",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#333"),
            FontSize = 20,
            WidthRequest = 44,
            HeightRequest = 36
        };
        menuButton.Clicked += async (_, _) => await ShowAssetMenuAsync(item);

        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        contentGrid.Add(checkBox, 0, 0);
        contentGrid.Add(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label
                {
                    Text = name,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222"),
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                badges,
                new Label
                {
                    Text = item.FilePath,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#999"),
                    LineBreakMode = LineBreakMode.MiddleTruncation
                }
            }
        }, 1, 0);
        contentGrid.Add(openButton, 2, 0);
        contentGrid.Add(menuButton, 3, 0);

        return new Frame
        {
            BackgroundColor = item.MissingSince == null ? Colors.White : Color.FromArgb("#FFEBEE"),
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 12,
            HasShadow = false,
            Content = contentGrid
        };
    }

    private async void OnChangeFolderClicked(object? sender, EventArgs e)
    {
        var current = await GetRootFolderAsync() ?? "";
        var path = await DisplayPromptAsync(
            "Parent folder",
            "Paste the full path to the parent folder:",
            initialValue: current,
            placeholder: @"C:\assets");

        if (string.IsNullOrWhiteSpace(path))
            return;

        path = path.Trim().Trim('"');
        if (!Directory.Exists(path))
        {
            await DisplayAlert("Invalid folder", "That folder does not exist.", "OK");
            return;
        }

        await SetRootFolderAsync(path);
        await LoadAsync();
    }

    private async void OnRescanClicked(object? sender, EventArgs e)
    {
        var root = await GetRootFolderAsync();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            await DisplayAlert("Folder needed", "Set a valid parent folder first.", "OK");
            return;
        }

        _rescanButton.IsEnabled = false;
        _progressLabel.IsVisible = true;
        _progressLabel.Text = "Scanning...";

        try
        {
            var progress = new Progress<AssetLibraryScanProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _progressLabel.Text = $"Scanning... {p.FilesIndexed:N0} indexed ({p.FilesScanned:N0} scanned)";
                });
            });

            var result = await _assetService.RescanAsync(_auth.CurrentUsername, root, progress);
            await SetLastScanAsync(DateTime.UtcNow);
            await DisplayAlert(
                "Scan complete",
                $"{result.NewlyIndexed} new, {result.AlreadyIndexed} already indexed, {result.MarkedMissing} marked missing.",
                "OK");
        }
        finally
        {
            _progressLabel.IsVisible = false;
            _rescanButton.IsEnabled = true;
            await LoadAsync();
        }
    }

    private async Task ShowAssetMenuAsync(AssetLibraryItem item)
    {
        var choice = await DisplayActionSheet(
            item.FileName,
            "Cancel",
            null,
            "Edit name",
            "Change category",
            "Delete from library");

        if (choice == "Edit name")
            await EditNameAsync(item);
        else if (choice == "Change category")
            await ChangeCategoryAsync(new[] { item.Id });
        else if (choice == "Delete from library")
            await DeleteFromLibraryAsync(item);
    }

    private async Task EditNameAsync(AssetLibraryItem item)
    {
        var name = await DisplayPromptAsync(
            "Edit name",
            "Descriptive name. Leave empty to use filename:",
            initialValue: item.DescriptiveName ?? "");

        if (name == null)
            return;

        await _assetService.SetDescriptiveNameAsync(item.Id, name);
        await LoadAsync();
    }

    private async Task CategorizeSelectedAsync()
    {
        if (_selectedIds.Count == 0)
            return;

        await ChangeCategoryAsync(_selectedIds.ToList());
        _selectedIds.Clear();
        await LoadAsync();
    }

    private async Task ChangeCategoryAsync(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        var options = new List<string>();
        options.AddRange(_currentCategories);
        options.Add("New category...");
        options.Add("Clear category");

        var choice = await DisplayActionSheet("Change category", "Cancel", null, options.ToArray());
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
            return;

        string category;
        if (choice == "New category...")
        {
            var newCategory = await DisplayPromptAsync("New category", "Category name:");
            if (string.IsNullOrWhiteSpace(newCategory))
                return;
            category = newCategory.Trim();
        }
        else if (choice == "Clear category")
        {
            category = "";
        }
        else
        {
            category = choice;
        }

        if (idList.Count == 1)
            await _assetService.SetCategoryAsync(idList[0], category);
        else
            await _assetService.SetCategoryBulkAsync(idList, category);

        await LoadAsync();
    }

    private async Task DeleteFromLibraryAsync(AssetLibraryItem item)
    {
        var confirm = await DisplayAlert(
            "Remove from library?",
            "The file on disk is not deleted.",
            "Remove",
            "Cancel");
        if (!confirm)
            return;

        await _assetService.DeleteAsync(item.Id);
        _selectedIds.Remove(item.Id);
        await LoadAsync();
    }

    private async Task OpenAssetAsync(AssetLibraryItem item)
    {
        try
        {
            await Launcher.OpenAsync(new OpenFileRequest("Open file", new ReadOnlyFile(item.FilePath)));
        }
        catch
        {
            await DisplayAlert("Could not open file", "The file could not be opened.", "OK");
        }
    }

    private async Task<string?> GetRootFolderAsync()
    {
        try { return await SecureStorage.GetAsync(GetRootKey()); }
        catch { return null; }
    }

    private async Task SetRootFolderAsync(string path)
    {
        try { await SecureStorage.SetAsync(GetRootKey(), path); }
        catch { }
    }

    private async Task<DateTime?> GetLastScanAsync()
    {
        try
        {
            var value = await SecureStorage.GetAsync(GetLastScanKey());
            if (DateTime.TryParse(value, out var parsed))
                return parsed;
        }
        catch { }

        return null;
    }

    private async Task SetLastScanAsync(DateTime when)
    {
        try { await SecureStorage.SetAsync(GetLastScanKey(), when.ToString("O")); }
        catch { }
    }

    private string GetRootKey() => $"asset_library_root_{_auth.CurrentUsername}";

    private string GetLastScanKey() => $"asset_library_last_scan_{_auth.CurrentUsername}";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
