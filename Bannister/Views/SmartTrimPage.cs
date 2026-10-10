using Bannister.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class SmartTrimPage : ContentPage
{
    private readonly List<TalkingHeadCutterPage.TranscriptWord> _transcriptWords;
    private readonly Dictionary<int, int> _ratings;
    private readonly string _originalText;
    private string? _videoPath;
    private readonly FFmpegService _ffmpeg;
    private readonly Action<string>? _onVideoPathChanged;

    private readonly Editor _suggestionsEditor;
    private readonly Label _importStatusLabel;
    private readonly VerticalStackLayout _suggestionsStack;
    private readonly List<CutSuggestion> _suggestions = new();

    public SmartTrimPage(
        IEnumerable<TalkingHeadCutterPage.TranscriptWord> transcriptWords,
        string? videoPath,
        FFmpegService ffmpeg,
        Action<string>? onVideoPathChanged = null,
        Dictionary<int, int>? ratings = null)
    {
        _transcriptWords = transcriptWords.ToList();
        _ratings = ratings ?? new Dictionary<int, int>();
        _originalText = string.Join(" ", _transcriptWords.Select(w => w.Word));
        _videoPath = videoPath;
        _ffmpeg = ffmpeg;
        _onVideoPathChanged = onVideoPathChanged;

        Title = "Smart Trim";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _suggestionsEditor = CreateEditor("Paste cut[n] / reason[n] / save[n] response here...", 120);
        _importStatusLabel = CreateMutedLabel("No suggestions imported");
        _suggestionsStack = new VerticalStackLayout { Spacing = 8 };

        BuildUI();
    }

    private void BuildUI()
    {
        var originalView = CreateEditor("", 220);
        originalView.Text = _originalText;
        originalView.IsReadOnly = true;

        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14,
            Children =
            {
                new Label
                {
                    Text = "Smart Trim",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00695C")
                },
                BuildCard("Smart Trim Suggestions", new View[]
                {
                    CreateActionButton("Copy Smart Trim Prompt", OnCopySmartTrimPromptClicked),
                    CreateLabeledEditor("Paste LLM response", _suggestionsEditor),
                    CreateActionButton("Import Suggestions", OnImportSuggestionsClicked),
                    _importStatusLabel,
                    _suggestionsStack
                }),
                BuildCard("Original Transcript", new View[]
                {
                    originalView,
                    CreateMutedLabel($"{CountWords(_originalText)} words")
                }),
                BuildCard("Review & Compile", new View[]
                {
                    CreateActionButton("Open Smart Trim Review", OnOpenReviewClicked)
                })
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private async void OnCopySmartTrimPromptClicked(object? sender, EventArgs e)
    {
        var sentences = BuildOriginalSentenceItems();
        string prompt =
            "Here is a spoken transcript split into numbered sentences. Analyze it and suggest cuts ranked by priority \u2014 most impactful first. For each suggestion, give the sentence numbers to cut, a brief reason, and estimated seconds saved. A single useless sentence cut should rank higher than a long but meaningful passage.\n\n" +
            "Sentences:\n" +
            string.Join("\n", sentences.Select((sentence, index) => $"{index + 1}. {sentence.Text}")) +
            "\n\nReturn ONLY this format:\n" +
            "cut[1] = \"4,7,8\";\n" +
            "reason[1] = \"Filler tangent adds nothing\";\n" +
            "save[1] = \"18\";\n" +
            "cut[2] = \"15,16\";\n" +
            "reason[2] = \"Repetition of earlier point\";\n" +
            "save[2] = \"12\";\n" +
            "(continue for all suggestions, seconds only for save)";

        await Clipboard.Default.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Smart trim prompt copied.", "OK");
    }

    private void OnImportSuggestionsClicked(object? sender, EventArgs e)
    {
        _suggestions.Clear();
        _suggestions.AddRange(ParseSuggestions(_suggestionsEditor.Text ?? ""));
        RefreshSuggestionsList();
    }

    private async void OnOpenReviewClicked(object? sender, EventArgs e)
    {
        var page = BuildReviewPage();
        await Navigation.PushAsync(page);
    }

    private void RefreshSuggestionsList()
    {
        _suggestionsStack.Children.Clear();
        _importStatusLabel.Text = _suggestions.Count == 0
            ? "No suggestions imported"
            : $"{_suggestions.Count} suggestion(s) imported";

        foreach (var suggestion in _suggestions)
            _suggestionsStack.Children.Add(CreateMutedLabel($"Cut {suggestion.Indices.Count} sentences - save ~{suggestion.SaveSeconds}s | {suggestion.Reason}"));
    }

    private ContentPage BuildReviewPage()
    {
        var original = BuildOriginalSentenceItems();
        var rows = original
            .Select((sentence, index) =>
            {
                int number = index + 1;
                _ratings.TryGetValue(number, out int rating);
                return new SmartTrimRow(sentence, number, true, rating);
            })
            .ToList();

        var suggestionButtons = new List<(CutSuggestion suggestion, Button button)>();
        var suggestionsStack = new VerticalStackLayout { Spacing = 8 };
        var grid = new Grid
        {
            RowSpacing = 1,
            ColumnSpacing = 1,
            BackgroundColor = Color.FromArgb("#CFD8DC"),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 34 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddHeaderCell(grid, "", 0);
        AddHeaderCell(grid, "Original", 1);
        AddHeaderCell(grid, "Revised", 2);

        var statsLabel = CreateStatsLabel();
        var progressLabel = CreateMutedLabel("");
        var compileButton = CreateButton("Compile Video", Color.FromArgb("#00695C"));

        Action refreshState = () =>
        {
            foreach (var row in rows)
                ApplyRowState(row);

            foreach (var item in suggestionButtons)
                item.button.BackgroundColor = item.suggestion.IsApplied
                    ? Color.FromArgb("#78909C")
                    : Color.FromArgb("#2E7D32");

            var enabledSentences = rows
                .Where(r => r.IsEnabled && r.Original.Timestamp != null)
                .ToList();
            int totalSentences = rows.Count(r => r.Original.Timestamp != null);
            var segments = BuildKeepSegmentsFromRows(rows);
            var estimatedDuration = TimeSpan.FromSeconds(segments.Sum(s => Math.Max(0, s.end - s.start)));
            statsLabel.Text = $"{enabledSentences.Count} of {totalSentences} sentences enabled | Estimated duration: {FormatDuration(estimatedDuration)}";
            compileButton.IsEnabled = segments.Count > 0
                && !string.IsNullOrWhiteSpace(_videoPath)
                && File.Exists(_videoPath);
        };

        foreach (var suggestion in _suggestions)
        {
            var button = CreateButton(
                $"Cut {suggestion.Indices.Count} sentences - save ~{suggestion.SaveSeconds}s | {suggestion.Reason}",
                Color.FromArgb("#2E7D32"));
            button.Clicked += (_, _) =>
            {
                foreach (int index in suggestion.Indices)
                {
                    var row = rows.FirstOrDefault(r => r.Number == index);
                    if (row != null)
                        row.IsEnabled = false;
                }

                suggestion.IsApplied = true;
                refreshState();
            };
            suggestionButtons.Add((suggestion, button));
            suggestionsStack.Children.Add(button);
        }

        int gridRow = 1;
        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddInteractiveRow(grid, row, gridRow, refreshState);
            gridRow++;
        }

        refreshState();

        compileButton.Clicked += async (_, _) =>
        {
            await CompileVideoAsync(rows, progressLabel);
            refreshState();
        };

        var statsFrame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CFD8DC"),
            CornerRadius = 0,
            HasShadow = false,
            Padding = new Thickness(16, 10),
            Content = statsLabel
        };

        var comparisonScroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "Smart Trim Review",
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#263238")
                    },
                    suggestionsStack,
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            CreateLegendItem("Enabled", Color.FromArgb("#E8F5E9")),
                            CreateLegendItem("Disabled", Color.FromArgb("#ECEFF1"))
                        }
                    },
                    grid,
                    compileButton,
                    progressLabel
                }
            }
        };

        var contentGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        Grid.SetRow(statsFrame, 0);
        Grid.SetRow(comparisonScroll, 1);
        contentGrid.Children.Add(statsFrame);
        contentGrid.Children.Add(comparisonScroll);

        return new ContentPage
        {
            Title = "Smart Trim Review",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Content = contentGrid
        };
    }

    private List<SentenceItem> BuildOriginalSentenceItems()
    {
        var sentences = new List<SentenceItem>();
        var words = new List<string>();
        TimeSpan? start = null;

        foreach (var word in _transcriptWords)
        {
            if (start == null)
                start = word.Timestamp;

            words.Add(word.Word);
            if (IsSentenceEndingWord(word.Word))
            {
                sentences.Add(new SentenceItem(string.Join(" ", words).Trim(), start, null));
                words.Clear();
                start = null;
            }
        }

        if (words.Count > 0)
            sentences.Add(new SentenceItem(string.Join(" ", words).Trim(), start, null));

        for (int i = 0; i < sentences.Count; i++)
        {
            var end = i + 1 < sentences.Count && sentences[i + 1].Timestamp != null
                ? sentences[i + 1].Timestamp
                : sentences[i].Timestamp?.Add(TimeSpan.FromSeconds(3));
            sentences[i] = sentences[i] with { EndTimestamp = end };
        }

        return sentences;
    }

    private static List<CutSuggestion> ParseSuggestions(string text)
    {
        var suggestions = new List<CutSuggestion>();
        foreach (Match cutMatch in Regex.Matches(text, @"cut\[(\d+)\]\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase))
        {
            int id = int.Parse(cutMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            string indicesText = cutMatch.Groups[2].Value;
            var reasonMatch = Regex.Match(text, $@"reason\[{id}\]\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
            var saveMatch = Regex.Match(text, $@"save\[{id}\]\s*=\s*""(\d+)""", RegexOptions.IgnoreCase);

            var indices = Regex.Matches(indicesText, @"\d+")
                .Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture))
                .Where(n => n > 0)
                .Distinct()
                .ToList();
            if (indices.Count == 0)
                continue;

            suggestions.Add(new CutSuggestion
            {
                Indices = indices,
                Reason = reasonMatch.Success ? reasonMatch.Groups[1].Value : "Suggested cut",
                SaveSeconds = saveMatch.Success ? int.Parse(saveMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0
            });
        }

        return suggestions;
    }

    private static bool IsSentenceEndingWord(string word) =>
        Regex.IsMatch(word.Trim(), @"[.!?][""')\]]*$");

    private static int CountWords(string text) =>
        Regex.Matches(text, @"\b[\w']+\b").Count;

    private static string BuildOriginalDisplay(SmartTrimRow row) =>
        $"[{FormatTimestamp(row.Original.Timestamp)}] {row.Original.Text}";

    private static string BuildRevisedDisplay(SmartTrimRow row) =>
        row.IsEnabled ? row.Original.Text : "[cut]";

    private static string FormatTimestamp(TimeSpan? timestamp)
    {
        if (timestamp == null)
            return "--:--";

        int totalMinutes = (int)timestamp.Value.TotalMinutes;
        return $"{totalMinutes:00}:{timestamp.Value.Seconds:00}";
    }

    private static string FormatDuration(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private static string Slugify(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        return Regex.Replace(new string(chars), "_+", "_").Trim('_');
    }

    private static Color GetRowBackground(SmartTrimRow row) =>
        row.IsEnabled ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#ECEFF1");

    private static List<(double start, double end)> BuildKeepSegmentsFromRows(List<SmartTrimRow> rows)
    {
        var enabled = rows
            .Where(r => r.IsEnabled && r.Original.Timestamp != null)
            .Select(r => r.Original)
            .OrderBy(s => s.Timestamp)
            .ToList();

        var segments = new List<(double start, double end)>();
        if (enabled.Count == 0)
            return segments;

        TimeSpan firstStart = enabled[0].Timestamp ?? TimeSpan.Zero;
        double currentStart = firstStart.TotalSeconds;
        double currentEnd = GetSentenceEnd(enabled[0]).TotalSeconds;

        for (int i = 1; i < enabled.Count; i++)
        {
            var sentence = enabled[i];
            TimeSpan sentenceStart = sentence.Timestamp ?? TimeSpan.Zero;
            double start = sentenceStart.TotalSeconds;
            double end = GetSentenceEnd(sentence).TotalSeconds;
            double gap = Math.Max(0, start - currentEnd);

            if (gap > 1.5)
            {
                segments.Add((currentStart, currentEnd));
                currentStart = start;
            }

            currentEnd = Math.Max(currentEnd, end);
        }

        segments.Add((currentStart, currentEnd));
        return segments;
    }

    private static TimeSpan GetSentenceEnd(SentenceItem sentence)
    {
        TimeSpan start = sentence.Timestamp ?? TimeSpan.Zero;
        return sentence.EndTimestamp ?? start.Add(TimeSpan.FromSeconds(3));
    }

    private async Task CompileVideoAsync(List<SmartTrimRow> rows, Label progressLabel)
    {
        if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
        {
            bool pick = await DisplayAlert("No Video Selected", "No video is selected for this project. Would you like to pick one now?", "Pick Video", "Cancel");
            if (!pick) return;

            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp4", ".mov", ".avi", ".mkv" } }
            });
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose video file",
                FileTypes = fileTypes
            });
            if (fileResult == null) return;
            _videoPath = fileResult.FullPath;
            _onVideoPathChanged?.Invoke(_videoPath);
        }

        var segments = BuildKeepSegmentsFromRows(rows);
        if (segments.Count == 0)
        {
            await DisplayAlert("No Sentences", "Enable at least one timestamped sentence first.", "OK");
            return;
        }

        string? outputFolder = await WebsiteFolderHelper.PickParentFolderPathAsync(this);
        if (string.IsNullOrWhiteSpace(outputFolder))
            return;

        try
        {
            progressLabel.Text = "Preparing FFmpeg...";
            string ffmpegPath = await _ffmpeg.EnsureFFmpegAsync();
            string originalName = Path.GetFileNameWithoutExtension(_videoPath);
            string outputPath = Path.Combine(outputFolder, $"{Slugify(originalName)}_smart_trim.mp4");

            var progress = new Progress<double>(pct =>
            {
                progressLabel.Text = $"Compiling video... {pct * 100:0}%";
            });

            var result = await _ffmpeg.CutSegmentsAsync(
                ffmpegPath,
                _videoPath,
                outputPath,
                segments,
                progress);

            if (result.success)
            {
                progressLabel.Text = $"\u2713 Saved to {outputPath}";
                await DisplayAlert("Saved", $"Saved to:\n{outputPath}", "OK");
                return;
            }

            var errorLines = result.error.Split('\n');
            var lastLines = string.Join("\n", errorLines.TakeLast(5));
            progressLabel.Text = "Compile failed.";
            await DisplayAlert("FFmpeg Error", lastLines, "OK");
        }
        catch (Exception ex)
        {
            progressLabel.Text = "Compile failed.";
            await DisplayAlert("Compile Error", ex.Message, "OK");
        }
    }

    private static void AddInteractiveRow(Grid grid, SmartTrimRow row, int gridRow, Action refreshState)
    {
        row.IndicatorLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        row.NumberLabel = new Label
        {
            Text = row.Number.ToString(CultureInfo.InvariantCulture),
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            VerticalTextAlignment = TextAlignment.Center
        };
        row.RatingLabel = new Label
        {
            Text = row.Rating > 0 ? $"\u2605{row.Rating}" : "\u2605-",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Padding = new Thickness(7, 2),
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };
        row.OriginalLabel = CreateComparisonTextLabel(BuildOriginalDisplay(row));
        row.RevisedLabel = CreateComparisonTextLabel(BuildRevisedDisplay(row));

        row.IndicatorFrame = CreateComparisonFrame(row.IndicatorLabel);
        row.RatingFrame = CreateRatingFrame(row.RatingLabel, row.Rating);
        row.OriginalFrame = CreateComparisonFrame(new VerticalStackLayout
        {
            Padding = new Thickness(10),
            Spacing = 6,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        row.NumberLabel,
                        row.RatingFrame
                    }
                },
                row.OriginalLabel
            }
        });
        row.RevisedFrame = CreateComparisonFrame(row.RevisedLabel);

        AddTap(row.IndicatorFrame, row, refreshState);
        AddTap(row.OriginalFrame, row, refreshState);
        AddTap(row.RevisedFrame, row, refreshState);

        Grid.SetRow(row.IndicatorFrame, gridRow);
        Grid.SetColumn(row.IndicatorFrame, 0);
        Grid.SetRow(row.OriginalFrame, gridRow);
        Grid.SetColumn(row.OriginalFrame, 1);
        Grid.SetRow(row.RevisedFrame, gridRow);
        Grid.SetColumn(row.RevisedFrame, 2);
        grid.Children.Add(row.IndicatorFrame);
        grid.Children.Add(row.OriginalFrame);
        grid.Children.Add(row.RevisedFrame);
    }

    private static Label CreateComparisonTextLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 14,
            TextColor = Color.FromArgb("#1F2933"),
            Padding = new Thickness(10),
            LineBreakMode = LineBreakMode.WordWrap
        };

    private static Frame CreateComparisonFrame(View content) =>
        new()
        {
            BackgroundColor = Colors.White,
            BorderColor = Colors.Transparent,
            CornerRadius = 0,
            HasShadow = false,
            Padding = 0,
            Content = content
        };

    private static Frame CreateRatingFrame(View content, int rating) =>
        new()
        {
            BackgroundColor = GetRatingColor(rating),
            BorderColor = Colors.Transparent,
            CornerRadius = 8,
            HasShadow = false,
            Padding = 0,
            Content = content,
            VerticalOptions = LayoutOptions.Center
        };

    private static void AddTap(Frame frame, SmartTrimRow row, Action refreshState)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            row.IsEnabled = !row.IsEnabled;
            refreshState();
        };
        frame.GestureRecognizers.Add(tap);
    }

    private static void ApplyRowState(SmartTrimRow row)
    {
        var background = GetRowBackground(row);
        row.IndicatorFrame.BackgroundColor = background;
        row.OriginalFrame.BackgroundColor = background;
        row.RevisedFrame.BackgroundColor = background;
        row.IndicatorLabel.Text = row.IsEnabled ? "\u2713" : "\u2717";
        row.IndicatorLabel.TextColor = row.IsEnabled
            ? Color.FromArgb("#1B5E20")
            : Color.FromArgb("#B71C1C");

        var textDecorations = row.IsEnabled
            ? TextDecorations.None
            : TextDecorations.Strikethrough;
        row.OriginalLabel.TextDecorations = textDecorations;
        row.RevisedLabel.TextDecorations = textDecorations;
        row.RevisedLabel.Text = BuildRevisedDisplay(row);
        row.NumberLabel.TextColor = row.IsEnabled
            ? Color.FromArgb("#263238")
            : Color.FromArgb("#607D8B");
        row.OriginalLabel.TextColor = row.IsEnabled
            ? Color.FromArgb("#1F2933")
            : Color.FromArgb("#607D8B");
        row.RevisedLabel.TextColor = row.IsEnabled
            ? Color.FromArgb("#1F2933")
            : Color.FromArgb("#607D8B");
    }

    private static Color GetRatingColor(int rating)
    {
        if (rating >= 7)
            return Color.FromArgb("#2E7D32");
        if (rating >= 4)
            return Color.FromArgb("#EF6C00");
        if (rating >= 1)
            return Color.FromArgb("#C62828");
        return Color.FromArgb("#78909C");
    }

    private static void AddHeaderCell(Grid grid, string text, int column)
    {
        var label = new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            TextColor = Colors.White,
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#263238")
        };

        Grid.SetRow(label, 0);
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    private static View CreateLegendItem(string text, Color color) =>
        new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new BoxView
                {
                    Color = color,
                    WidthRequest = 14,
                    HeightRequest = 14,
                    VerticalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = text,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#455A64"),
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

    private Frame BuildCard(string title, IEnumerable<View> children)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#263238")
                }
            }
        };

        foreach (var child in children)
            stack.Children.Add(child);

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CFD8DC"),
            CornerRadius = 10,
            HasShadow = false,
            Padding = 14,
            Content = stack
        };
    }

    private View CreateLabeledEditor(string label, Editor editor) =>
        new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#546E7A"),
                    LineBreakMode = LineBreakMode.WordWrap
                },
                editor
            }
        };

    private Editor CreateEditor(string placeholder, double height) =>
        new()
        {
            Placeholder = placeholder,
            HeightRequest = height,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#777")
        };

    private Button CreateActionButton(string text, EventHandler clicked)
    {
        var button = CreateButton(text, Color.FromArgb("#1565C0"));
        button.Clicked += clicked;
        return button;
    }

    private static Button CreateButton(string text, Color color) =>
        new()
        {
            Text = text,
            BackgroundColor = color,
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(12, 10)
        };

    private static Label CreateMutedLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 13,
            TextColor = Color.FromArgb("#607D8B"),
            LineBreakMode = LineBreakMode.WordWrap
        };

    private static Label CreateStatsLabel() =>
        new()
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        };

    private sealed record SentenceItem(string Text, TimeSpan? Timestamp, TimeSpan? EndTimestamp);

    private sealed class CutSuggestion
    {
        public List<int> Indices { get; set; } = new();
        public string Reason { get; set; } = "";
        public int SaveSeconds { get; set; }
        public bool IsApplied { get; set; }
    }

    private sealed class SmartTrimRow
    {
        public SmartTrimRow(SentenceItem original, int number, bool isEnabled, int rating)
        {
            Original = original;
            Number = number;
            IsEnabled = isEnabled;
            Rating = rating;
        }

        public SentenceItem Original { get; }
        public int Number { get; }
        public bool IsEnabled { get; set; }
        public int Rating { get; }
        public Frame IndicatorFrame { get; set; } = null!;
        public Frame OriginalFrame { get; set; } = null!;
        public Frame RevisedFrame { get; set; } = null!;
        public Frame RatingFrame { get; set; } = null!;
        public Label IndicatorLabel { get; set; } = null!;
        public Label NumberLabel { get; set; } = null!;
        public Label RatingLabel { get; set; } = null!;
        public Label OriginalLabel { get; set; } = null!;
        public Label RevisedLabel { get; set; } = null!;
    }
}
