using System.Net;
using System.Text;
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

        string html = BuildComparisonHtml(_originalText, revisedText);
        string tempPath = Path.Combine(FileSystem.CacheDirectory, "comparison.html");
        File.WriteAllText(tempPath, html, Encoding.UTF8);
        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(tempPath)
        });
    }

    private void UpdateTrimmedState()
    {
        string text = _trimmedEditor.Text?.Trim() ?? "";
        _trimmedWordCountLabel.Text = $"{CountWords(text)} words";
        _compareButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
    }

    private string BuildComparisonHtml(string originalText, string revisedText)
    {
        var original = SplitSentences(originalText);
        var revised = SplitSentences(revisedText);
        var rows = AlignSentences(original, revised);

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<title>Transcript Comparison</title>");
        builder.AppendLine("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        builder.AppendLine("<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        builder.AppendLine("<link href=\"https://fonts.googleapis.com/css2?family=Source+Serif+4:wght@400;600;700&display=swap\" rel=\"stylesheet\">");
        builder.AppendLine("<style>");
        builder.AppendLine("""
            :root {
                --bg: #111318;
                --panel: #1b1f27;
                --line: #343b49;
                --text: #f2efe9;
                --muted: #b9b1a7;
                --same: #f7f1e7;
                --similar: #f0a24a;
                --changed: #c8403b;
            }
            * { box-sizing: border-box; }
            body {
                margin: 0;
                background: var(--bg);
                color: var(--text);
                font-family: 'Source Serif 4', Georgia, serif;
                line-height: 1.5;
            }
            header {
                position: sticky;
                top: 0;
                z-index: 3;
                background: rgba(17, 19, 24, .96);
                border-bottom: 1px solid var(--line);
                padding: 18px 22px;
            }
            h1 { margin: 0 0 12px; font-size: 28px; }
            .legend { display: flex; gap: 14px; flex-wrap: wrap; color: var(--muted); font-size: 15px; }
            .swatch { display: inline-flex; align-items: center; gap: 7px; }
            .box { width: 14px; height: 14px; border-radius: 3px; display: inline-block; }
            .box.same { background: var(--same); }
            .box.similar { background: var(--similar); }
            .box.changed { background: var(--changed); }
            main { padding: 18px 22px 32px; }
            .grid {
                display: grid;
                grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
                border: 1px solid var(--line);
                border-radius: 8px;
                overflow: clip;
                background: var(--panel);
            }
            .head {
                position: sticky;
                top: 92px;
                z-index: 2;
                background: #252b36;
                color: var(--text);
                font-weight: 700;
                padding: 12px 16px;
                border-bottom: 1px solid var(--line);
            }
            .left { border-right: 1px solid var(--line); }
            .cell {
                min-height: 62px;
                padding: 12px 16px;
                border-bottom: 1px solid var(--line);
                white-space: pre-wrap;
            }
            .same { color: var(--same); }
            .similar { color: var(--similar); }
            .changed { color: var(--changed); }
            .placeholder { opacity: .72; font-style: italic; }
            .section {
                grid-column: 1 / -1;
                padding: 9px 16px;
                background: #101216;
                color: var(--muted);
                border-bottom: 1px solid var(--line);
                font-size: 13px;
                text-transform: uppercase;
                letter-spacing: .08em;
            }
            @media (max-width: 760px) {
                .grid { grid-template-columns: 1fr; }
                .left { border-right: 0; }
                .head { position: static; }
            }
            """);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<header>");
        builder.AppendLine("<h1>Transcript Comparison</h1>");
        builder.AppendLine("<div class=\"legend\">");
        builder.AppendLine("<span class=\"swatch\"><span class=\"box same\"></span>White: identical</span>");
        builder.AppendLine("<span class=\"swatch\"><span class=\"box similar\"></span>Orange: same content, different wording</span>");
        builder.AppendLine("<span class=\"swatch\"><span class=\"box changed\"></span>Red: cut or added</span>");
        builder.AppendLine("</div>");
        builder.AppendLine("</header>");
        builder.AppendLine("<main><div class=\"grid\">");
        builder.AppendLine("<div class=\"head left\">Original</div><div class=\"head\">Revised</div>");

        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0 && i % 10 == 0)
                builder.AppendLine($"<div class=\"section\">Section {i / 10 + 1}</div>");

            var row = rows[i];
            builder.AppendLine($"<div class=\"cell left {row.Kind}\">{EncodeCell(row.Original)}</div>");
            builder.AppendLine($"<div class=\"cell {row.Kind}\">{EncodeCell(row.Revised)}</div>");
        }

        builder.AppendLine("</div></main>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static List<ComparisonRow> AlignSentences(List<string> original, List<string> revised)
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

    private static int[,] BuildLcsTable(List<string> original, List<string> revised)
    {
        int[,] table = new int[original.Count + 1, revised.Count + 1];
        for (int i = original.Count - 1; i >= 0; i--)
        {
            for (int j = revised.Count - 1; j >= 0; j--)
            {
                table[i, j] = NormalizeSentence(original[i]) == NormalizeSentence(revised[j])
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static List<(int originalIndex, int revisedIndex)> BacktrackMatches(List<string> original, List<string> revised, int[,] table)
    {
        var matches = new List<(int originalIndex, int revisedIndex)>();
        int i = 0;
        int j = 0;
        while (i < original.Count && j < revised.Count)
        {
            if (NormalizeSentence(original[i]) == NormalizeSentence(revised[j]))
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
        List<string> original,
        int originalStart,
        int originalEnd,
        List<string> revised,
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

                double score = WordOverlap(original[i], revised[j]);
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
                rows.Add(new ComparisonRow(original[i], "[cut]", "changed"));
            }
        }

        for (int j = revisedStart; j < revisedEnd; j++)
        {
            if (!usedRevised.Contains(j))
                rows.Add(new ComparisonRow("[added]", revised[j], "changed"));
        }
    }

    private static List<string> SplitSentences(string text)
    {
        return Regex.Split(text.Trim(), @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

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

    private static string EncodeCell(string text)
    {
        string encoded = WebUtility.HtmlEncode(text);
        if (text is "[cut]" or "[added]")
            return $"<span class=\"placeholder\">{encoded}</span>";
        return encoded;
    }

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

    private sealed record ComparisonRow(string Original, string Revised, string Kind);
}
