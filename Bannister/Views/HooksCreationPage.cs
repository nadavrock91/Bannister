using Bannister.Services;

namespace Bannister.Views;

public class HooksCreationPage : ContentPage
{
    private readonly AuthService _auth;

    private Editor _stage1Editor = null!;
    private Editor _stage2Editor = null!;
    private Editor _stage3Editor = null!;
    private Editor _stage4Editor = null!;
    private Editor _stage1TemplateEditor = null!;
    private Editor _stage2TemplateEditor = null!;
    private Editor _stage3TemplateEditor = null!;
    private Editor _stage4TemplateEditor = null!;
    private Editor _gridSuffixTemplateEditor = null!;
    private Editor _oneShotTemplateEditor = null!;
    private bool _isLoadingTemplates;

    private const string DefaultStage1Prompt = "Give me a flat list of exactly 100 random English words. Choose words from a broad mix of categories: physical objects, abstract concepts, emotions, animals, foods, materials, places, actions, sensations, colors, sounds, professions, time periods, weather phenomena, body parts, tools, textures, and feelings. Mix high-concept and low-concept, ordinary and strange. Return ONLY the words, numbered 1 to 100, one per line. No commentary, no headers, no grouping. Just the numbered list.";

    private const string DefaultStage2Prompt = """
Below is a list of 100 random words.
{WORDS}
Use these words as conceptual seeds to generate 100 ideas for SCROLL-STOPPING HOOK IMAGES. A scroll-stopping hook image is the first frame of a short-form video that makes the viewer involuntarily stop scrolling — through visual incongruity, emotional immediacy, mystery, danger, beauty, taboo, scale, or pattern interruption.
Each idea should be a one-sentence visual description of an image, not a description of the video. The image must be self-contained: a viewer seeing just this image with no caption or context should feel a pull to keep watching.
Mix the 100 words freely — combine 2 or 3 random words per image idea. Don't restrict yourself to using each word once. The point is variety, not coverage.
Return ONLY a numbered list 1 to 100, one image idea per line. No commentary, no headers.
""";

    private const string DefaultStage3Prompt = """
Below are 100 hook image ideas.
{HOOK_IDEAS}
For each image idea, identify the CONCEPTUAL HOOK — the underlying psychological or perceptual mechanic that makes the image scroll-stopping. Examples of conceptual hook categories: visual incongruity, scale violation, taboo proximity, hidden danger, beauty in unexpected context, pattern interruption, emotional immediacy on a face, time-frozen impossibility, scale of suffering, scale of joy, body horror at a distance, the uncanny valley, recognition of a forbidden act, recognition of a private moment.
For each of the 100 ideas, return:

{index}. {hook_idea_summary} | CONCEPTUAL HOOK: {one-phrase mechanic}
Return ONLY the numbered list. No commentary, no preamble.
""";

    private const string DefaultStage4Prompt = """
You have two source lists.
LIST A — 100 random words:

{WORDS}
LIST B — 100 image ideas with conceptual hooks:

{CONCEPTUAL_HOOKS}
Randomly pick 20 different combinations: each combination is ONE word from List A plus ONE conceptual-hook entry from List B. Use true random selection across all 20 picks — each pick should be different, and across the 20 picks the spread should feel varied (no clustering, no repeats). For each picked combination, synthesize a single IMAGE PROMPT that integrates the picked word into an image built around the picked conceptual hook mechanic. Each image prompt must be:

A scroll-stopping first frame for a short-form video.

Visually specific — describe composition, subject, lighting, mood, color, and one or two unexpected details.

Image-only — do not describe motion, sound, or what happens next.

40 to 80 words per prompt (shorter than a single integrated prompt since you're producing 20).

Output format — numbered 1 to 20, one block per variation:



PICKED WORD: {the word}

PICKED HOOK: {the conceptual hook phrase}

IMAGE PROMPT: {the 40–80 word image prompt}



PICKED WORD: ...

...
Continue through 20. Return only those 20 blocks. No commentary, no preamble, no closing remark.
""";

    private const string DefaultGridSuffix = "Create the result as a single 9:16 vertical concept sheet containing 20 numbered variations arranged in a 4x5 grid. Each panel must show a completely different idea, composition, story moment, camera angle, environment, mood, and visual hook. Prioritize variety of ideas over small visual changes. Large visible numbers 1-20. Cinematic realistic, high detail, easy side-by-side comparison, no text except numbers.";

    private const string DefaultOneShotPrompt = """
You're going to run a 4-stage hook image prompt generation pipeline internally. Do all four stages in order, but ONLY return the final output (the 20 image prompts). Do not show intermediate stages, do not explain your reasoning, do not output the words or hook ideas — only the final 20 prompts.
Stage 1 — Internally generate 100 random English words spanning physical objects, abstract concepts, emotions, animals, foods, materials, places, actions, sensations, colors, sounds, professions, time periods, weather phenomena, body parts, tools, textures, and feelings. Mix high-concept and low-concept, ordinary and strange.
Stage 2 — Internally use those 100 words to generate 100 ideas for SCROLL-STOPPING HOOK IMAGES. A scroll-stopping hook image is the first frame of a short-form video that makes the viewer involuntarily stop scrolling — through visual incongruity, emotional immediacy, mystery, danger, beauty, taboo, scale, or pattern interruption. Each idea is a one-sentence visual description of an image, not a description of the video. Mix the 100 words freely, combining 2 or 3 random words per image idea.
Stage 3 — Internally annotate each of the 100 image ideas with its CONCEPTUAL HOOK — the underlying psychological or perceptual mechanic that makes it scroll-stopping. Examples: visual incongruity, scale violation, taboo proximity, hidden danger, beauty in unexpected context, pattern interruption, emotional immediacy on a face, time-frozen impossibility, scale of suffering, scale of joy, body horror at a distance, the uncanny valley, recognition of a forbidden act, recognition of a private moment.
Stage 4 — From your 100 internal hook ideas, randomly pick 20 different combinations spanning maximum variety (no clustering, no near-duplicates). For each, synthesize a single IMAGE PROMPT (40 to 80 words) integrating one random word and one conceptual hook mechanic. Each prompt must be:
A scroll-stopping first frame for a short-form video.

Visually specific — describe composition, subject, lighting, mood, color, and one or two unexpected details.

Image-only — do not describe motion, sound, or what happens next.
Output format — return ONLY this block, nothing else:



PICKED WORD: {the word}

PICKED HOOK: {the conceptual hook phrase}

IMAGE PROMPT: {40–80 word image prompt}



PICKED WORD: ...

...
Continue through 20. No preamble, no commentary, no closing remark — just the 20 numbered blocks.
""";

    public HooksCreationPage(AuthService auth)
    {
        _auth = auth;
        Title = "Hooks Creation";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPromptTemplatesAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = "🪝 Hooks Creation",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new Label
                {
                    Text = "Generate scroll-stopping hook image prompts through a 4-stage variety amplifier. Copy each prompt to your LLM, paste the response, then run the next stage.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666")
                }
            }
        };

        _stage1Editor = CreateResponseEditor("Paste the LLM's 100 random words here...");
        _stage2Editor = CreateResponseEditor("Paste the LLM's 100 hook image ideas here...");
        _stage3Editor = CreateResponseEditor("Paste the LLM's conceptual hook analysis here...");
        _stage4Editor = CreateResponseEditor("Paste the LLM's 20 image prompts here.");

        _stage1TemplateEditor = CreatePromptTemplateEditor("stage1");
        _stage2TemplateEditor = CreatePromptTemplateEditor("stage2");
        _stage3TemplateEditor = CreatePromptTemplateEditor("stage3");
        _stage4TemplateEditor = CreatePromptTemplateEditor("stage4");
        _gridSuffixTemplateEditor = CreatePromptTemplateEditor("grid_suffix");
        _oneShotTemplateEditor = CreatePromptTemplateEditor("one_shot");

        stack.Children.Add(BuildOneShotSection());
        stack.Children.Add(CreateStageSection(
            "Stage 1 — 100 Random Words",
            "Get a list of 100 random words that will seed the rest of the flow.",
            "Copy Stage 1 prompt",
            OnCopyStage1PromptClicked,
            _stage1Editor,
            _stage1TemplateEditor,
            "stage1",
            DefaultStage1Prompt,
            null));
        stack.Children.Add(CreateStageSection(
            "Stage 2 — 100 Hook Ideas",
            "Use the random words to generate 100 hook image ideas.",
            "Copy Stage 2 prompt",
            OnCopyStage2PromptClicked,
            _stage2Editor,
            _stage2TemplateEditor,
            "stage2",
            DefaultStage2Prompt,
            "Use {WORDS} as the placeholder for Stage 1 content."));
        stack.Children.Add(CreateStageSection(
            "Stage 3 — Conceptual Hook Analysis",
            "Analyze what makes each hook idea conceptually scroll-stopping.",
            "Copy Stage 3 prompt",
            OnCopyStage3PromptClicked,
            _stage3Editor,
            _stage3TemplateEditor,
            "stage3",
            DefaultStage3Prompt,
            "Use {HOOK_IDEAS} as the placeholder for Stage 2 content."));
        stack.Children.Add(CreateStageSection(
            "Stage 4 — Integrated Image Prompt",
            "Combine a random word and a conceptual hook into a finished image prompt. Re-run as many times as you want for infinite variety.",
            "Copy Stage 4 prompt",
            OnCopyStage4PromptClicked,
            _stage4Editor,
            _stage4TemplateEditor,
            "stage4",
            DefaultStage4Prompt,
            "Use {WORDS} and {CONCEPTUAL_HOOKS} as placeholders for Stage 1 and Stage 3 content.",
            CreateStage4TrailingBlock()));

        stack.Children.Add(new Label
        {
            Text = "Tip: re-press Copy Stage 4 prompt for a fresh set of 20. Then paste the LLM result, and use Copy 20 prompts + grid suffix to send everything to an image generator that renders a 4x5 numbered concept sheet.",
            FontSize = 12,
            TextColor = Color.FromArgb("#777"),
            FontAttributes = FontAttributes.Italic,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var rootGrid = new Grid();
        rootGrid.Children.Add(new ScrollView { Content = stack });
        Content = rootGrid;
    }

    private async Task LoadPromptTemplatesAsync()
    {
        _isLoadingTemplates = true;
        try
        {
            _stage1TemplateEditor.Text = await GetCustomPromptAsync("stage1") ?? DefaultStage1Prompt;
            _stage2TemplateEditor.Text = await GetCustomPromptAsync("stage2") ?? DefaultStage2Prompt;
            _stage3TemplateEditor.Text = await GetCustomPromptAsync("stage3") ?? DefaultStage3Prompt;
            _stage4TemplateEditor.Text = await GetCustomPromptAsync("stage4") ?? DefaultStage4Prompt;
            _gridSuffixTemplateEditor.Text = await GetCustomPromptAsync("grid_suffix") ?? DefaultGridSuffix;
            _oneShotTemplateEditor.Text = await GetCustomPromptAsync("one_shot") ?? DefaultOneShotPrompt;
        }
        finally
        {
            _isLoadingTemplates = false;
        }
    }

    private Frame BuildOneShotSection()
    {
        var oneShotButton = new Button
        {
            Text = " One-shot: all 4 stages in a single LLM call",
            BackgroundColor = Color.FromArgb("#FF6F00"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Fill
        };
        oneShotButton.Clicked += OnCopyOneShotPromptClicked;

        var subtitle = new Label
        {
            Text = "Skip the 4-stage flow. Copies a single prompt that asks the LLM to do everything internally — produce 100 words, 100 hook ideas, conceptual analysis, and 20 final image prompts. Paste the LLM's response directly into the Stage 4 box below.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };

        var expander = CreatePromptTemplateExpander(
            _oneShotTemplateEditor,
            "one_shot",
            DefaultOneShotPrompt,
            null,
            out var toggleButton);

        return new Frame
        {
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            BorderColor = Color.FromArgb("#FF6F00"),
            CornerRadius = 10,
            Padding = 16,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    oneShotButton,
                    subtitle,
                    toggleButton,
                    expander
                }
            }
        };
    }

    private Frame CreateStageSection(
        string title,
        string subtitle,
        string buttonText,
        EventHandler clicked,
        Editor responseEditor,
        Editor templateEditor,
        string storageSuffix,
        string defaultPrompt,
        string? placeholderHint,
        View? trailingView = null)
    {
        var copyButton = new Button
        {
            Text = buttonText,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Start
        };
        copyButton.Clicked += clicked;

        var templateContainer = CreatePromptTemplateExpander(
            templateEditor,
            storageSuffix,
            defaultPrompt,
            placeholderHint,
            out var toggleButton);

        var sectionStack = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new Label
                {
                    Text = subtitle,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#666")
                },
                copyButton,
                toggleButton,
                templateContainer,
                responseEditor
            }
        };

        if (trailingView != null)
            sectionStack.Children.Add(trailingView);

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 16,
            HasShadow = false,
            Content = sectionStack
        };
    }

    private VerticalStackLayout CreatePromptTemplateExpander(
        Editor templateEditor,
        string storageSuffix,
        string defaultPrompt,
        string? placeholderHint,
        out Button toggleButton)
    {
        var container = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false
        };

        container.Children.Add(new Label
        {
            Text = "Prompt template (edit to customize):",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });
        container.Children.Add(templateEditor);

        if (!string.IsNullOrWhiteSpace(placeholderHint))
        {
            container.Children.Add(new Label
            {
                Text = placeholderHint,
                FontSize = 11,
                FontAttributes = FontAttributes.Italic,
                TextColor = Color.FromArgb("#777")
            });
        }

        var resetButton = new Button
        {
            Text = "Reset to default",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(0),
            HorizontalOptions = LayoutOptions.Start
        };
        resetButton.Clicked += async (_, _) =>
        {
            var reset = await DisplayAlert(
                "Reset to default?",
                "This will discard your custom prompt and restore the built-in default.",
                "Reset",
                "Cancel");
            if (!reset) return;

            await SetCustomPromptAsync(storageSuffix, "");
            _isLoadingTemplates = true;
            try
            {
                templateEditor.Text = defaultPrompt;
            }
            finally
            {
                _isLoadingTemplates = false;
            }
        };
        container.Children.Add(resetButton);

        var localToggleButton = new Button
        {
            Text = "Show prompt template",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            FontSize = 12,
            HeightRequest = 36,
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.Start
        };
        localToggleButton.Clicked += (_, _) =>
        {
            container.IsVisible = !container.IsVisible;
            localToggleButton.Text = container.IsVisible ? "Hide prompt template" : "Show prompt template";
        };

        toggleButton = localToggleButton;
        return container;
    }

    private View CreateStage4TrailingBlock()
    {
        var gridSuffixExpander = CreatePromptTemplateExpander(
            _gridSuffixTemplateEditor,
            "grid_suffix",
            DefaultGridSuffix,
            null,
            out var gridToggle);

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                CreateImageGeneratorCopyButton(),
                new Label
                {
                    Text = "Grid suffix template (customize)",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00838F")
                },
                gridToggle,
                gridSuffixExpander
            }
        };
    }

    private Button CreateImageGeneratorCopyButton()
    {
        var button = new Button
        {
            Text = "Copy 20 prompts + grid suffix for image generator",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Start
        };
        button.Clicked += OnCopyImageGeneratorPromptClicked;
        return button;
    }

    private Editor CreatePromptTemplateEditor(string storageSuffix)
    {
        var editor = new Editor
        {
            HeightRequest = 220,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            FontSize = 12,
            FontFamily = "Consolas"
        };
        editor.TextChanged += async (_, e) =>
        {
            if (_isLoadingTemplates) return;
            await SetCustomPromptAsync(storageSuffix, e.NewTextValue ?? "");
        };
        return editor;
    }

    private static Editor CreateResponseEditor(string placeholder) => new()
    {
        Placeholder = placeholder,
        HeightRequest = 44,
        AutoSize = EditorAutoSizeOption.Disabled,
        BackgroundColor = Color.FromArgb("#FAFAFA"),
        TextColor = Color.FromArgb("#222"),
        PlaceholderColor = Color.FromArgb("#999"),
        FontSize = 13
    };

    private async Task<string?> GetCustomPromptAsync(string suffix)
    {
        var username = _auth.CurrentUsername;
        if (string.IsNullOrWhiteSpace(username))
            return null;

        try
        {
            var value = await SecureStorage.GetAsync(GetCustomPromptKey(suffix, username));
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private async Task SetCustomPromptAsync(string suffix, string value)
    {
        var username = _auth.CurrentUsername;
        if (string.IsNullOrWhiteSpace(username))
            return;

        try
        {
            var key = GetCustomPromptKey(suffix, username);
            if (string.IsNullOrWhiteSpace(value))
                SecureStorage.Remove(key);
            else
                await SecureStorage.SetAsync(key, value);
        }
        catch { }
    }

    private static string GetCustomPromptKey(string suffix, string username) =>
        $"hooks_creation_{suffix}_custom_{username}";

    private static string ReplaceToken(string template, string token, string value) =>
        (template ?? "").Replace(token, value ?? "", StringComparison.Ordinal);

    private static string BuildStage2Prompt(string template, string words) =>
        ReplaceToken(template, "{WORDS}", words);

    private static string BuildStage3Prompt(string template, string hookIdeas) =>
        ReplaceToken(template, "{HOOK_IDEAS}", hookIdeas);

    private static string BuildStage4Prompt(string template, string words, string conceptualHooks) =>
        ReplaceToken(ReplaceToken(template, "{WORDS}", words), "{CONCEPTUAL_HOOKS}", conceptualHooks);

    private async void OnCopyStage1PromptClicked(object? sender, EventArgs e)
    {
        _stage1Editor.Text = "";
        await Clipboard.SetTextAsync(_stage1TemplateEditor.Text ?? DefaultStage1Prompt);
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 1 prompt");
        await ShowPasteResultModalAsync("Stage 1 (100 words)", _stage1Editor);
    }

    private async void OnCopyStage2PromptClicked(object? sender, EventArgs e)
    {
        _stage2Editor.Text = "";
        var words = _stage1Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(words))
        {
            await DisplayAlert("Stage 1 needed", "Paste the Stage 1 result first.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(BuildStage2Prompt(_stage2TemplateEditor.Text ?? DefaultStage2Prompt, words));
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 2 prompt");
        await ShowPasteResultModalAsync("Stage 2 (100 hook ideas)", _stage2Editor);
    }

    private async void OnCopyStage3PromptClicked(object? sender, EventArgs e)
    {
        _stage3Editor.Text = "";
        var hookIdeas = _stage2Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(hookIdeas))
        {
            await DisplayAlert("Stage 2 needed", "Paste the Stage 2 result first.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(BuildStage3Prompt(_stage3TemplateEditor.Text ?? DefaultStage3Prompt, hookIdeas));
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 3 prompt");
        await ShowPasteResultModalAsync("Stage 3 (conceptual hook analysis)", _stage3Editor);
    }

    private async void OnCopyStage4PromptClicked(object? sender, EventArgs e)
    {
        _stage4Editor.Text = "";
        var words = _stage1Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(words))
        {
            await DisplayAlert("Stage 1 needed", "Paste the Stage 1 result first.", "OK");
            return;
        }

        var conceptualHooks = _stage3Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(conceptualHooks))
        {
            await DisplayAlert("Stage 3 needed", "Paste the Stage 3 result first.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(BuildStage4Prompt(_stage4TemplateEditor.Text ?? DefaultStage4Prompt, words, conceptualHooks));
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 4 prompt");
        await ShowPasteResultModalAsync("Stage 4 (20 image prompts)", _stage4Editor);
    }

    private async void OnCopyOneShotPromptClicked(object? sender, EventArgs e)
    {
        _stage4Editor.Text = "";
        await Clipboard.SetTextAsync(_oneShotTemplateEditor.Text ?? DefaultOneShotPrompt);
        if (sender is Button btn) await FlashCopiedAsync(btn, " One-shot: all 4 stages in a single LLM call");
        await ShowPasteResultModalAsync("20 image prompts", _stage4Editor);
    }

    private async void OnCopyImageGeneratorPromptClicked(object? sender, EventArgs e)
    {
        var prompts = _stage4Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(prompts))
        {
            await DisplayAlert("Stage 4 needed", "Paste the LLM's 20 image prompts first.", "OK");
            return;
        }

        string finalBlock = prompts + "\n\n" + (_gridSuffixTemplateEditor.Text ?? DefaultGridSuffix);
        await Clipboard.SetTextAsync(finalBlock);
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy 20 prompts + grid suffix for image generator");
    }

    private async Task FlashCopiedAsync(Button btn, string original)
    {
        btn.Text = "Copied!";
        await Task.Delay(1000);
        btn.Text = original;
    }

    private async Task ShowPasteResultModalAsync(string stageLabel, Editor targetEditor)
    {
        if (Content is not Grid root)
            return;

        var modalEditor = new Editor
        {
            Placeholder = $"Paste the LLM's {stageLabel} result here...",
            HeightRequest = 280,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            FontSize = 13
        };

        var applyButton = new Button
        {
            Text = "Apply",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Fill
        };

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Fill
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
            WidthRequest = 480,
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
                        Text = $"Paste the {stageLabel} result",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "When you have the LLM's response, paste it here and tap Apply. Or tap Close and paste directly into the page.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    },
                    modalEditor,
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
            targetEditor.Text = modalEditor.Text ?? "";
            root.Children.Remove(overlay);
            tcs.TrySetResult(true);
        };
        closeButton.Clicked += (_, _) =>
        {
            root.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        root.Children.Add(overlay);
        await tcs.Task;
    }
}
