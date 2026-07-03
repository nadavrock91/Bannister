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

    private const string DefaultBulkCategorizePrompt = """
You're categorizing image and video filenames from an asset library. Below are:
- The existing categories in the library (prefer these when they fit)
- A list of filenames that need categories assigned

YOUR TASK: For each filename, assign one or more category names. The filename itself is the descriptive name — use it to judge what category the asset belongs to. Prefer existing categories when they fit; suggest new ones only when nothing existing applies. Multiple categories per file are fine (e.g. a video of a dragon flying might get both dragons and flying_creatures). Use lowercase snake_case for any new categories you invent.

EXISTING CATEGORIES:

{CATEGORIES}

FILENAMES TO CATEGORIZE:

{FILENAMES}

Output ONLY a C# dictionary literal in this exact format, no explanation, no commentary, no code fences:

new Dictionary<string, string[]>
{
    { "filename_one.jpg", new[] { "category_a", "category_b" } },
    { "filename_two.mp4", new[] { "category_c" } },
    { "filename_three.png", new[] { "category_a" } },
}

Include every filename from the list above. Each filename gets at least one category.
""";

    private readonly AuthService _auth;
    private readonly AssetLibraryService _assetService;
    private readonly AssetThumbnailService _thumbnailService;

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
    private Editor _bulkCategorizeTemplateEditor = null!;

    private readonly HashSet<int> _selectedIds = new();
    private List<AssetLibraryItem> _currentItems = new();
    private List<string> _currentCategories = new();
    private string _selectedCategoryFilter = "All";
    private bool _isLoading;
    private bool _isLoadingLookupTemplate;
    private bool _isLoadingBulkCategorizeTemplate;

    public AssetLibraryPage(AuthService auth, AssetLibraryService assetService, AssetThumbnailService thumbnailService)
    {
        _auth = auth;
        _assetService = assetService;
        _thumbnailService = thumbnailService;
        Title = "Asset Library";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        await LoadLookupTemplateAsync();
        await LoadBulkCategorizeTemplateAsync();
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
        stack.Children.Add(BuildStoryDiscoveryButton());
        stack.Children.Add(BuildBulkCategorizeButton());
        stack.Children.Add(BuildBulkCategorizeTemplateExpander());

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
            Text = "Add category to selected",
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

    private Button BuildStoryDiscoveryButton()
    {
        var button = new Button
        {
            Text = " Story-based asset discovery →",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#F57C00"),
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };

        button.Clicked += async (_, _) =>
            await Navigation.PushAsync(new StoryBasedAssetDiscoveryPage(_auth, _assetService, _thumbnailService));

        return button;
    }

    private Button BuildBulkCategorizeButton()
    {
        var button = new Button
        {
            Text = " Auto-categorize uncategorized",
            BackgroundColor = Color.FromArgb("#F3E5F5"),
            TextColor = Color.FromArgb("#6A1B9A"),
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };

        button.Clicked += async (_, _) => await ShowBulkCategorizeModalAsync();
        return button;
    }

    private VerticalStackLayout BuildBulkCategorizeTemplateExpander()
    {
        _bulkCategorizeTemplateEditor = new Editor
        {
            HeightRequest = 220,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            FontSize = 12,
            Text = DefaultBulkCategorizePrompt
        };
        _bulkCategorizeTemplateEditor.TextChanged += async (_, _) =>
        {
            if (_isLoadingBulkCategorizeTemplate)
                return;

            try
            {
                var key = GetBulkCategorizeTemplateKey();
                var value = _bulkCategorizeTemplateEditor.Text ?? "";
                if (string.IsNullOrWhiteSpace(value))
                    SecureStorage.Remove(key);
                else
                    await SecureStorage.SetAsync(key, value);
            }
            catch { }
        };

        var resetButton = new Button
        {
            Text = "Reset to default",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Start
        };
        resetButton.Clicked += async (_, _) =>
        {
            var reset = await DisplayAlert("Reset to default?", "Discard your custom prompt and restore the default.", "Reset", "Cancel");
            if (!reset)
                return;

            try { SecureStorage.Remove(GetBulkCategorizeTemplateKey()); } catch { }
            _bulkCategorizeTemplateEditor.Text = DefaultBulkCategorizePrompt;
        };

        var container = new VerticalStackLayout
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
                _bulkCategorizeTemplateEditor,
                new Label
                {
                    Text = "Use {CATEGORIES} and {FILENAMES} as placeholders.",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#666")
                },
                resetButton
            }
        };

        var toggle = new Button
        {
            Text = "Show bulk-categorize prompt template",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        toggle.Clicked += (_, _) =>
        {
            container.IsVisible = !container.IsVisible;
            toggle.Text = container.IsVisible
                ? "Hide bulk-categorize prompt template"
                : "Show bulk-categorize prompt template";
        };

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children = { toggle, container }
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

    private string GetBulkCategorizeTemplateKey() => $"bulk_categorize_prompt_{_auth.CurrentUsername}";

    private async Task LoadBulkCategorizeTemplateAsync()
    {
        if (_bulkCategorizeTemplateEditor == null)
            return;

        _isLoadingBulkCategorizeTemplate = true;
        try
        {
            var saved = await SecureStorage.GetAsync(GetBulkCategorizeTemplateKey());
            _bulkCategorizeTemplateEditor.Text = string.IsNullOrWhiteSpace(saved) ? DefaultBulkCategorizePrompt : saved;
        }
        catch
        {
            _bulkCategorizeTemplateEditor.Text = DefaultBulkCategorizePrompt;
        }
        finally
        {
            _isLoadingBulkCategorizeTemplate = false;
        }
    }

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
        var enriched = items
            .Select(i =>
            {
                var cats = AssetLibraryService.ParseCategories(i);
                return new
                {
                    Item = i,
                    Cats = cats,
                    SortKey = cats.FirstOrDefault() ?? "zzz_uncategorized"
                };
            })
            .OrderBy(x => x.SortKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new System.Text.StringBuilder();
        foreach (var x in enriched)
        {
            var category = x.Cats.Count == 0 ? "uncategorized" : string.Join(", ", x.Cats);
            sb.AppendLine($"{x.Item.FileName} | {category} | {x.Item.FileType}");
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

    private async void OnCopyBulkCategorizePromptClicked(object? sender, EventArgs e)
    {
        var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
        var uncategorized = allItems
            .Where(i => i.MissingSince == null && AssetLibraryService.ParseCategories(i).Count == 0)
            .ToList();

        if (uncategorized.Count == 0)
        {
            await DisplayAlert("Nothing to categorize", "No uncategorized items in the library.", "OK");
            return;
        }

        const int cap = 500;
        var batch = uncategorized.Take(cap).ToList();
        var truncated = uncategorized.Count > cap;

        var existingCategories = await _assetService.GetDistinctCategoriesAsync(_auth.CurrentUsername);
        var categoriesBlock = existingCategories.Count == 0
            ? "(none yet — invent categories as needed)"
            : string.Join(", ", existingCategories);

        var filenamesBlock = string.Join("\n", batch.Select(i => i.FileName));

        var template = _bulkCategorizeTemplateEditor.Text ?? DefaultBulkCategorizePrompt;
        var prompt = template
            .Replace("{CATEGORIES}", categoriesBlock, StringComparison.Ordinal)
            .Replace("{FILENAMES}", filenamesBlock, StringComparison.Ordinal);

        if (prompt.Length > 100000)
        {
            var ok = await DisplayAlert(
                "Large prompt",
                $"Prompt is {prompt.Length:N0} chars. Continue?",
                "Copy",
                "Cancel");
            if (!ok)
                return;
        }

        await Clipboard.SetTextAsync(prompt);

        if (truncated)
        {
            await DisplayAlert(
                "Batch limit reached",
                $"{batch.Count} of {uncategorized.Count} uncategorized filenames included. Apply this batch, then run again to categorize the next {Math.Min(cap, uncategorized.Count - batch.Count)}.",
                "OK");
        }

        if (sender is Button btn)
        {
            var original = btn.Text;
            btn.Text = "Copied!";
            await Task.Delay(1000);
            btn.Text = original;
        }
    }

    private static Dictionary<string, List<string>>? ParseBulkCategorizeResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var dictStart = raw.IndexOf("new Dictionary", StringComparison.OrdinalIgnoreCase);
        var braceStart = dictStart >= 0 ? raw.IndexOf('{', dictStart) : raw.IndexOf('{');
        if (braceStart < 0)
            return null;

        int depth = 0;
        int braceEnd = -1;
        for (int i = braceStart; i < raw.Length; i++)
        {
            if (raw[i] == '{')
                depth++;
            else if (raw[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    braceEnd = i;
                    break;
                }
            }
        }
        if (braceEnd < 0)
            return null;

        var body = raw.Substring(braceStart + 1, braceEnd - braceStart - 1);
        var pattern = new System.Text.RegularExpressions.Regex(
            "\\{\\s*\"([^\"]+)\"\\s*,\\s*new(?:\\s+string)?\\s*\\[\\s*\\]\\s*\\{([^}]*)\\}\\s*\\}",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        var stringLit = new System.Text.RegularExpressions.Regex("\"([^\"]+)\"");

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in pattern.Matches(body))
        {
            var filename = match.Groups[1].Value.Trim();
            var innerBody = match.Groups[2].Value;
            var categories = new List<string>();
            foreach (System.Text.RegularExpressions.Match stringMatch in stringLit.Matches(innerBody))
            {
                var name = stringMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    categories.Add(name);
            }

            if (categories.Count > 0)
                result[filename] = categories;
        }

        return result.Count == 0 ? null : result;
    }

    private async Task<(int applied, int notFound, int noCategories)> ApplyBulkCategorizationAsync(Dictionary<string, List<string>> parsed)
    {
        var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
        var byFile = allItems
            .Where(i => i.MissingSince == null)
            .GroupBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int applied = 0;
        int notFound = 0;
        int noCategories = 0;

        foreach (var kv in parsed)
        {
            if (!byFile.TryGetValue(kv.Key, out var item))
            {
                notFound++;
                continue;
            }

            var cats = kv.Value
                .Select(c => c?.Trim() ?? "")
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cats.Count == 0)
            {
                noCategories++;
                continue;
            }

            var existing = AssetLibraryService.ParseCategories(item);
            var merged = existing.Concat(cats).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            await _assetService.SetCategoriesAsync(item.Id, merged);
            applied++;
        }

        return (applied, notFound, noCategories);
    }

    private async Task ShowBulkCategorizeModalAsync()
    {
        if (Content is not Grid root)
            return;

        var copyButton = new Button
        {
            Text = "Copy prompt to clipboard",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };
        copyButton.Clicked += OnCopyBulkCategorizePromptClicked;

        var pasteEditor = new Editor
        {
            Placeholder = "Paste the LLM's C# dictionary response here...",
            HeightRequest = 300,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            FontSize = 13
        };

        var statusLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#2E7D32"),
            IsVisible = false
        };

        var applyButton = new Button
        {
            Text = "Apply categorization",
            BackgroundColor = Color.FromArgb("#2E7D32"),
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

        var actionsRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        actionsRow.Add(closeButton, 0, 0);
        actionsRow.Add(applyButton, 1, 0);

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            CornerRadius = 12,
            WidthRequest = 560,
            MaximumHeightRequest = 700,
            HasShadow = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "Auto-categorize uncategorized",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Send filenames of uncategorized items + your existing categories to the LLM. Paste the returned C# dictionary and tap Apply to categorize directly.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    },
                    copyButton,
                    pasteEditor,
                    statusLabel,
                    actionsRow
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

        applyButton.Clicked += async (_, _) =>
        {
            var text = pasteEditor.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                await DisplayAlert("Empty", "Paste the LLM response first.", "OK");
                return;
            }

            var parsed = ParseBulkCategorizeResponse(text);
            if (parsed == null || parsed.Count == 0)
            {
                await DisplayAlert("Could not parse", "Paste a C# dictionary literal mapping filenames to string[] of categories.", "OK");
                return;
            }

            var (applied, notFound, noCategories) = await ApplyBulkCategorizationAsync(parsed);
            statusLabel.Text = $"Applied categories to {applied} file(s). Not found: {notFound}. Skipped (no categories): {noCategories}.";
            statusLabel.IsVisible = true;
            applyButton.IsVisible = false;
            closeButton.Text = "Done";

            await LoadAsync();
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

            int uncategorizedCount = allItems.Count(i => AssetLibraryService.ParseCategories(i).Count == 0);
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
            return items.Where(i => AssetLibraryService.ParseCategories(i).Count == 0).ToList();

        if (_selectedCategoryFilter != "All")
            return items.Where(i => AssetLibraryService.ParseCategories(i).Contains(_selectedCategoryFilter, StringComparer.OrdinalIgnoreCase)).ToList();

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
        var categoriesList = AssetLibraryService.ParseCategories(item);

        var badges = new HorizontalStackLayout { Spacing = 4 };
        if (categoriesList.Count == 0)
        {
            badges.Children.Add(BuildCategoryBadge("(uncategorized)", true));
        }
        else
        {
            foreach (var category in categoriesList.Take(3))
                badges.Children.Add(BuildCategoryBadge(category, false));

            if (categoriesList.Count > 3)
            {
                badges.Children.Add(new Label
                {
                    Text = $"+{categoriesList.Count - 3}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#666"),
                    VerticalOptions = LayoutOptions.Center
                });
            }
        }

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

        var middleColumn = new VerticalStackLayout
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
                    Text = $"{(item.FileType == "image" ? "🖼️ image" : "🎞️ video")} • {FormatBytes(item.FileSizeBytes)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#666")
                },
                new Label
                {
                    Text = item.FilePath,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#999"),
                    LineBreakMode = LineBreakMode.MiddleTruncation
                }
            }
        };

        middleColumn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenCategoryModalAsync(item))
        });

        var rightActions = new HorizontalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { openButton, menuButton }
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };

        contentGrid.Add(checkBox, 0, 0);
        contentGrid.Add(BuildThumbnail(item), 1, 0);
        contentGrid.Add(middleColumn, 2, 0);
        contentGrid.Add(rightActions, 3, 0);

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

    private View BuildCategoryBadge(string text, bool uncategorized)
    {
        return new Frame
        {
            BackgroundColor = uncategorized ? Color.FromArgb("#ECEFF1") : Color.FromArgb("#E0F2F1"),
            BorderColor = Colors.Transparent,
            Padding = new Thickness(8, 3),
            CornerRadius = 8,
            Content = new Label
            {
                Text = text,
                FontSize = 11,
                TextColor = uncategorized ? Color.FromArgb("#777") : Color.FromArgb("#00695C"),
                FontAttributes = uncategorized ? FontAttributes.Italic : FontAttributes.None
            }
        };
    }

    private View BuildThumbnail(AssetLibraryItem item)
    {
        var slot = new Grid
        {
            WidthRequest = 80,
            HeightRequest = 80,
            BackgroundColor = item.MissingSince != null ? Color.FromArgb("#EEEEEE") : Color.FromArgb("#F5F5F5")
        };

        slot.Children.Add(new Label
        {
            Text = item.MissingSince != null ? "❓" : item.FileType == "video" ? "🎞️" : "🖼️",
            FontSize = 28,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        });

        if (item.FileType == "image" && item.MissingSince == null)
        {
            var thumbPath = _thumbnailService.GetThumbnailPath(item.Id, item.FilePath);
            if (!string.IsNullOrWhiteSpace(thumbPath))
            {
                slot.Children.Add(new Image
                {
                    Source = ImageSource.FromFile(thumbPath),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = 80,
                    HeightRequest = 80
                });
            }
        }

        var thumbFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 8,
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            IsClippedToBounds = true,
            Content = slot
        };

        thumbFrame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenAssetAsync(item))
        });

        return thumbFrame;
    }

    private async Task OpenCategoryModalAsync(AssetLibraryItem item)
    {
        if (Content is not Grid root)
            return;

        var allCategories = await _assetService.GetDistinctCategoriesAsync(_auth.CurrentUsername);
        var currentCategories = AssetLibraryService.ParseCategories(item);

        var state = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in allCategories)
            state[category] = currentCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
        foreach (var category in currentCategories)
            if (!state.ContainsKey(category))
                state[category] = true;

        var searchEntry = new Entry
        {
            Placeholder = "Filter categories...",
            FontSize = 13
        };

        var categoryListStack = new VerticalStackLayout { Spacing = 4 };

        void RenderCategoryList()
        {
            categoryListStack.Children.Clear();
            var filter = searchEntry.Text?.Trim() ?? "";
            var keys = state.Keys
                .Where(k => string.IsNullOrWhiteSpace(filter) || k.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (keys.Count == 0)
            {
                categoryListStack.Children.Add(new Label
                {
                    Text = "No categories match.",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#999")
                });
                return;
            }

            foreach (var key in keys)
            {
                var localKey = key;
                var checkBox = new CheckBox
                {
                    IsChecked = state[localKey],
                    VerticalOptions = LayoutOptions.Center
                };
                checkBox.CheckedChanged += (_, e) => state[localKey] = e.Value;

                categoryListStack.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        checkBox,
                        new Label
                        {
                            Text = localKey,
                            FontSize = 13,
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                });
            }
        }

        searchEntry.TextChanged += (_, _) => RenderCategoryList();

        var newCategoryEntry = new Entry
        {
            Placeholder = "New category name",
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        };

        var addNewButton = new Button
        {
            Text = "Add",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 36,
            WidthRequest = 80
        };
        addNewButton.Clicked += (_, _) =>
        {
            var name = newCategoryEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return;

            state[name] = true;
            newCategoryEntry.Text = "";
            RenderCategoryList();
        };

        var addRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };
        addRow.Add(newCategoryEntry, 0, 0);
        addRow.Add(addNewButton, 1, 0);

        var cancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42
        };

        var updateButton = new Button
        {
            Text = "Update",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };

        var actionsRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        actionsRow.Add(cancelButton, 0, 0);
        actionsRow.Add(updateButton, 1, 0);

        RenderCategoryList();

        var displayName = string.IsNullOrWhiteSpace(item.DescriptiveName) ? item.FileName : item.DescriptiveName;
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 18,
            CornerRadius = 12,
            WidthRequest = 480,
            MaximumHeightRequest = 640,
            HasShadow = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = displayName,
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        LineBreakMode = LineBreakMode.TailTruncation
                    },
                    new Label
                    {
                        Text = "Tick the categories this asset belongs to. Untick to remove.",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#666")
                    },
                    searchEntry,
                    new ScrollView
                    {
                        HeightRequest = 320,
                        Content = categoryListStack
                    },
                    addRow,
                    actionsRow
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

        updateButton.Clicked += async (_, _) =>
        {
            var checkedCategories = state
                .Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            await _assetService.SetCategoriesAsync(item.Id, checkedCategories);
            root.Children.Remove(overlay);
            tcs.TrySetResult(true);
            await LoadAsync();
        };

        cancelButton.Clicked += (_, _) =>
        {
            root.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        root.Children.Add(overlay);
        await tcs.Task;
    }

    private View BuildAssetRowLegacy(AssetLibraryItem item)
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
        // Category management moved to the middle-column tap modal.
        var choice = await DisplayActionSheet(
            item.FileName,
            "Cancel",
            null,
            "Edit name",
            "Delete from library");

        if (choice == "Edit name")
            await EditNameAsync(item);
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

        await AddCategoryToSelectedAsync(_selectedIds.ToList());
        _selectedIds.Clear();
        await LoadAsync();
    }

    private async Task AddCategoryToSelectedAsync(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        var options = new List<string>();
        options.AddRange(_currentCategories);
        options.Add("New category...");

        var choice = await DisplayActionSheet("Add category to selected", "Cancel", null, options.ToArray());
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
        else
        {
            category = choice;
        }

        await _assetService.AddCategoryBulkAsync(idList, category);
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
