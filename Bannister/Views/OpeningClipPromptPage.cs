using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class OpeningClipPromptPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomPromptService _customPrompts;
    private readonly DoNotService _doNotService;

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

    // Stage 4 — paste response
    private Button _pasteResponseButton = null!;

    // Stage 5 — parsed prompts
    private VerticalStackLayout _promptsContainer = null!;

    private readonly List<string> _parsedPrompts = new();
    private readonly List<string> _parsedTitles = new();

    public OpeningClipPromptPage(
        AuthService auth,
        CustomPromptService customPrompts,
        DoNotService doNotService)
    {
        _auth = auth;
        _customPrompts = customPrompts;
        _doNotService = doNotService;
        Title = "Opening Clip Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFoundationAsync();
        await RefreshDoNotListAsync();
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

        Content = new ScrollView { Content = stack };
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
        var prompt = BuildPrompt(foundation, doNots);
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
            "So before you do anything else, design your own randomization " +
            "system from scratch.\n\n" +

            "Step 1 — Build your randomization engine:\n" +
            "Think about what kinds of lists would create genuine variety " +
            "in short-form hook video clip prompts. You decide everything: " +
            "how many lists to create, what each list contains, how many " +
            "items are in each list, and how to draw from and combine them. " +
            "Examples of the kinds of things you might consider (but are not " +
            "limited to): psychological triggers, narrative structures, " +
            "visual event types, emotional arcs, camera relationships, " +
            "consequence types, mystery categories, physical laws to violate. " +
            "Do not use my examples as your list — invent your own. " +
            "Randomly assign values from your lists to each of the 5 clip slots. " +
            "Randomly shuffle the order of whatever process you set up. " +
            "Show your work briefly — what lists you made, what you drew, " +
            "what order you set up.\n\n" +

            "Step 2 — Apply your randomization engine:\n" +
            "For each of the 5 clip slots, apply the process you designed " +
            "in Step 1. Work through it slot by slot. For each slot, first " +
            "apply the analytical step your randomization assigned to it, " +
            "then use that analysis to generate the Grok video prompt for " +
            "that slot. The analysis for each slot should be genuinely " +
            "different from the others because your randomization assigned " +
            "different lenses.\n\n" +

            "The task itself:\n" +
            "Use the attached starting frame as the first frame of a " +
            "10-second AI-generated video. Generate 5 Grok video-generation " +
            "prompts that begin naturally from this exact starting frame and " +
            "are designed to maximize 10-second viewer retention. " +
            $"Preserve the current experiment's core foundation of {foundation}. " +
            "Each prompt must describe the chronological action that should " +
            "happen from the supplied first frame. The viewer should be able " +
            "to watch muted and still perceive an unfolding event. " +
            "Keep each prompt practical for Grok video generation." +
            doNotSection + "\n\n" +

            "After your Step 1 and Step 2 working, return ONLY the final " +
            "5 prompts in exactly this format at the end of your response. " +
            "Each entry must have a clipTitle (3-5 word hook idea label for " +
            "quick skimming) and a clipPrompt (the full Grok prompt):\n" +
            "clipTitle[1] = \"...\";\n" +
            "clipPrompts[1] = \"...\";\n" +
            "clipTitle[2] = \"...\";\n" +
            "clipPrompts[2] = \"...\";\n" +
            "clipTitle[3] = \"...\";\n" +
            "clipPrompts[3] = \"...\";\n" +
            "clipTitle[4] = \"...\";\n" +
            "clipPrompts[4] = \"...\";\n" +
            "clipTitle[5] = \"...\";\n" +
            "clipPrompts[5] = \"...\";";
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

        var (titles, prompts) = ParseClipPrompts(result.Trim());
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
        RenderParsedPrompts();
    }

    private static (List<string> Titles, List<string> Prompts)
        ParseClipPrompts(string response)
    {
        var titles = new List<string>();
        var prompts = new List<string>();

        for (int i = 1; i <= 5; i++)
        {
            // Parse title
            var titlePattern = $"clipTitle[{i}]";
            var titleIdx = response.IndexOf(titlePattern,
                StringComparison.OrdinalIgnoreCase);
            if (titleIdx >= 0)
            {
                var eqIdx = response.IndexOf('=', titleIdx);
                if (eqIdx >= 0)
                {
                    var rest = response[(eqIdx + 1)..].TrimStart();
                    if (rest.StartsWith('"'))
                    {
                        int end = 1;
                        while (end < rest.Length)
                        {
                            if (rest[end] == '"' && rest[end - 1] != '\\')
                                break;
                            end++;
                        }
                        titles.Add(end < rest.Length
                            ? rest[1..end].Replace("\\\"", "\"").Trim()
                            : $"Hook {i}");
                    }
                    else titles.Add($"Hook {i}");
                }
                else titles.Add($"Hook {i}");
            }
            else titles.Add($"Hook {i}");

            // Parse prompt
            var promptPattern = $"clipPrompts[{i}]";
            var promptIdx = response.IndexOf(promptPattern,
                StringComparison.OrdinalIgnoreCase);
            if (promptIdx < 0) continue;
            var peqIdx = response.IndexOf('=', promptIdx);
            if (peqIdx < 0) continue;
            var prest = response[(peqIdx + 1)..].TrimStart();
            if (!prest.StartsWith('"')) continue;
            int pend = 1;
            while (pend < prest.Length)
            {
                if (prest[pend] == '"' && prest[pend - 1] != '\\') break;
                pend++;
            }
            if (pend < prest.Length)
                prompts.Add(prest[1..pend]
                    .Replace("\\\"", "\"").Trim());
        }
        return (titles, prompts);
    }

    private void RenderParsedPrompts()
    {
        _promptsContainer.Children.Clear();

        for (int i = 0; i < _parsedPrompts.Count; i++)
        {
            int capturedI = i;
            var promptText = _parsedPrompts[i];
            var title = i < _parsedTitles.Count
                ? _parsedTitles[i]
                : $"Hook {i + 1}";

            var card = new Frame
            {
                BackgroundColor = Color.FromArgb("#F8F9FF"),
                Padding = 14,
                CornerRadius = 10,
                BorderColor = Color.FromArgb("#C5CAE9"),
                HasShadow = false
            };
            var inner = new VerticalStackLayout { Spacing = 8 };

            inner.Children.Add(new Label
            {
                Text = $"{i + 1}. {title}",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0")
            });
            inner.Children.Add(new Label
            {
                Text = promptText,
                FontSize = 12,
                TextColor = Color.FromArgb("#444"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            var copyBtn = new Button
            {
                Text = " Copy",
                BackgroundColor = Color.FromArgb("#1565C0"),
                TextColor = Colors.White,
                CornerRadius = 8,
                FontSize = 13,
                HeightRequest = 38,
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
            await Navigation.PopAsync();
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
        await Navigation.PushAsync(editorPage);
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
