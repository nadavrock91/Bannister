using System.Text.RegularExpressions;

namespace Bannister.Views;

public class TranscriptTrimPage : ContentPage
{
    private readonly List<TalkingHeadCutterPage.TranscriptWord> _transcriptWords;
    private readonly string _originalText;

    private readonly Entry _targetMinutesEntry;
    private readonly Editor _trimmedEditor;
    private readonly Label _trimmedWordCountLabel;
    private readonly Button _compareButton;

    public TranscriptTrimPage(IEnumerable<TalkingHeadCutterPage.TranscriptWord> transcriptWords)
    {
        _transcriptWords = transcriptWords.ToList();
        _originalText = string.Join(" ", _transcriptWords.Select(w => w.Word));

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
        var rows = AlignSentences(original, revised);

        var grid = new Grid
        {
            RowSpacing = 1,
            ColumnSpacing = 1,
            BackgroundColor = Color.FromArgb("#CFD8DC"),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddHeaderCell(grid, "Original", 0);
        AddHeaderCell(grid, "Revised", 1);

        int gridRow = 1;
        foreach (var row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddComparisonCell(grid, BuildOriginalDisplay(row), row.Kind, 0, gridRow);
            AddComparisonCell(grid, BuildRevisedDisplay(row), row.Kind, 1, gridRow);
            gridRow++;
        }

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
                                CreateLegendItem("Identical", Colors.White),
                                CreateLegendItem("Similar", Color.FromArgb("#FFE0B2")),
                                CreateLegendItem("Cut / Added", Color.FromArgb("#FFCDD2"))
                            }
                        },
                        grid
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
                sentences.Add(new SentenceItem(string.Join(" ", words).Trim(), start));
                words.Clear();
                start = null;
            }
        }

        if (words.Count > 0)
            sentences.Add(new SentenceItem(string.Join(" ", words).Trim(), start));

        return sentences;
    }

    private static List<SentenceItem> SplitSentenceItems(string text)
    {
        return Regex.Split(text.Trim(), @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new SentenceItem(s, null))
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

    private static string BuildOriginalDisplay(ComparisonRow row)
    {
        if (row.Original == null)
            return "[--:--] [added]";

        return $"[{FormatTimestamp(row.Original.Timestamp)}] {row.Original.Text}";
    }

    private static string BuildRevisedDisplay(ComparisonRow row)
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

    private static Color GetRowBackground(string kind) =>
        kind switch
        {
            "similar" => Color.FromArgb("#FFE0B2"),
            "changed" => Color.FromArgb("#FFCDD2"),
            _ => Colors.White
        };

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

    private sealed record SentenceItem(string Text, TimeSpan? Timestamp);

    private sealed record ComparisonRow(SentenceItem? Original, SentenceItem? Revised, string Kind);
}
