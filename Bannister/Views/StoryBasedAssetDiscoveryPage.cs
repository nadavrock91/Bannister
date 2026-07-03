using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Layouts;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class StoryBasedAssetDiscoveryPage : ContentPage
{
    private const string DefaultStageAPrompt = """
You're helping me find reusable visual assets for a video production. I have an asset library organized by category, and I'll be showing you the story I want to produce plus a list of available categories.

YOUR TASK: Decide how many asset filenames from each category you'd want to see to help me pick reusable assets. You don't get the filenames yet — just categories and how many items each category has. Based on the story below, output the number of samples you want per category. You can request 0 for categories that are clearly irrelevant to the story. Cap each request at 100. Request more from categories that seem highly relevant.

STORY:

{STORY}

AVAILABLE CATEGORIES (name: total item count):

{CATEGORIES}

Output ONLY a C# dictionary literal in this exact format, no explanation, no commentary, no code fences:

new Dictionary<string, int>
{
    { "category_name_1", 12 },
    { "category_name_2", 0 },
    { "category_name_3", 45 },
}

Include every category from the list above (even ones you want 0 from). Use the exact category names as shown.
""";

    private const string DefaultStageBPrompt = """
You're helping me find reusable visual assets for a video production. Below is the story and a randomly sampled subset of filenames from my asset library, grouped by category. The filenames themselves are the descriptive names — use them to judge relevance.

STORY:

{STORY}

SAMPLED ASSETS BY CATEGORY:

{SAMPLED_ASSETS}

YOUR TASK: Recommend which specific filenames from the sample above would be useful for this story. Pick filenames that clearly fit specific moments in the story. Skip filenames that are only vaguely related. Be selective — better to recommend 10 great matches than 40 mediocre ones.

Output ONLY a C# array literal in this exact format, no explanation, no commentary, no code fences:

new[]
{
    "filename_one.jpg",
    "filename_two.mp4",
    "filename_three.png",
}

Use only filenames from the sampled list above. Do not invent filenames.
""";

    private enum DiscoveryStage
    {
        PasteStory,
        PasteBudget,
        PasteRecommendations,
        Complete
    }

    private readonly AuthService _auth;
    private readonly AssetLibraryService _assetService;
    private readonly AssetThumbnailService _thumbnailService;

    private DiscoveryStage _stage = DiscoveryStage.PasteStory;
    private string _story = "";
    private Dictionary<string, int>? _categoryBudget;
    private Dictionary<string, List<AssetLibraryItem>>? _sampledPerCategory;
    private List<AssetLibraryItem>? _recommendedItems;
    private List<string> _unmatchedNames = new();
    private bool _isLoadingTemplates;

    private Editor _storyEditor = null!;
    private Editor _stageATemplateEditor = null!;
    private Editor _stageBTemplateEditor = null!;
    private Label _budgetSummaryLabel = null!;
    private Label _recommendationsSummaryLabel = null!;
    private Button _copyStageBButton = null!;
    private Button _pasteRecommendationsButton = null!;
    private VerticalStackLayout _gridContainer = null!;

    public StoryBasedAssetDiscoveryPage(AuthService auth, AssetLibraryService assetService, AssetThumbnailService thumbnailService)
    {
        _auth = auth;
        _assetService = assetService;
        _thumbnailService = thumbnailService;
        Title = "Story-Based Asset Discovery";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTemplatesAsync();
    }

    private void BuildUI()
    {
        _storyEditor = new Editor
        {
            Placeholder = "Paste your story or script here...",
            HeightRequest = 220,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            FontSize = 13
        };

        _stageATemplateEditor = CreateTemplateEditor(DefaultStageAPrompt);
        _stageBTemplateEditor = CreateTemplateEditor(DefaultStageBPrompt);

        _budgetSummaryLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#2E7D32"),
            IsVisible = false
        };

        _recommendationsSummaryLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#2E7D32"),
            IsVisible = false
        };

        _copyStageBButton = new Button
        {
            Text = "Sample and copy Stage B prompt to clipboard",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            IsEnabled = false
        };
        _copyStageBButton.Clicked += OnCopyStageBPromptClicked;

        _pasteRecommendationsButton = new Button
        {
            Text = "Paste recommendations response",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42,
            IsEnabled = false
        };
        _pasteRecommendationsButton.Clicked += async (_, _) => await ShowRecommendationsPasteModalAsync();

        _gridContainer = new VerticalStackLayout { Spacing = 10 };

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = " Story-Based Asset Discovery",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new Label
                {
                    Text = "Three-stage flow: paste your story, get a category budget from the LLM, sample assets per category, get final recommendations.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666")
                },
                BuildStageASection(),
                BuildStageBSection(),
                BuildStageCSection(),
                _gridContainer
            }
        };

        var root = new Grid();
        root.Children.Add(new ScrollView { Content = stack });
        Content = root;
    }

    private Frame BuildStageASection()
    {
        var copyButton = new Button
        {
            Text = " Copy Stage A prompt to clipboard",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };
        copyButton.Clicked += OnCopyStageAPromptClicked;

        var templateExpander = BuildTemplateExpander(
            "Show Stage A prompt template",
            _stageATemplateEditor,
            "story_discovery_stage_a_prompt",
            DefaultStageAPrompt);

        return BuildSectionFrame(new View[]
        {
            new Label { Text = "Step 1: Paste your story", FontSize = 16, FontAttributes = FontAttributes.Bold },
            _storyEditor,
            copyButton,
            templateExpander
        });
    }

    private Frame BuildStageBSection()
    {
        var pasteBudgetButton = new Button
        {
            Text = "Paste category budget response",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        pasteBudgetButton.Clicked += async (_, _) => await ShowBudgetPasteModalAsync();

        var templateExpander = BuildTemplateExpander(
            "Show Stage B prompt template",
            _stageBTemplateEditor,
            "story_discovery_stage_b_prompt",
            DefaultStageBPrompt);

        return BuildSectionFrame(new View[]
        {
            new Label { Text = "Step 2: Paste LLM budget response", FontSize = 16, FontAttributes = FontAttributes.Bold },
            pasteBudgetButton,
            _budgetSummaryLabel,
            _copyStageBButton,
            templateExpander
        });
    }

    private Frame BuildStageCSection()
    {
        return BuildSectionFrame(new View[]
        {
            new Label { Text = "Step 3: Paste LLM recommendation response", FontSize = 16, FontAttributes = FontAttributes.Bold },
            _pasteRecommendationsButton,
            _recommendationsSummaryLabel
        });
    }

    private static Frame BuildSectionFrame(IEnumerable<View> children)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 10
        };

        foreach (var child in children)
            stack.Children.Add(child);

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 14,
            HasShadow = false,
            Content = stack
        };
    }

    private static Editor CreateTemplateEditor(string defaultText) => new()
    {
        Text = defaultText,
        HeightRequest = 220,
        AutoSize = EditorAutoSizeOption.TextChanges,
        BackgroundColor = Color.FromArgb("#FAFAFA"),
        TextColor = Color.FromArgb("#222"),
        FontSize = 12
    };

    private View BuildTemplateExpander(string title, Editor editor, string keyPrefix, string defaultPrompt)
    {
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
                editor,
                new Label
                {
                    Text = "Use the placeholders shown in the default prompt.",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#666")
                }
            }
        };

        editor.TextChanged += async (_, _) =>
        {
            if (_isLoadingTemplates)
                return;

            try
            {
                var key = GetTemplateKey(keyPrefix);
                var value = editor.Text ?? "";
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
            var reset = await DisplayAlert(
                "Reset to default?",
                "This will discard your custom prompt and restore the built-in default.",
                "Reset",
                "Cancel");
            if (!reset)
                return;

            try { SecureStorage.Remove(GetTemplateKey(keyPrefix)); } catch { }
            editor.Text = defaultPrompt;
        };
        container.Children.Add(resetButton);

        var toggle = new Button
        {
            Text = title,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };

        toggle.Clicked += (_, _) =>
        {
            container.IsVisible = !container.IsVisible;
            toggle.Text = container.IsVisible ? title.Replace("Show", "Hide") : title;
        };

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children = { toggle, container }
        };
    }

    private async Task LoadTemplatesAsync()
    {
        _isLoadingTemplates = true;
        try
        {
            _stageATemplateEditor.Text = await GetTemplateAsync("story_discovery_stage_a_prompt") ?? DefaultStageAPrompt;
            _stageBTemplateEditor.Text = await GetTemplateAsync("story_discovery_stage_b_prompt") ?? DefaultStageBPrompt;
        }
        finally
        {
            _isLoadingTemplates = false;
        }
    }

    private async Task<string?> GetTemplateAsync(string keyPrefix)
    {
        try
        {
            var value = await SecureStorage.GetAsync(GetTemplateKey(keyPrefix));
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private string GetTemplateKey(string keyPrefix) => $"{keyPrefix}_{_auth.CurrentUsername}";

    private async void OnCopyStageAPromptClicked(object? sender, EventArgs e)
    {
        var story = _storyEditor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(story))
        {
            await DisplayAlert("No story", "Paste a story first.", "OK");
            return;
        }

        var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
        var usable = allItems.Where(i => i.MissingSince == null).ToList();

        if (usable.Count == 0)
        {
            await DisplayAlert("Empty library", "The asset library is empty. Rescan a folder first.", "OK");
            return;
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in usable)
        {
            var cats = AssetLibraryService.ParseCategories(item);
            if (cats.Count == 0)
            {
                counts["uncategorized"] = counts.GetValueOrDefault("uncategorized", 0) + 1;
            }
            else
            {
                foreach (var category in cats)
                    counts[category] = counts.GetValueOrDefault(category, 0) + 1;
            }
        }

        var categoriesBlock = string.Join("\n", counts
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}: {kv.Value} items"));

        var template = _stageATemplateEditor.Text ?? DefaultStageAPrompt;
        var prompt = template
            .Replace("{STORY}", story, StringComparison.Ordinal)
            .Replace("{CATEGORIES}", categoriesBlock, StringComparison.Ordinal);

        if (prompt.Length > 100000)
        {
            var ok = await DisplayAlert("Large prompt", $"Prompt is {prompt.Length:N0} chars. Continue?", "Copy", "Cancel");
            if (!ok)
                return;
        }

        _story = story;
        _stage = DiscoveryStage.PasteBudget;
        await Clipboard.SetTextAsync(prompt);
        await FlashButtonAsync(sender);
    }

    private async void OnCopyStageBPromptClicked(object? sender, EventArgs e)
    {
        if (_categoryBudget == null || _categoryBudget.Count == 0)
        {
            await DisplayAlert("No budget", "Parse the Stage A budget response first.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_story))
        {
            await DisplayAlert("No story", "Story text was lost. Re-paste the story and repeat Stage A.", "OK");
            return;
        }

        var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
        var usable = allItems.Where(i => i.MissingSince == null).ToList();

        var byCategory = new Dictionary<string, List<AssetLibraryItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in usable)
        {
            var cats = AssetLibraryService.ParseCategories(item);
            if (cats.Count == 0)
            {
                if (!byCategory.ContainsKey("uncategorized"))
                    byCategory["uncategorized"] = new List<AssetLibraryItem>();
                byCategory["uncategorized"].Add(item);
            }
            else
            {
                foreach (var category in cats)
                {
                    if (!byCategory.ContainsKey(category))
                        byCategory[category] = new List<AssetLibraryItem>();
                    byCategory[category].Add(item);
                }
            }
        }

        var rng = new Random();
        _sampledPerCategory = new Dictionary<string, List<AssetLibraryItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _categoryBudget)
        {
            var wanted = Math.Min(100, kv.Value);
            if (wanted <= 0)
                continue;
            if (!byCategory.TryGetValue(kv.Key, out var pool))
                continue;

            _sampledPerCategory[kv.Key] = pool.Count <= wanted
                ? new List<AssetLibraryItem>(pool)
                : pool.OrderBy(_ => rng.Next()).Take(wanted).ToList();
        }

        var sb = new System.Text.StringBuilder();
        foreach (var kv in _sampledPerCategory.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"=== {kv.Key} ({kv.Value.Count}) ===");
            foreach (var item in kv.Value.OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(item.FileName);
            sb.AppendLine();
        }

        var template = _stageBTemplateEditor.Text ?? DefaultStageBPrompt;
        var prompt = template
            .Replace("{STORY}", _story, StringComparison.Ordinal)
            .Replace("{SAMPLED_ASSETS}", sb.ToString(), StringComparison.Ordinal);

        if (prompt.Length > 100000)
        {
            var ok = await DisplayAlert("Large prompt", $"Prompt is {prompt.Length:N0} chars. Continue?", "Copy", "Cancel");
            if (!ok)
                return;
        }

        _stage = DiscoveryStage.PasteRecommendations;
        _pasteRecommendationsButton.IsEnabled = true;
        await Clipboard.SetTextAsync(prompt);
        await FlashButtonAsync(sender);
    }

    private async Task ShowBudgetPasteModalAsync()
    {
        await ShowParseModalAsync(
            "Paste category budget response",
            "Paste the LLM's C# dictionary literal here.",
            "Parse budget",
            async text =>
            {
                var parsed = ParseStageAResponse(text);
                if (parsed == null)
                {
                    await DisplayAlert("Could not parse", "Paste a C# dictionary literal with category names and integer counts.", "OK");
                    return false;
                }

                _categoryBudget = parsed;
                _stage = DiscoveryStage.PasteBudget;
                var total = parsed.Sum(kv => kv.Value);
                _budgetSummaryLabel.Text = $"Budget parsed: {parsed.Count} categories, {total} total samples.";
                _budgetSummaryLabel.IsVisible = true;
                _copyStageBButton.IsEnabled = true;
                return true;
            });
    }

    private async Task ShowRecommendationsPasteModalAsync()
    {
        await ShowParseModalAsync(
            "Paste recommendations response",
            "Paste the LLM's C# array literal here.",
            "Parse recommendations",
            async text =>
            {
                var parsed = ParseStageBResponse(text);
                if (parsed == null)
                {
                    await DisplayAlert("Could not parse", "Paste a C# array literal of filenames.", "OK");
                    return false;
                }

                var allItems = await _assetService.GetAllAsync(_auth.CurrentUsername);
                var byFile = allItems
                    .Where(i => i.MissingSince == null)
                    .GroupBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var matched = new List<AssetLibraryItem>();
                _unmatchedNames = new List<string>();
                foreach (var name in parsed.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (byFile.TryGetValue(name, out var item))
                        matched.Add(item);
                    else
                        _unmatchedNames.Add(name);
                }

                _recommendedItems = matched;
                _stage = DiscoveryStage.Complete;
                _recommendationsSummaryLabel.Text = $"Recommendations parsed: {matched.Count} files matched, {_unmatchedNames.Count} not found.";
                _recommendationsSummaryLabel.IsVisible = true;
                RenderGrid();
                return true;
            });
    }

    private async Task ShowParseModalAsync(string title, string description, string parseButtonText, Func<string, Task<bool>> parseAsync)
    {
        if (Content is not Grid root)
            return;

        var modalEditor = new Editor
        {
            Placeholder = "Paste response here...",
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

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42
        };

        var parseButton = new Button
        {
            Text = parseButtonText,
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
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
        buttonsRow.Add(parseButton, 1, 0);

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
                    new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold },
                    new Label { Text = description, FontSize = 12, TextColor = Color.FromArgb("#666") },
                    modalEditor,
                    statusLabel,
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

        parseButton.Clicked += async (_, _) =>
        {
            var text = modalEditor.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return;

            var ok = await parseAsync(text);
            if (!ok)
                return;

            statusLabel.Text = "Parsed successfully.";
            statusLabel.IsVisible = true;
            parseButton.IsVisible = false;
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

    private static Dictionary<string, int>? ParseStageAResponse(string raw)
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
        var pattern = new Regex("\\{\\s*\"([^\"]+)\"\\s*,\\s*(\\d+)\\s*\\}", RegexOptions.Compiled);

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in pattern.Matches(body))
        {
            var name = match.Groups[1].Value.Trim();
            if (int.TryParse(match.Groups[2].Value, out var count))
                result[name] = Math.Min(100, Math.Max(0, count));
        }

        return result.Count == 0 ? null : result;
    }

    private static List<string>? ParseStageBResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var arrStart = raw.IndexOf("new[]", StringComparison.OrdinalIgnoreCase);
        if (arrStart < 0)
            arrStart = raw.IndexOf("new []", StringComparison.OrdinalIgnoreCase);
        if (arrStart < 0)
            arrStart = raw.IndexOf("new string[]", StringComparison.OrdinalIgnoreCase);

        var braceStart = arrStart >= 0 ? raw.IndexOf('{', arrStart) : raw.IndexOf('{');
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
        var pattern = new Regex("\"([^\"]+)\"");
        var result = new List<string>();
        foreach (Match match in pattern.Matches(body))
        {
            var name = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(name))
                result.Add(name);
        }

        return result.Count == 0 ? null : result;
    }

    private void RenderGrid()
    {
        _gridContainer.Children.Clear();
        if (_recommendedItems == null || _recommendedItems.Count == 0)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "No matches to display.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic,
                TextColor = Color.FromArgb("#777")
            });
            return;
        }

        var flex = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start,
            AlignContent = FlexAlignContent.Start
        };

        foreach (var item in _recommendedItems)
            flex.Children.Add(BuildGridCell(item));

        _gridContainer.Children.Add(flex);

        if (_unmatchedNames.Count > 0)
        {
            var preview = string.Join(", ", _unmatchedNames.Take(10));
            var suffix = _unmatchedNames.Count > 10 ? $"... and {_unmatchedNames.Count - 10} more." : "";
            _gridContainer.Children.Add(new Label
            {
                Text = $"{_unmatchedNames.Count} recommendation(s) not found in library: {preview}{suffix}",
                FontSize = 11,
                TextColor = Color.FromArgb("#C62828"),
                Margin = new Thickness(0, 12, 0, 0)
            });
        }
    }

    private View BuildGridCell(AssetLibraryItem item)
    {
        var thumbSlot = new Grid
        {
            WidthRequest = 120,
            HeightRequest = 120,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        thumbSlot.Children.Add(new Label
        {
            Text = item.FileType == "video" ? "🎞️" : "🖼️",
            FontSize = 36,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        });

        if (item.FileType == "image" && item.MissingSince == null)
        {
            var thumbPath = _thumbnailService.GetThumbnailPath(item.Id, item.FilePath);
            if (!string.IsNullOrWhiteSpace(thumbPath))
            {
                thumbSlot.Children.Add(new Image
                {
                    Source = ImageSource.FromFile(thumbPath),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = 120,
                    HeightRequest = 120
                });
            }
        }

        var categoriesList = AssetLibraryService.ParseCategories(item);
        var categoryText = categoriesList.Count == 0
            ? "(uncategorized)"
            : string.Join(", ", categoriesList.Take(3));

        var cell = new VerticalStackLayout
        {
            Spacing = 4,
            WidthRequest = 140,
            Padding = new Thickness(6),
            Children =
            {
                new Frame
                {
                    Padding = 0,
                    CornerRadius = 8,
                    BackgroundColor = Colors.Transparent,
                    BorderColor = Color.FromArgb("#E0E0E0"),
                    IsClippedToBounds = true,
                    HasShadow = false,
                    Content = thumbSlot
                },
                new Label
                {
                    Text = item.FileName,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                },
                new Label
                {
                    Text = categoryText,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#666"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                }
            }
        };

        cell.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenAssetAsync(item))
        });

        return cell;
    }

    private async Task FlashButtonAsync(object? sender)
    {
        if (sender is not Button btn)
            return;

        var original = btn.Text;
        btn.Text = "Copied!";
        await Task.Delay(1000);
        btn.Text = original;
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
}
