using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class OpeningClipPromptPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomPromptService _customPrompts;
    private readonly DoNotService _doNotService;
    private ClipPromptTemplateService? _templateService;

    private const string Area = "OpeningClipPrompts";
    private const string FoundationStorageKey_Prefix = "opening_clip_foundation_";
    private const string DefaultFoundation = "foe + movement";

    // Stage 1 — core foundation
    private Editor _foundationEditor = null!;

    // Stage 2 — do-nots
    private VerticalStackLayout _doNotContainer = null!;
    private List<DoNotItem> _doNotItems = new();

    // Stage 3 — build & copy
    private Button _buildButton = null!;
    private Label _outputStatusLabel = null!;
    private VerticalStackLayout _templateList = null!;
    private Entry _templateNameEntry = null!;
    private Editor _templateBodyEditor = null!;
    private Editor _templateExtraDoNotsEditor = null!;
    private Button _templateSaveButton = null!;
    private int? _editingTemplateId;
    private List<ClipPromptTemplate> _templates = new();
    private VerticalStackLayout _templatePickerContainer = null!;

    // Stage 4 — paste response
    private Button _pasteResponseButton = null!;

    // Stage 5 — parsed prompts
    private VerticalStackLayout _promptsContainer = null!;

    private readonly List<string> _parsedPrompts = new();
    private readonly List<string> _parsedTitles = new();
    private readonly List<string> _parsedHooks = new();

    public OpeningClipPromptPage(
        AuthService auth,
        CustomPromptService customPrompts,
        DoNotService doNotService,
        ClipPromptTemplateService? templateService = null)
    {
        _auth = auth;
        _customPrompts = customPrompts;
        _doNotService = doNotService;
        _templateService = templateService;
        Title = "Opening Clip Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFoundationAsync();
        await RefreshDoNotListAsync();
        await RefreshTemplateListAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 16 };

        stack.Children.Add(new Label
        {
            Text = " Opening Clip Prompts",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Bannister builds a retention-optimised prompt for 5 Grok " +
                   "video variations. Copy to clipboard, attach the image in " +
                   "Grok, paste and run.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        stack.Children.Add(BuildStageCard(
            "Stage 1 — Core Foundation", BuildStage1Content()));
        stack.Children.Add(BuildStageCard(
            "Stage 2 — Do Nots", BuildStage2Content()));
        stack.Children.Add(BuildStageCard(
            "Stage 3 — Build & Copy Prompt", BuildStage3Content()));
        stack.Children.Add(BuildStageCard(
            "Stage 4 — Paste LLM Response", BuildStage4Content()));
        stack.Children.Add(BuildStageCard(
            "Stage 5 — Generated Prompts", BuildStage5Content()));

        stack.Children.Insert(0, BuildStageCard(
            "Stage 0 — Prompt Templates", BuildStage0Content()));

        Content = new ScrollView { Content = stack };
    }

    private View BuildStage0Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Children.Add(new Label
        {
            Text = "Save reusable prompt bodies and any extra exclusions.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        _templateList = new VerticalStackLayout { Spacing = 6 };
        v.Children.Add(_templateList);

        _templateNameEntry = new Entry
        {
            Placeholder = "Template name",
            FontSize = 13,
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };
        _templateBodyEditor = new Editor
        {
            Placeholder = "Prompt body",
            HeightRequest = 180,
            AutoSize = EditorAutoSizeOption.Disabled,
            FontSize = 12,
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };
        _templateExtraDoNotsEditor = new Editor
        {
            Placeholder = "Additional do-nots for this template...",
            HeightRequest = 90,
            AutoSize = EditorAutoSizeOption.Disabled,
            FontSize = 12,
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };
        _templateSaveButton = new Button
        {
            Text = "Save Template",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 38,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(14, 0)
        };
        _templateSaveButton.Clicked += async (_, _) =>
            await SaveTemplateFormAsync();
        v.Children.Add(_templateNameEntry);
        v.Children.Add(_templateBodyEditor);
        v.Children.Add(_templateExtraDoNotsEditor);
        v.Children.Add(_templateSaveButton);
        return v;
    }

    private static Frame BuildStageCard(string title, View content)
    {
        var inner = new VerticalStackLayout { Spacing = 10 };
        inner.Children.Add(new Label
        {
            Text = title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });
        inner.Children.Add(content);
        return new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true,
            Content = inner
        };
    }

    // ── Stage 1: Foundation ───────────────────────────────────────────
    private async Task RefreshTemplateListAsync()
    {
        _templateService ??= Handler?.MauiContext?.Services
            .GetService<ClipPromptTemplateService>();
        if (_templateService == null || _templateList == null) return;

        _templates = await _templateService.GetAllTemplatesAsync();
        _templateList.Children.Clear();
        _templateList.Children.Add(new Label
        {
            Text = "Default (built-in)",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        foreach (var template in _templates)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 6
            };
            row.Add(new Label
            {
                Text = template.Name,
                FontSize = 13,
                TextColor = Color.FromArgb("#444"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            var edit = new Button
            {
                Text = "Edit",
                FontSize = 11,
                HeightRequest = 32,
                Padding = new Thickness(8, 0),
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0")
            };
            edit.Clicked += (_, _) =>
            {
                _editingTemplateId = template.Id;
                _templateNameEntry.Text = template.Name;
                _templateBodyEditor.Text = template.PromptBody;
                _templateExtraDoNotsEditor.Text = template.ExtraDoNots;
                _templateSaveButton.Text = "Update Template";
            };
            row.Add(edit, 1, 0);
            var delete = new Button
            {
                Text = "Delete",
                FontSize = 11,
                HeightRequest = 32,
                Padding = new Thickness(8, 0),
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828")
            };
            delete.Clicked += async (_, _) =>
            {
                if (!await DisplayAlert("Delete Template",
                    $"Delete \"{template.Name}\"?", "Delete", "Cancel"))
                    return;
                await _templateService.DeleteTemplateAsync(template.Id);
                await RefreshTemplateListAsync();
            };
            row.Add(delete, 2, 0);
            _templateList.Children.Add(row);
        }
    }

    private async Task SaveTemplateFormAsync()
    {
        if (_templateService == null ||
            string.IsNullOrWhiteSpace(_templateNameEntry.Text) ||
            string.IsNullOrWhiteSpace(_templateBodyEditor.Text))
            return;

        var template = new ClipPromptTemplate
        {
            Id = _editingTemplateId ?? 0,
            Name = _templateNameEntry.Text.Trim(),
            PromptBody = _templateBodyEditor.Text.Trim(),
            ExtraDoNots = _templateExtraDoNotsEditor.Text?.Trim() ?? ""
        };
        await _templateService.SaveTemplateAsync(template);
        _editingTemplateId = null;
        _templateNameEntry.Text = "";
        _templateBodyEditor.Text = "";
        _templateExtraDoNotsEditor.Text = "";
        _templateSaveButton.Text = "Save Template";
        await RefreshTemplateListAsync();
    }

    private View BuildStage1Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Children.Add(new Label
        {
            Text = "The current retention hypothesis to keep constant across " +
                   "variations (e.g. \"foe + movement\"). Edit per experiment.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        _foundationEditor = new Editor
        {
            HeightRequest = 60,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            FontSize = 13,
            Placeholder = "e.g. foe + movement",
            PlaceholderColor = Color.FromArgb("#999")
        };
        _foundationEditor.TextChanged += async (_, e) =>
            await SaveFoundationAsync(e.NewTextValue ?? "");
        v.Children.Add(_foundationEditor);

        var resetBtn = new Button
        {
            Text = "↺ Reset to Default",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(12, 0)
        };
        resetBtn.Clicked += async (_, _) =>
        {
            _foundationEditor.Text = DefaultFoundation;
            await SaveFoundationAsync(DefaultFoundation);
        };
        v.Children.Add(resetBtn);
        return v;
    }

    // ── Stage 2: Do Nots ──────────────────────────────────────────────
    private View BuildStage2Content()
    {
        var v = new VerticalStackLayout { Spacing = 10 };
        v.Children.Add(new Label
        {
            Text = "Things you do NOT want the LLM to generate. " +
                   "These are injected into the prompt automatically.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        _doNotContainer = new VerticalStackLayout { Spacing = 6 };
        v.Children.Add(_doNotContainer);

        var addBtn = new Button
        {
            Text = "+ Add Do Not",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 38,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(14, 0)
        };
        addBtn.Clicked += async (_, _) => await AddDoNotAsync();
        v.Children.Add(addBtn);

        return v;
    }

    // ── Stage 3: Build & Copy ─────────────────────────────────────────
    private View BuildStage3Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Children.Add(new Label
        {
            Text = "Builds the full prompt with your foundation and do-nots " +
                   "injected. Attach the starting frame image in Grok/Claude " +
                   "after pasting.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        _templatePickerContainer = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = false
        };
        v.Children.Add(_templatePickerContainer);
        _buildButton = new Button
        {
            Text = " Build & Copy Prompt to Clipboard",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        _buildButton.Clicked += async (_, _) => await BuildAndCopyPromptAsync();
        v.Children.Add(_buildButton);
        _outputStatusLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#2E7D32"),
            IsVisible = false,
            LineBreakMode = LineBreakMode.WordWrap
        };
        v.Children.Add(_outputStatusLabel);
        return v;
    }

    // ── Stage 4: Paste Response ───────────────────────────────────────
    private View BuildStage4Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Children.Add(new Label
        {
            Text = "After running the prompt in Grok/Claude with the image " +
                   "attached, paste the response here to parse the 5 clip prompts.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        _pasteResponseButton = new Button
        {
            Text = " Paste LLM Response",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44
        };
        _pasteResponseButton.Clicked += async (_, _) =>
            await PasteLLMResponseAsync();
        v.Children.Add(_pasteResponseButton);
        return v;
    }

    // ── Stage 5: Parsed Prompts ───────────────────────────────────────
    private View BuildStage5Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Children.Add(new Label
        {
            Text = "Parsed prompts appear here. Skim the titles, " +
                   "tap Copy to send each to Grok.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        _promptsContainer = new VerticalStackLayout { Spacing = 12 };
        v.Children.Add(_promptsContainer);
        return v;
    }

    // ─────────────────────────────────────────────────────────────────
    // DO NOT LOGIC
    // ─────────────────────────────────────────────────────────────────
    private async Task RefreshDoNotListAsync()
    {
        _doNotItems = await _doNotService.GetItemsAsync(
            _auth.CurrentUsername, Area);
        RenderDoNotList();
    }

    private void RenderDoNotList()
    {
        _doNotContainer.Children.Clear();

        if (_doNotItems.Count == 0)
        {
            _doNotContainer.Children.Add(new Label
            {
                Text = "No exclusions added yet.",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        foreach (var item in _doNotItems)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(36)),
                    new ColumnDefinition(new GridLength(36))
                },
                ColumnSpacing = 6
            };

            row.Add(new Label
            {
                Text = item.Text,
                FontSize = 13,
                TextColor = Color.FromArgb("#222"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var editBtn = new Button
            {
                Text = "✏",
                FontSize = 13,
                HeightRequest = 34,
                WidthRequest = 34,
                CornerRadius = 6,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0")
            };
            var capturedItem = item;
            editBtn.Clicked += async (_, _) =>
                await EditDoNotAsync(capturedItem);
            row.Add(editBtn, 1, 0);

            var delBtn = new Button
            {
                Text = "✕",
                FontSize = 13,
                HeightRequest = 34,
                WidthRequest = 34,
                CornerRadius = 6,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828")
            };
            delBtn.Clicked += async (_, _) =>
                await DeleteDoNotAsync(capturedItem);
            row.Add(delBtn, 2, 0);

            _doNotContainer.Children.Add(row);
        }
    }

    private async Task AddDoNotAsync()
    {
        string? text = await DisplayPromptAsync(
            "Add Do Not",
            "What should the LLM avoid generating?",
            "Add", "Cancel",
            placeholder: "e.g. eyes coming out of the floor");
        if (string.IsNullOrWhiteSpace(text)) return;
        await _doNotService.AddItemAsync(
            _auth.CurrentUsername, Area, text.Trim());
        await RefreshDoNotListAsync();
    }

    private async Task EditDoNotAsync(DoNotItem item)
    {
        string? text = await DisplayPromptAsync(
            "Edit Do Not",
            "Edit this exclusion:",
            "Save", "Cancel",
            initialValue: item.Text);
        if (string.IsNullOrWhiteSpace(text)) return;
        item.Text = text.Trim();
        await _doNotService.UpdateItemAsync(item);
        await RefreshDoNotListAsync();
    }

    private async Task DeleteDoNotAsync(DoNotItem item)
    {
        bool confirm = await DisplayAlert(
            "Remove", $"Remove \"{item.Text}\"?", "Remove", "Cancel");
        if (!confirm) return;
        await _doNotService.DeleteItemAsync(item.Id);
        await RefreshDoNotListAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // BUILD PROMPT
    // ─────────────────────────────────────────────────────────────────
    private async Task BuildAndCopyPromptAsync()
    {
        var foundation = (_foundationEditor.Text ?? DefaultFoundation).Trim();
        if (string.IsNullOrWhiteSpace(foundation))
            foundation = DefaultFoundation;

        var doNots = _doNotItems.Select(i => i.Text).ToList();
        await RefreshTemplateListAsync();
        var selectedIndex = await ShowTemplatePickerAsync();
        if (!selectedIndex.HasValue) return;

        var prompt = selectedIndex.Value == 0
            ? BuildPrompt(foundation, doNots)
            : BuildCustomPrompt(_templates[selectedIndex.Value - 1], doNots);
        await Clipboard.SetTextAsync(prompt);

        _outputStatusLabel.Text =
            "✓ Prompt copied. Open Grok/Claude, attach the starting frame, " +
            "paste the prompt and run. Then paste the response in Stage 4.";
        _outputStatusLabel.IsVisible = true;

        var original = _buildButton.Text;
        _buildButton.Text = "✓ Copied!";
        await Task.Delay(1800);
        _buildButton.Text = original;
    }

    private async Task<int?> ShowTemplatePickerAsync()
    {
        _templatePickerContainer.Children.Clear();
        _templatePickerContainer.Children.Add(new Label
        {
            Text = "Choose a prompt template:",
            FontSize = 12,
            TextColor = Color.FromArgb("#555")
        });

        var tcs = new TaskCompletionSource<int?>();
        void Select(int? index)
        {
            _templatePickerContainer.IsVisible = false;
            tcs.TrySetResult(index);
        }

        var defaultButton = new Button
        {
            Text = "Default (built-in)",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 7,
            HeightRequest = 38
        };
        defaultButton.Clicked += (_, _) => Select(0);
        _templatePickerContainer.Children.Add(defaultButton);

        for (int i = 0; i < _templates.Count; i++)
        {
            int capturedIndex = i + 1;
            var templateButton = new Button
            {
                Text = _templates[i].Name,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                CornerRadius = 7,
                HeightRequest = 38
            };
            templateButton.Clicked += (_, _) => Select(capturedIndex);
            _templatePickerContainer.Children.Add(templateButton);
        }

        var cancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#888"),
            HeightRequest = 32
        };
        cancelButton.Clicked += (_, _) => Select(null);
        _templatePickerContainer.Children.Add(cancelButton);
        _templatePickerContainer.IsVisible = true;
        return await tcs.Task;
    }

    private static string BuildCustomPrompt(
        ClipPromptTemplate template,
        List<string> persistentDoNots)
    {
        var exclusions = persistentDoNots
            .Concat((template.ExtraDoNots ?? "")
                .Split(new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries))
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exclusions.Count == 0)
            return template.PromptBody;

        return template.PromptBody.TrimEnd() +
            "\n\nABSOLUTE EXCLUSIONS — do not generate any variation " +
            "that includes any of the following, even partially:\n" +
            string.Join("\n", exclusions.Select(item => $"- {item}"));
    }

    private static string BuildPrompt(
        string foundation, List<string> doNots)
    {
        var doNotSection = doNots.Count > 0
            ? "\n\nABSOLUTE EXCLUSIONS — do not generate any variation " +
              "that includes any of the following, even partially:\n" +
              string.Join("\n", doNots.Select(d => $"- {d}"))
            : "";

        return
            "I have a task for you but I need maximum variety because " +
            "you tend to repeat yourself when asked for multiple variations. " +
            "So before you do anything else, work through the following " +
            "steps in order.\n\n" +

            "Step 1 — Build your randomization engine:\n" +
            "Think about what kinds of lists would create genuine variety " +
            "in short-form hook video clip prompts. You decide everything: " +
            "how many lists to create, what each list contains, how many " +
            "items are in each list, and how to draw from and combine them. " +
            "Do not use any examples I might suggest — invent your own lists " +
            "from scratch. Randomly assign values from your lists to 100 " +
            "idea slots. Randomly shuffle the order of your process. " +
            "Show your work briefly.\n\n" +

            "Step 2 — Generate 100 raw hook ideas:\n" +
            "Apply your randomization engine to generate exactly 100 distinct " +
            "hook ideas for a 10-second AI-generated video clip starting from " +
            "the attached frame. For each idea write only a single sentence " +
            "describing what happens. Number them 1–100. Do not write full " +
            "prompts yet — just the raw ideas. Apply a different randomized " +
            "lens from Step 1 to each idea slot so no two ideas share the " +
            "same underlying mechanism.\n\n" +

            "Step 3 — Score and rank:\n" +
            "Review all 100 ideas. Score each one on viewer retention " +
            "potential (scroll-stopping power, curiosity gap, emotional " +
            "trigger, visual clarity, muted-viewer comprehension). " +
            "Select the top 30 highest-scoring ideas. These must be " +
            "genuinely diverse — if your top 30 share similar mechanisms, " +
            "replace the duplicates with the next highest-scoring " +
            "distinct idea.\n\n" +

            "Step 4 — Write the 30 Grok prompts:\n" +
            "For each of your top 30 selected ideas, write a detailed " +
            "Grok video-generation prompt of at least 100 words. " +
            $"Preserve the current experiment's core foundation of {foundation}. " +
            "Every prompt must follow these mandatory requirements:\n" +
            "- Describe the full chronological action beat by beat from the " +
            "supplied first frame through to the final second of the clip.\n" +
            "- Specify FAST, EXPLOSIVE, REALISTIC movement throughout. " +
            "Characters and objects must move with real physical weight, " +
            "momentum, and speed. No slow motion. No floaty movement. " +
            "No generic cinematic drift. Every second of the clip must " +
            "contain visible, purposeful, fast motion.\n" +
            "- Describe the speed and physicality of each action explicitly: " +
            "e.g. 'instantly', 'slams', 'snaps', 'hurls', 'crashes', " +
            "'detonates', 'tears through', 'accelerates hard'. " +
            "Never use passive or slow verbs.\n" +
            "- Break the 10 seconds into at least 3 distinct beats or " +
            "sub-events so the clip has meaningful progression, not one " +
            "repeated action.\n" +
            "- Describe what the camera sees in concrete physical terms — " +
            "what moves, where it moves, how fast, what the result is.\n" +
            "- The viewer must be able to watch muted and immediately " +
            "understand an unfolding event with clear cause and consequence.\n" +
            "- End each prompt with a one-line technical note specifying: " +
            "cinematic realism, fast realistic motion, no dialogue, no text.\n" +
            "Keep each prompt practical and directly executable by " +
            "Grok video generation." +
            doNotSection + "\n\n" +

            "After completing all four steps, return ONLY the final " +
            "30 prompts in exactly this format at the end of your response. " +
            "Each entry must have three parts:\n" +
            "- clipTitle: 8-12 words describing what actually happens in " +
            "the clip (the event, not a poetic label)\n" +
            "- clipHook: one sentence explaining why this is a hook " +
            "(what psychological mechanism makes the viewer need to see " +
            "what happens next)\n" +
            "- clipPrompts: the full Grok video generation prompt\n\n" +
            "clipTitle[1] = \"...\";\n" +
            "clipHook[1] = \"...\";\n" +
            "clipPrompts[1] = \"...\";\n" +
            "clipTitle[2] = \"...\";\n" +
            "clipHook[2] = \"...\";\n" +
            "clipPrompts[2] = \"...\";\n" +
            "... (continue for all 30)";
    }

    // ─────────────────────────────────────────────────────────────────
    // PASTE & PARSE
    // ─────────────────────────────────────────────────────────────────
    private async Task PasteLLMResponseAsync()
    {
        var result = await ShowMultilineEditorAsync(
            "Paste LLM Response",
            "Paste the full response from Grok/Claude:",
            "",
            "Paste response here...");
        if (string.IsNullOrWhiteSpace(result)) return;

        var (titles, hooks, prompts) = ParseClipPrompts(result.Trim());
        if (prompts.Count == 0)
        {
            await DisplayAlert("Parse Failed",
                "Could not find clipPrompts[1..5] in the response. " +
                "Make sure the LLM returned the exact format requested.",
                "OK");
            return;
        }

        _parsedPrompts.Clear();
        _parsedPrompts.AddRange(prompts);
        _parsedTitles.Clear();
        _parsedTitles.AddRange(titles);
        _parsedHooks.Clear();
        _parsedHooks.AddRange(hooks);
        RenderParsedPrompts();
    }

    private static (List<string> Titles, List<string> Hooks,
        List<string> Prompts) ParseClipPrompts(string response)
    {
        var titles = new List<string>();
        var hooks = new List<string>();
        var prompts = new List<string>();

        for (int i = 1; i <= 30; i++)
        {
            // Parse title
            titles.Add(ParseQuotedField(response,
                $"clipTitle[{i}]") ?? $"Hook {i}");

            // Parse hook summary
            hooks.Add(ParseQuotedField(response,
                $"clipHook[{i}]") ?? "");

            // Parse prompt
            var prompt = ParseQuotedField(response,
                $"clipPrompts[{i}]");
            if (prompt != null)
                prompts.Add(prompt);
        }
        return (titles, hooks, prompts);
    }

    private static string? ParseQuotedField(
        string response, string key)
    {
        var idx = response.IndexOf(key,
            StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var eqIdx = response.IndexOf('=', idx);
        if (eqIdx < 0) return null;
        var rest = response[(eqIdx + 1)..].TrimStart();
        if (!rest.StartsWith('"')) return null;
        int end = 1;
        while (end < rest.Length)
        {
            if (rest[end] == '"' && rest[end - 1] != '\\') break;
            end++;
        }
        return end < rest.Length
            ? rest[1..end].Replace("\\\"", "\"").Trim()
            : null;
    }

    private void RenderParsedPrompts()
    {
        _promptsContainer.Children.Clear();

        for (int i = 0; i < _parsedPrompts.Count; i++)
        {
            int capturedI = i;
            var promptText = _parsedPrompts[i];
            var title = i < _parsedTitles.Count
                ? _parsedTitles[i] : $"Hook {i + 1}";
            var hook = i < _parsedHooks.Count
                ? _parsedHooks[i] : "";

            var card = new Frame
            {
                BackgroundColor = Color.FromArgb("#F8F9FF"),
                Padding = 14,
                CornerRadius = 10,
                BorderColor = Color.FromArgb("#C5CAE9"),
                HasShadow = false
            };
            var inner = new VerticalStackLayout { Spacing = 6 };

            // Number + title (longer, describes what happens)
            inner.Children.Add(new Label
            {
                Text = $"{i + 1}. {title}",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            // Hook summary (why it works psychologically)
            if (!string.IsNullOrWhiteSpace(hook))
                inner.Children.Add(new Label
                {
                    Text = hook,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#5B63EE"),
                    FontAttributes = FontAttributes.Italic,
                    LineBreakMode = LineBreakMode.WordWrap
                });

            // Separator
            inner.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#E8EAF6"),
                Margin = new Thickness(0, 2, 0, 2)
            });

            // Full prompt (smaller, secondary)
            inner.Children.Add(new Label
            {
                Text = promptText,
                FontSize = 11,
                TextColor = Color.FromArgb("#555"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            var copyBtn = new Button
            {
                Text = " Copy",
                BackgroundColor = Color.FromArgb("#1565C0"),
                TextColor = Colors.White,
                CornerRadius = 8,
                FontSize = 13,
                HeightRequest = 36,
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(14, 0)
            };
            copyBtn.Clicked += async (_, _) =>
            {
                await Clipboard.SetTextAsync(_parsedPrompts[capturedI]);
                var orig = copyBtn.Text;
                copyBtn.Text = "✓ Copied!";
                await Task.Delay(1500);
                copyBtn.Text = orig;
            };
            inner.Children.Add(copyBtn);
            card.Content = inner;
            _promptsContainer.Children.Add(card);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // MULTILINE EDITOR MODAL
    // ─────────────────────────────────────────────────────────────────
    private async Task<string> ShowMultilineEditorAsync(
        string title, string message,
        string initialValue, string placeholder)
    {
        var tcs = new TaskCompletionSource<string>();
        var editorPage = new ContentPage
        {
            Title = title,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        var editor = new Editor
        {
            Text = initialValue,
            Placeholder = placeholder,
            HeightRequest = 300,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 13,
            Margin = new Thickness(16)
        };
        editorPage.Disappearing += (_, _) =>
            tcs.TrySetResult(editor.Text ?? "");
        var confirmBtn = new Button
        {
            Text = "Done",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Margin = new Thickness(16, 0)
        };
        confirmBtn.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(editor.Text ?? "");
            await Navigation.PopModalAsync();
        };
        editorPage.Content = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = 8,
            Children =
            {
                new Label
                {
                    Text = message,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#444"),
                    Margin = new Thickness(16, 16, 16, 0),
                    LineBreakMode = LineBreakMode.WordWrap
                },
                editor,
                confirmBtn
            }
        };
        await Navigation.PushModalAsync(editorPage);
        return await tcs.Task;
    }

    // ─────────────────────────────────────────────────────────────────
    // FOUNDATION PERSISTENCE
    // ─────────────────────────────────────────────────────────────────
    private string FoundationKey =>
        $"{FoundationStorageKey_Prefix}{_auth.CurrentUsername}";

    private async Task LoadFoundationAsync()
    {
        try
        {
            var stored = await SecureStorage.GetAsync(FoundationKey);
            _foundationEditor.Text =
                string.IsNullOrWhiteSpace(stored)
                    ? DefaultFoundation
                    : stored;
        }
        catch { _foundationEditor.Text = DefaultFoundation; }
    }

    private async Task SaveFoundationAsync(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                SecureStorage.Remove(FoundationKey);
            else
                await SecureStorage.SetAsync(FoundationKey, value);
        }
        catch { }
    }
}
