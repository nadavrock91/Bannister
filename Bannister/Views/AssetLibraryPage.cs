using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AssetLibraryPage : ContentPage
{
    private const string DefaultLookupPrompt = """
You're helping me find reusable visual assets for a video production. Below is what I'm working on (a script or description) and the available asset library inventory.

WHAT I'M WORKING ON:

{SCRIPT}

ASSET LIBRARY:

{ASSETS}

Suggest assets from the library that could plausibly be reused for this content. The asset filename itself is the descriptive name — use it to judge relevance. Prefer specificity over generic matches. Skip suggestions for content where no asset is a good fit.

Output format:

For each relevant moment or line in the source content:
- {filename} ({category}) — {one-sentence reason}

If multiple assets fit the same moment, list them grouped. If no asset fits any part of the content, say so plainly. Be honest about poor matches — better to suggest nothing than to suggest things that don't really fit.
""";

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
    private Editor _scriptEditor = null!;
    private Editor _lookupTemplateEditor = null!;
    private VerticalStackLayout _lookupSectionContent = null!;
    private VerticalStackLayout _lookupTemplateContainer = null!;
    private Button _lookupSectionToggle = null!;
    private Button _lookupTemplateToggle = null!;

    private readonly HashSet<int> _selectedIds = new();
    private List<AssetLibraryItem> _currentItems = new();
    private List<string> _currentCategories = new();
    private string _selectedCategoryFilter = "All";
    private bool _isLoading;
    private bool _isLoadingLookupTemplate;

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
        await LoadLookupTemplateAsync();
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

        stack.Children.Add(BuildLookupSection());

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

        var rootGrid = new Grid();
        rootGrid.Children.Add(new ScrollView { Content = stack });
        Content = rootGrid;
    }

    private Frame BuildLookupSection()
    {
        _scriptEditor = new Editor
        {
            Placeholder = "Paste your script or description here...",
            HeightRequest = 200,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            FontSize = 13
        };

        _lookupTemplateEditor = new Editor
        {
            HeightRequest = 220,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            FontSize = 12,
            Text = DefaultLookupPrompt
        };
        _lookupTemplateEditor.TextChanged += async (_, _) =>
        {
            if (_isLoadingLookupTemplate)
                return;

            try
            {
                var value = _lookupTemplateEditor.Text ?? "";
                if (string.IsNullOrWhiteSpace(value))
                    SecureStorage.Remove(GetLookupTemplateKey());
                else
                    await SecureStorage.SetAsync(GetLookupTemplateKey(), value);
            }
            catch
            {
            }
        };

        var copyButton = new Button
        {
            Text = " Copy lookup prompt to clipboard",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };
        copyButton.Clicked += OnCopyLookupPromptClicked;

        var pasteResponseButton = new Button
        {
            Text = "Paste LLM response",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        pasteResponseButton.Clicked += async (_, _) => await ShowLookupResponseModalAsync();

        _lookupTemplateToggle = new Button
        {
            Text = "Show prompt template",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };

        var resetTemplateButton = new Button
        {
            Text = "Reset to default",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Start
        };
        resetTemplateButton.Clicked += async (_, _) =>
        {
            var reset = await DisplayAlert(
                "Reset to default?",
                "This will discard your custom prompt and restore the built-in default.",
                "Reset",
                "Cancel");

            if (!reset)
                return;

            try
            {
                SecureStorage.Remove(GetLookupTemplateKey());
            }
            catch
            {
            }

            _lookupTemplateEditor.Text = DefaultLookupPrompt;
        };

        _lookupTemplateContainer = new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Prompt template (edit to customize):",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333")
                },
                _lookupTemplateEditor,
                new Label
                {
                    Text = "Use {SCRIPT} and {ASSETS} as placeholders for the pasted content and full asset inventory.",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#666")
                },
                resetTemplateButton
            }
        };

        _lookupTemplateToggle.Clicked += (_, _) =>
        {
            _lookupTemplateContainer.IsVisible = !_lookupTemplateContainer.IsVisible;
            _lookupTemplateToggle.Text = _lookupTemplateContainer.IsVisible
                ? "Hide prompt template"
                : "Show prompt template";
        };

        var buttonsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        buttonsGrid.Add(copyButton, 0, 0);
        buttonsGrid.Add(pasteResponseButton, 1, 0);

        _lookupSectionContent = new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Paste a script or description of what you need. The page will assemble a prompt combining your text with the entire asset library inventory. Copy the prompt to ChatGPT, then paste the response below to view suggestions.",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#666")
                },
                _scriptEditor,
                buttonsGrid,
                _lookupTemplateToggle,
                _lookupTemplateContainer
            }
        };

        _lookupSectionToggle = new Button
        {
            Text = " Find reusable assets (LLM lookup)",
            BackgroundColor = Color.FromArgb("#E0F2F1"),
            TextColor = Color.FromArgb("#00695C"),
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        _lookupSectionToggle.Clicked += (_, _) =>
        {
            _lookupSectionContent.IsVisible = !_lookupSectionContent.IsVisible;
            _lookupSectionToggle.Text = _lookupSectionContent.IsVisible
                ? " Find reusable assets (hide)"
                : " Find reusable assets (LLM lookup)";
        };

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#B2DFDB"),
            CornerRadius = 10,
            Padding = 14,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    _lookupSectionToggle,
                    _lookupSectionContent
                }
            }
        };
    }

    private async Task LoadLookupTemplateAsync()
    {
        if (_lookupTemplateEditor == null)
            return;

        _isLoadingLookupTemplate = true;
        try
        {
            var saved = await SecureStorage.GetAsync(GetLookupTemplateKey());
            _lookupTemplateEditor.Text = string.IsNullOrWhiteSpace(saved) ? DefaultLookupPrompt : saved;
        }
        catch
        {
            _lookupTemplateEditor.Text = DefaultLookupPrompt;
        }
        finally
        {
            _isLoadingLookupTemplate = false;
        }
    }

    private string GetLookupTemplateKey() => $"asset_library_lookup_prompt_custom_{_auth.CurrentUsername}";

    private async void OnCopyLookupPromptClicked(object? sender, EventArgs e)
    {
        var script = _scriptEditor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(script))
        {
            await DisplayAlert("No content", "Paste a script or description first.", "OK");
            return;
        }

        var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
        var usable = allItems.Where(i => i.MissingSince == null).ToList();

        if (usable.Count == 0)
        {
            await DisplayAlert("Empty library", "The asset library has no usable items. Rescan a folder first.", "OK");
            return;
        }

        var assetsBlock = BuildAssetsBlock(usable);
        var template = _lookupTemplateEditor.Text ?? DefaultLookupPrompt;
        var prompt = template
            .Replace("{SCRIPT}", script, StringComparison.Ordinal)
            .Replace("{ASSETS}", assetsBlock, StringComparison.Ordinal);

        if (prompt.Length > 100000)
        {
            var proceed = await DisplayAlert(
                "Large prompt",
                $"The combined script and library is {prompt.Length:N0} characters. It may not paste cleanly into ChatGPT. Continue anyway?",
                "Copy",
                "Cancel");

            if (!proceed)
                return;
        }

        await Clipboard.SetTextAsync(prompt);
        if (sender is Button btn)
        {
            var originalText = btn.Text;
            btn.Text = "Copied!";
            await Task.Delay(1000);
            btn.Text = originalText;
        }
    }

    private static string BuildAssetsBlock(List<AssetLibraryItem> items)
    {
        var sorted = items
            .OrderBy(i => string.IsNullOrWhiteSpace(i.Category) ? "zzz_uncategorized" : i.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new System.Text.StringBuilder();
        foreach (var item in sorted)
        {
            var category = string.IsNullOrWhiteSpace(item.Category) ? "uncategorized" : item.Category;
            sb.AppendLine($"{item.FileName} | {category} | {item.FileType}");
        }

        return sb.ToString();
    }

    private async Task ShowLookupResponseModalAsync()
    {
        if (Content is not Grid root)
            return;

        var modalEditor = new Editor
        {
            Placeholder = "Paste the LLM's response here...",
            HeightRequest = 360,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            FontSize = 13
        };

        var responseLabel = new Label
        {
            Text = "",
            FontSize = 13,
            IsVisible = false,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var responseScroll = new ScrollView
        {
            HeightRequest = 280,
            IsVisible = false,
            Content = responseLabel
        };

        var applyButton = new Button
        {
            Text = "Show response",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42
        };

        var buttonsRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        buttonsRow.Add(closeButton, 0, 0);
        buttonsRow.Add(applyButton, 1, 0);

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            CornerRadius = 12,
            WidthRequest = 560,
            HasShadow = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Paste LLM response",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Paste the response from ChatGPT and tap Show response.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    },
                    modalEditor,
                    responseScroll,
                    buttonsRow
                }
            }
        };

        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false,
            Children = { card }
        };

        var tcs = new TaskCompletionSource<bool>();

        applyButton.Clicked += (_, _) =>
        {
            var text = modalEditor.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return;

            responseLabel.Text = text;
            responseLabel.IsVisible = true;
            responseScroll.IsVisible = true;
            modalEditor.IsVisible = false;
            applyButton.IsVisible = false;
            closeButton.Text = "Done";
        };

        closeButton.Clicked += (_, _) =>
        {
            root.Children.Remove(overlay);
            tcs.TrySetResult(true);
        };

        root.Children.Add(overlay);
        await tcs.Task;
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
