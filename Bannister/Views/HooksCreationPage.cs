namespace Bannister.Views;

public class HooksCreationPage : ContentPage
{
    private Editor _stage1Editor = null!;
    private Editor _stage2Editor = null!;
    private Editor _stage3Editor = null!;
    private Editor _stage4Editor = null!;

    private const string Stage1Prompt = "Give me a flat list of exactly 100 random English words. Choose words from a broad mix of categories: physical objects, abstract concepts, emotions, animals, foods, materials, places, actions, sensations, colors, sounds, professions, time periods, weather phenomena, body parts, tools, textures, and feelings. Mix high-concept and low-concept, ordinary and strange. Return ONLY the words, numbered 1 to 100, one per line. No commentary, no headers, no grouping. Just the numbered list.";
    private const string ImageGeneratorGridSuffix = "Create the result as a single 9:16 vertical concept sheet containing 20 numbered variations arranged in a 4x5 grid. Each panel must show a completely different idea, composition, story moment, camera angle, environment, mood, and visual hook. Prioritize variety of ideas over small visual changes. Large visible numbers 1-20. Cinematic realistic, high detail, easy side-by-side comparison, no text except numbers.";

    public HooksCreationPage()
    {
        Title = "Hooks Creation";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
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

        _stage1Editor = CreateEditor("Paste the LLM's 100 random words here...");
        _stage2Editor = CreateEditor("Paste the LLM's 100 hook image ideas here...");
        _stage3Editor = CreateEditor("Paste the LLM's conceptual hook analysis here...");
        _stage4Editor = CreateEditor("Paste the LLM's 20 image prompts here.");

        stack.Children.Add(CreateStageSection(
            "Stage 1 — 100 Random Words",
            "Get a list of 100 random words that will seed the rest of the flow.",
            "Copy Stage 1 prompt",
            OnCopyStage1PromptClicked,
            _stage1Editor));
        stack.Children.Add(CreateStageSection(
            "Stage 2 — 100 Hook Ideas",
            "Use the random words to generate 100 hook image ideas.",
            "Copy Stage 2 prompt",
            OnCopyStage2PromptClicked,
            _stage2Editor));
        stack.Children.Add(CreateStageSection(
            "Stage 3 — Conceptual Hook Analysis",
            "Analyze what makes each hook idea conceptually scroll-stopping.",
            "Copy Stage 3 prompt",
            OnCopyStage3PromptClicked,
            _stage3Editor));
        stack.Children.Add(CreateStageSection(
            "Stage 4 — Integrated Image Prompt",
            "Combine a random word and a conceptual hook into a finished image prompt. Re-run as many times as you want for infinite variety.",
            "Copy Stage 4 prompt",
            OnCopyStage4PromptClicked,
            _stage4Editor,
            CreateImageGeneratorCopyButton()));

        stack.Children.Add(new Label
        {
            Text = "Tip: re-press Copy Stage 4 prompt for a fresh set of 20. Then paste the LLM result, and use Copy 20 prompts + grid suffix to send everything to an image generator that renders a 4x5 numbered concept sheet.",
            FontSize = 12,
            TextColor = Color.FromArgb("#777"),
            FontAttributes = FontAttributes.Italic,
            Margin = new Thickness(0, 4, 0, 0)
        });

        Content = new ScrollView { Content = stack };
    }

    private static Frame CreateStageSection(string title, string subtitle, string buttonText, EventHandler clicked, Editor editor, View? trailingView = null)
    {
        var button = new Button
        {
            Text = buttonText,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 42,
            HorizontalOptions = LayoutOptions.Start
        };
        button.Clicked += clicked;

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
                button,
                editor
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

    private static Editor CreateEditor(string placeholder) => new()
    {
        Placeholder = placeholder,
        HeightRequest = 180,
        AutoSize = EditorAutoSizeOption.TextChanges,
        BackgroundColor = Color.FromArgb("#FAFAFA"),
        TextColor = Color.FromArgb("#222"),
        PlaceholderColor = Color.FromArgb("#999"),
        FontSize = 13
    };

    private static string BuildStage2Prompt(string words) => $$"""
Below is a list of 100 random words.
{{words}}
Use these words as conceptual seeds to generate 100 ideas for SCROLL-STOPPING HOOK IMAGES. A scroll-stopping hook image is the first frame of a short-form video that makes the viewer involuntarily stop scrolling — through visual incongruity, emotional immediacy, mystery, danger, beauty, taboo, scale, or pattern interruption.
Each idea should be a one-sentence visual description of an image, not a description of the video. The image must be self-contained: a viewer seeing just this image with no caption or context should feel a pull to keep watching.
Mix the 100 words freely — combine 2 or 3 random words per image idea. Don't restrict yourself to using each word once. The point is variety, not coverage.
Return ONLY a numbered list 1 to 100, one image idea per line. No commentary, no headers.
""";

    private static string BuildStage3Prompt(string hookIdeas) => $$"""
Below are 100 hook image ideas.
{{hookIdeas}}
For each image idea, identify the CONCEPTUAL HOOK — the underlying psychological or perceptual mechanic that makes the image scroll-stopping. Examples of conceptual hook categories: visual incongruity, scale violation, taboo proximity, hidden danger, beauty in unexpected context, pattern interruption, emotional immediacy on a face, time-frozen impossibility, scale of suffering, scale of joy, body horror at a distance, the uncanny valley, recognition of a forbidden act, recognition of a private moment.
For each of the 100 ideas, return:

{index}. {hook_idea_summary} | CONCEPTUAL HOOK: {one-phrase mechanic}
Return ONLY the numbered list. No commentary, no preamble.
""";

    private static string BuildStage4Prompt(string words, string conceptualHooks) => $$"""
You have two source lists.
LIST A — 100 random words:

{{words}}
LIST B — 100 image ideas with conceptual hooks:

{{conceptualHooks}}
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

    private async void OnCopyStage1PromptClicked(object? sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(Stage1Prompt);
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 1 prompt");
    }

    private async void OnCopyStage2PromptClicked(object? sender, EventArgs e)
    {
        var words = _stage1Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(words))
        {
            await DisplayAlert("Stage 1 needed", "Paste the Stage 1 result first.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(BuildStage2Prompt(words));
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 2 prompt");
    }

    private async void OnCopyStage3PromptClicked(object? sender, EventArgs e)
    {
        var hookIdeas = _stage2Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(hookIdeas))
        {
            await DisplayAlert("Stage 2 needed", "Paste the Stage 2 result first.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(BuildStage3Prompt(hookIdeas));
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 3 prompt");
    }

    private async void OnCopyStage4PromptClicked(object? sender, EventArgs e)
    {
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

        await Clipboard.SetTextAsync(BuildStage4Prompt(words, conceptualHooks));
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy Stage 4 prompt");
    }

    private async void OnCopyImageGeneratorPromptClicked(object? sender, EventArgs e)
    {
        var prompts = _stage4Editor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(prompts))
        {
            await DisplayAlert("Stage 4 needed", "Paste the LLM's 20 image prompts first.", "OK");
            return;
        }

        string finalBlock = prompts + "\n\n" + ImageGeneratorGridSuffix;
        await Clipboard.SetTextAsync(finalBlock);
        if (sender is Button btn) await FlashCopiedAsync(btn, "Copy 20 prompts + grid suffix for image generator");
    }

    private async Task FlashCopiedAsync(Button btn, string original)
    {
        btn.Text = "Copied!";
        await Task.Delay(1000);
        btn.Text = original;
    }
}
