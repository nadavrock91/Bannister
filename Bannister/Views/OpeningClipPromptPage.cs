using Bannister.Services;

namespace Bannister.Views;

public class OpeningClipPromptPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomPromptService _customPrompts;

    // Stage 1 — image pick
    private Label _imageInfoLabel = null!;
    private Image _imagePreview = null!;
    private Label _previewHint = null!;

    // Stage 2 — core foundation
    private Editor _foundationEditor = null!;

    // Stage 3 — output
    private Button _buildButton = null!;
    private Label _outputStatusLabel = null!;

    // Stage 4 — paste response
    private Button _pasteResponseButton = null!;

    // Stage 5 — parsed prompts
    private VerticalStackLayout _promptsContainer = null!;

    private string? _pickedFilePath;
    private readonly List<string> _parsedPrompts = new();

    private const string FoundationStorageKey_Prefix = "opening_clip_foundation_";
    private const string DefaultFoundation = "foe + movement";
    private const string FavoritesArea = "OpeningClipFoundation";

    public OpeningClipPromptPage(
        AuthService auth,
        CustomPromptService customPrompts)
    {
        _auth = auth;
        _customPrompts = customPrompts;
        Title = "Opening Clip Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFoundationAsync();
    }

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
            Text = "Pick a starting frame. Bannister builds a retention-optimised " +
                   "prompt for 5 Grok video variations. Copy to clipboard, " +
                   "attach the image in Grok, paste and run.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        stack.Children.Add(BuildStageCard("Stage 1 — Starting Frame",
            BuildStage1Content()));
        stack.Children.Add(BuildStageCard("Stage 2 — Core Foundation",
            BuildStage2Content()));
        stack.Children.Add(BuildStageCard("Stage 3 — Build & Copy Prompt",
            BuildStage3Content()));
        stack.Children.Add(BuildStageCard("Stage 4 — Paste LLM Response",
            BuildStage4Content()));
        stack.Children.Add(BuildStageCard("Stage 5 — Generated Prompts",
            BuildStage5Content()));

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

    // ── Stage 1 ──────────────────────────────────────────────────────
    private View BuildStage1Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };

        var pickBtn = new Button
        {
            Text = " Choose Starting Frame",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44
        };
        pickBtn.Clicked += async (_, _) => await PickImageAsync();
        v.Children.Add(pickBtn);

        _imageInfoLabel = new Label
        {
            Text = "No image selected.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        v.Children.Add(_imageInfoLabel);

        _imagePreview = new Image
        {
            HeightRequest = 200,
            Aspect = Aspect.AspectFit,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill
        };
        v.Children.Add(_imagePreview);

        _previewHint = new Label
        {
            Text = "Tap to fullscreen",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        var previewTap = new TapGestureRecognizer();
        previewTap.Tapped += async (_, _) =>
        {
            if (_imagePreview.Source == null || !_imagePreview.IsVisible) return;
            await Navigation.PushAsync(
                new FullScreenImagePage(_imagePreview.Source));
        };
        _imagePreview.GestureRecognizers.Add(previewTap);
        v.Children.Add(_previewHint);

        return v;
    }

    // ── Stage 2 ──────────────────────────────────────────────────────
    private View BuildStage2Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };

        v.Children.Add(new Label
        {
            Text = "The current retention hypothesis to keep constant across variations " +
                   "(e.g. \"foe + movement\"). Edit per experiment.",
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

    // ── Stage 3 ──────────────────────────────────────────────────────
    private View BuildStage3Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };

        v.Children.Add(new Label
        {
            Text = "Builds the full retention-optimised prompt. Attach the starting " +
                   "frame image yourself in Grok/Claude after pasting.",
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
            IsVisible = false
        };
        v.Children.Add(_outputStatusLabel);

        return v;
    }

    // ── Stage 4 ──────────────────────────────────────────────────────
    private View BuildStage4Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };

        v.Children.Add(new Label
        {
            Text = "After running the prompt in Grok/Claude with the image attached, " +
                   "paste the response here to parse the 5 clip prompts.",
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

    // ── Stage 5 ──────────────────────────────────────────────────────
    private View BuildStage5Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };

        v.Children.Add(new Label
        {
            Text = "Parsed prompts appear here. Tap Copy to send each to Grok.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        _promptsContainer = new VerticalStackLayout { Spacing = 12 };
        v.Children.Add(_promptsContainer);

        return v;
    }

    // ── Logic ─────────────────────────────────────────────────────────
    private async Task PickImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select starting frame",
                FileTypes = FilePickerFileType.Images
            });
            if (result == null) return;

            _pickedFilePath = result.FullPath;
            _imageInfoLabel.Text = System.IO.Path.GetFileName(_pickedFilePath);
            _imagePreview.Source = ImageSource.FromFile(_pickedFilePath);
            _imagePreview.IsVisible = true;
            _previewHint.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open image: {ex.Message}", "OK");
        }
    }

    private async Task BuildAndCopyPromptAsync()
    {
        var foundation = (_foundationEditor.Text ?? DefaultFoundation).Trim();
        if (string.IsNullOrWhiteSpace(foundation))
            foundation = DefaultFoundation;

        var prompt = BuildPrompt(foundation);
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

    private static string BuildPrompt(string foundation)
    {
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
            "Keep each prompt practical for Grok video generation.\n\n" +

            "After your Step 1 and Step 2 working, return ONLY the final " +
            "5 prompts in exactly this format at the end of your response:\n" +
            "clipPrompts[1] = \"...\";\n" +
            "clipPrompts[2] = \"...\";\n" +
            "clipPrompts[3] = \"...\";\n" +
            "clipPrompts[4] = \"...\";\n" +
            "clipPrompts[5] = \"...\";";
    }

    private async Task PasteLLMResponseAsync()
    {
        var result = await ShowMultilineEditorAsync(
            "Paste LLM Response",
            "Paste the full response from Grok/Claude:",
            "",
            "Paste response here...");

        if (string.IsNullOrWhiteSpace(result)) return;

        var parsed = ParseClipPrompts(result.Trim());
        if (parsed.Count == 0)
        {
            await DisplayAlert("Parse Failed",
                "Could not find clipPrompts[1..5] in the response. " +
                "Make sure the LLM returned the exact format requested.",
                "OK");
            return;
        }

        _parsedPrompts.Clear();
        _parsedPrompts.AddRange(parsed);
        RenderParsedPrompts();
    }

    private static List<string> ParseClipPrompts(string response)
    {
        var results = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            var pattern = $"clipPrompts[{i}]";
            var idx = response.IndexOf(pattern,
                StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var eqIdx = response.IndexOf('=', idx);
            if (eqIdx < 0) continue;

            var rest = response[(eqIdx + 1)..].TrimStart();
            if (rest.StartsWith('"'))
            {
                // Find closing quote — handle escaped quotes
                int end = 1;
                while (end < rest.Length)
                {
                    if (rest[end] == '"' && rest[end - 1] != '\\') break;
                    end++;
                }
                if (end < rest.Length)
                    results.Add(rest[1..end]
                        .Replace("\\\"", "\"")
                        .Trim());
            }
        }
        return results;
    }

    private void RenderParsedPrompts()
    {
        _promptsContainer.Children.Clear();

        for (int i = 0; i < _parsedPrompts.Count; i++)
        {
            int capturedI = i;
            var promptText = _parsedPrompts[i];

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
                Text = $"Prompt {i + 1}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0")
            });

            inner.Children.Add(new Label
            {
                Text = promptText,
                FontSize = 13,
                TextColor = Color.FromArgb("#222"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            var copyBtn = new Button
            {
                Text = $" Copy Prompt {i + 1}",
                BackgroundColor = Color.FromArgb("#1565C0"),
                TextColor = Colors.White,
                CornerRadius = 8,
                FontSize = 13,
                HeightRequest = 40,
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

    private async Task<string> ShowMultilineEditorAsync(
        string title, string message,
        string initialValue, string placeholder)
    {
        // Delegate to page's own modal — matches TargetedHooksPage pattern
        // Use DisplayPromptAsync for single line or a custom modal for multiline
        // Since MAUI doesn't have a built-in multiline prompt, use a push page
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

    // ── Foundation persistence ────────────────────────────────────────
    private string FoundationKey =>
        $"{FoundationStorageKey_Prefix}{_auth.CurrentUsername}";

    private async Task LoadFoundationAsync()
    {
        try
        {
            var stored = await SecureStorage.GetAsync(FoundationKey);
            _foundationEditor.Text =
                string.IsNullOrWhiteSpace(stored) ? DefaultFoundation : stored;
        }
        catch
        {
            _foundationEditor.Text = DefaultFoundation;
        }
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
