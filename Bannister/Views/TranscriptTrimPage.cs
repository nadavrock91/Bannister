using Bannister.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class TranscriptTrimPage : ContentPage
{
    private readonly List<TalkingHeadCutterPage.TranscriptWord> _transcriptWords;
    private readonly string _originalText;
    private readonly string? _videoPath;
    private readonly FFmpegService _ffmpeg;

    private readonly Entry _targetMinutesEntry;
    private readonly Editor _trimmedEditor;
    private readonly Label _trimmedWordCountLabel;
    private readonly Button _compareButton;

    public TranscriptTrimPage(
        IEnumerable<TalkingHeadCutterPage.TranscriptWord> transcriptWords,
        string? videoPath,
        FFmpegService ffmpeg)
    {
        _transcriptWords = transcriptWords.ToList();
        _originalText = string.Join(" ", _transcriptWords.Select(w => w.Word));
        _videoPath = videoPath;
        _ffmpeg = ffmpeg;

        Title = "Trim & Compare";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _targetMinutesEntry = CreateEntry("3");
        _trimmedEditor = CreateEditor("Paste trimmed transcript here...", 180);
        _trimmedEditor.TextChanged += (_, _) => UpdateTrimmedState();
        _trimmedWordCountLabel = CreateMutedLabel("0 words");
        _compareButton = CreateButton("Build Side-by-Side Comparison", Color.FromArgb("#00695C"));
        _compareButton.IsEnabled = false;
        _compareButton.Clicked += OnBuildComparisonClicked;

        BuildUI();
    }

    private void BuildUI()
    {
        var originalView = CreateEditor("", 260);
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
                    Text = "Trim & Compare",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#4A148C")
                },
                BuildCard("Trim Prompt", new View[]
                {
                    CreateLabeledEntry("Target duration (minutes)", _targetMinutesEntry),
                    CreateActionButton("Copy Trim Prompt", OnCopyTrimPromptClicked),
                    _trimmedEditor,
                    _trimmedWordCountLabel
                }),
                BuildCard("Original Transcript", new View[]
                {
                    originalView,
                    CreateMutedLabel($"{CountWords(_originalText)} words")
                }),
                BuildCard("Compare", new View[]
                {
                    _compareButton
                })
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private async void OnCopyTrimPromptClicked(object? sender, EventArgs e)
    {
        string targetMinutes = string.IsNullOrWhiteSpace(_targetMinutesEntry.Text)
            ? "3"
            : _targetMinutesEntry.Text.Trim();

        string prompt =
            $"Here is a spoken transcript. Trim it to approximately {targetMinutes} minutes of spoken content while preserving the most important ideas, the strongest opening hook, and the natural conclusion. Cut filler, repetition, and weaker tangents. Return ONLY the trimmed transcript as plain text with no timestamps, formatted as natural paragraphs.\n\n" +
            _originalText;

        await Clipboard.Default.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Trim prompt copied.", "OK");
    }

    private async void OnBuildComparisonClicked(object? sender, EventArgs e)
    {
        string revisedText = _trimmedEditor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(revisedText))
            return;

        var page = BuildComparisonPage(revisedText);
        await Navigation.PushAsync(page);
    }

    private void UpdateTrimmedState()
    {
        string text = _trimmedEditor.Text?.Trim() ?? "";
        _trimmedWordCountLabel.Text = $"{CountWords(text)} words";
        _compareButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
    }

    private ContentPage BuildComparisonPage(string revisedText)
    {
        var original = BuildOriginalSentenceItems();
        var revised = SplitSentenceItems(revisedText);
        var rows = AlignSentences(original, revised)
            .Select(row => new InteractiveComparisonRow(row))
            .ToList();

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

        var statsLabel = CreateMutedLabel("");
        var progressLabel = CreateMutedLabel("");
        var compileButton = CreateButton("Compile Video", Color.FromArgb("#00695C"));

        int gridRow = 1;
        Action refreshState = () =>
        {
            foreach (var row in rows)
                ApplyRowState(row);

            var enabledSentences = rows
                .Where(r => r.IsEnabled && r.Original?.Timestamp != null)
                .ToList();
            int totalSentences = rows.Count(r => r.Original?.Timestamp != null);
            var segments = BuildKeepSegmentsFromRows(rows);
            var estimatedDuration = TimeSpan.FromSeconds(segments.Sum(s => Math.Max(0, s.end - s.start)));
            statsLabel.Text = $"{enabledSentences.Count} of {totalSentences} sentences enabled | Estimated duration: {FormatDuration(estimatedDuration)}";
            compileButton.IsEnabled = segments.Count > 0
                && !string.IsNullOrWhiteSpace(_videoPath)
                && File.Exists(_videoPath);
        };

        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddInteractiveComparisonRow(grid, row, gridRow, refreshState);
            gridRow++;
        }

        compileButton.Clicked += async (_, _) =>
        {
            await CompileVideoAsync(rows, progressLabel);
            refreshState();
        };

        refreshState();

        return new ContentPage
        {
            Title = "Transcript Comparison",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 16,
                    Spacing = 12,
                    Children =
                    {
                        new Label
                        {
                            Text = "Transcript Comparison",
                            FontSize = 22,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#263238")
                        },
                        new HorizontalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                CreateLegendItem("Enabled", Color.FromArgb("#E8F5E9")),
                                CreateLegendItem("Disabled", Color.FromArgb("#ECEFF1"))
                            }
                        },
                        statsLabel,
                        grid,
                        compileButton,
                        progressLabel
                    }
                }
            }
        };
    }

    private static List<ComparisonRow> AlignSentences(List<SentenceItem> original, List<SentenceItem> revised)
    {
        int[,] lcs = BuildLcsTable(original, revised);
        var matches = BacktrackMatches(original, revised, lcs);
        var rows = new List<ComparisonRow>();
        int originalStart = 0;
        int revisedStart = 0;

        foreach (var match in matches)
        {
            AddUnmatchedRows(original, originalStart, match.originalIndex, revised, revisedStart, match.revisedIndex, rows);
            rows.Add(new ComparisonRow(original[match.originalIndex], revised[match.revisedIndex], "same"));
            originalStart = match.originalIndex + 1;
            revisedStart = match.revisedIndex + 1;
        }

        AddUnmatchedRows(original, originalStart, original.Count, revised, revisedStart, revised.Count, rows);
        return rows;
    }

    private static int[,] BuildLcsTable(List<SentenceItem> original, List<SentenceItem> revised)
    {
        int[,] table = new int[original.Count + 1, revised.Count + 1];
        for (int i = original.Count - 1; i >= 0; i--)
        {
            for (int j = revised.Count - 1; j >= 0; j--)
            {
                table[i, j] = NormalizeSentence(original[i].Text) == NormalizeSentence(revised[j].Text)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static List<(int originalIndex, int revisedIndex)> BacktrackMatches(List<SentenceItem> original, List<SentenceItem> revised, int[,] table)
    {
        var matches = new List<(int originalIndex, int revisedIndex)>();
        int i = 0;
        int j = 0;
        while (i < original.Count && j < revised.Count)
        {
            if (NormalizeSentence(original[i].Text) == NormalizeSentence(revised[j].Text))
            {
                matches.Add((i, j));
                i++;
                j++;
            }
            else if (table[i + 1, j] >= table[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return matches;
    }

    private static void AddUnmatchedRows(
        List<SentenceItem> original,
        int originalStart,
        int originalEnd,
        List<SentenceItem> revised,
        int revisedStart,
        int revisedEnd,
        List<ComparisonRow> rows)
    {
        var usedRevised = new HashSet<int>();
        for (int i = originalStart; i < originalEnd; i++)
        {
            int bestIndex = -1;
            double bestScore = 0;
            for (int j = revisedStart; j < revisedEnd; j++)
            {
                if (usedRevised.Contains(j))
                    continue;

                double score = WordOverlap(original[i].Text, revised[j].Text);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = j;
                }
            }

            if (bestIndex >= 0 && bestScore > 0.6)
            {
                usedRevised.Add(bestIndex);
                rows.Add(new ComparisonRow(original[i], revised[bestIndex], "similar"));
            }
            else
            {
                rows.Add(new ComparisonRow(original[i], null, "changed"));
            }
        }

        for (int j = revisedStart; j < revisedEnd; j++)
        {
            if (!usedRevised.Contains(j))
                rows.Add(new ComparisonRow(null, revised[j], "changed"));
        }
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

    private static List<SentenceItem> SplitSentenceItems(string text)
    {
        return Regex.Split(text.Trim(), @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new SentenceItem(s, null, null))
            .ToList();
    }

    private static bool IsSentenceEndingWord(string word) =>
        Regex.IsMatch(word.Trim(), @"[.!?][""')\]]*$");

    private static double WordOverlap(string left, string right)
    {
        var leftWords = GetComparableWords(left);
        var rightWords = GetComparableWords(right);
        if (leftWords.Count == 0 || rightWords.Count == 0)
            return 0;

        int overlap = leftWords.Intersect(rightWords).Count();
        return overlap / (double)Math.Max(leftWords.Count, rightWords.Count);
    }

    private static HashSet<string> GetComparableWords(string sentence) =>
        Regex.Matches(sentence.ToLowerInvariant(), @"\b[\w']+\b")
            .Select(m => m.Value)
            .ToHashSet();

    private static string NormalizeSentence(string sentence) =>
        string.Join(" ", Regex.Matches(sentence.ToLowerInvariant(), @"\b[\w']+\b").Select(m => m.Value));

    private static int CountWords(string text) =>
        Regex.Matches(text, @"\b[\w']+\b").Count;

    private static string BuildOriginalDisplay(InteractiveComparisonRow row)
    {
        if (row.Original == null)
            return "[--:--] [added]";

        return $"[{FormatTimestamp(row.Original.Timestamp)}] {row.Original.Text}";
    }

    private static string BuildRevisedDisplay(InteractiveComparisonRow row)
    {
        if (row.Revised == null)
            return "[--:--] [cut]";

        return $"[--:--] {row.Revised.Text}";
    }

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

    private static Color GetRowBackground(string kind) =>
        kind switch
        {
            "similar" => Color.FromArgb("#FFE0B2"),
            "changed" => Color.FromArgb("#FFCDD2"),
            _ => Colors.White
        };

    private static Color GetInteractiveRowBackground(InteractiveComparisonRow row) =>
        row.IsEnabled
            ? Color.FromArgb("#E8F5E9")
            : row.Original == null
                ? Color.FromArgb("#ECEFF1")
                : Color.FromArgb("#FFEBEE");

    private static List<(double start, double end)> BuildKeepSegmentsFromRows(List<InteractiveComparisonRow> rows)
    {
        var enabled = rows
            .Where(r => r.IsEnabled && r.Original?.Timestamp != null)
            .Select(r => r.Original!)
            .OrderBy(s => s.Timestamp)
            .ToList();

        var segments = new List<(double start, double end)>();
        if (enabled.Count == 0)
            return segments;

        double currentStart = enabled[0].Timestamp!.Value.TotalSeconds;
        double currentEnd = (enabled[0].EndTimestamp ?? enabled[0].Timestamp.Value.Add(TimeSpan.FromSeconds(3))).TotalSeconds;

        for (int i = 1; i < enabled.Count; i++)
        {
            var sentence = enabled[i];
            double start = sentence.Timestamp!.Value.TotalSeconds;
            double end = (sentence.EndTimestamp ?? sentence.Timestamp.Value.Add(TimeSpan.FromSeconds(3))).TotalSeconds;
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

    private async Task CompileVideoAsync(List<InteractiveComparisonRow> rows, Label progressLabel)
    {
        if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
        {
            await DisplayAlert("Video Missing", "Load a project with a selected video first.", "OK");
            return;
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
            string outputPath = Path.Combine(outputFolder, $"{Slugify(originalName)}_trimmed_comparison.mp4");

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

    private static void AddInteractiveComparisonRow(
        Grid grid,
        InteractiveComparisonRow row,
        int gridRow,
        Action refreshState)
    {
        row.IndicatorLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        row.OriginalLabel = CreateComparisonTextLabel(BuildOriginalDisplay(row));
        row.RevisedLabel = CreateComparisonTextLabel(BuildRevisedDisplay(row));

        row.IndicatorFrame = CreateComparisonFrame(row.IndicatorLabel);
        row.OriginalFrame = CreateComparisonFrame(row.OriginalLabel);
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

    private static void AddTap(Frame frame, InteractiveComparisonRow row, Action refreshState)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            row.IsEnabled = !row.IsEnabled;
            refreshState();
        };
        frame.GestureRecognizers.Add(tap);
    }

    private static void ApplyRowState(InteractiveComparisonRow row)
    {
        var background = GetInteractiveRowBackground(row);
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
        row.OriginalLabel.TextColor = row.IsEnabled
            ? Color.FromArgb("#1F2933")
            : Color.FromArgb("#607D8B");
        row.RevisedLabel.TextColor = row.IsEnabled
            ? Color.FromArgb("#1F2933")
            : Color.FromArgb("#607D8B");
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

    private static void AddComparisonCell(Grid grid, string text, string kind, int column, int row)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 14,
            TextColor = Color.FromArgb("#1F2933"),
            Padding = new Thickness(10),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var frame = new Frame
        {
            BackgroundColor = GetRowBackground(kind),
            BorderColor = Colors.Transparent,
            CornerRadius = 0,
            HasShadow = false,
            Padding = 0,
            Content = label
        };

        Grid.SetRow(frame, row);
        Grid.SetColumn(frame, column);
        grid.Children.Add(frame);
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

    private View CreateLabeledEntry(string label, Entry entry) =>
        new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#546E7A")
                },
                entry
            }
        };

    private Entry CreateEntry(string text) =>
        new()
        {
            Text = text,
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#777")
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
            TextColor = Color.FromArgb("#607D8B")
        };

    private sealed record SentenceItem(string Text, TimeSpan? Timestamp, TimeSpan? EndTimestamp);

    private sealed record ComparisonRow(SentenceItem? Original, SentenceItem? Revised, string Kind);

    private sealed class InteractiveComparisonRow
    {
        public InteractiveComparisonRow(ComparisonRow row)
        {
            Original = row.Original;
            Revised = row.Revised;
            Kind = row.Kind;
            IsEnabled = Original != null && Revised != null;
        }

        public SentenceItem? Original { get; }
        public SentenceItem? Revised { get; }
        public string Kind { get; }
        public bool IsEnabled { get; set; }
        public Frame IndicatorFrame { get; set; } = null!;
        public Frame OriginalFrame { get; set; } = null!;
        public Frame RevisedFrame { get; set; } = null!;
        public Label IndicatorLabel { get; set; } = null!;
        public Label OriginalLabel { get; set; } = null!;
        public Label RevisedLabel { get; set; } = null!;
    }
}
