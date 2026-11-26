using Bannister.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class TranscriptTrimPage : Bannister.Views.ModeThemedPage
{
    private readonly List<TalkingHeadCutterPage.TranscriptWord> _transcriptWords;
    private readonly string _originalText;
    private string? _videoPath;
    private readonly FFmpegService _ffmpeg;
    private readonly Action<string>? _onVideoPathChanged;
    private readonly Action<string, string>? _onKeepDataChanged;

    private readonly Entry _targetMinutesEntry;
    private readonly Editor _keepListEntry;
    private readonly Label _keepListStatusLabel;
    private readonly Button _compareButton;
    private readonly Button _smartTrimButton;

    public TranscriptTrimPage(
        IEnumerable<TalkingHeadCutterPage.TranscriptWord> transcriptWords,
        string? videoPath,
        FFmpegService ffmpeg,
        Action<string>? onVideoPathChanged = null,
        Action<string, string>? onKeepDataChanged = null,
        string? initialKeepData = null)
    {
        _transcriptWords = transcriptWords.ToList();
        _originalText = string.Join(" ", _transcriptWords.Select(w => w.Word));
        _videoPath = videoPath;
        _ffmpeg = ffmpeg;
        _onVideoPathChanged = onVideoPathChanged;
        _onKeepDataChanged = onKeepDataChanged;

        Title = "Trim & Compare";


        _targetMinutesEntry = CreateEntry("3");
        _keepListEntry = CreateEditor("Paste keep = 1,3,5...\nratings = 1:8,2:7...", 120);
        _keepListEntry.TextChanged += (_, _) => UpdateKeepListState();
        _keepListStatusLabel = CreateMutedLabel("Paste the sentence numbers to keep.");
        _compareButton = CreateButton("Build Comparison", ThemeKey.HighlightMintText);
        _compareButton.IsEnabled = false;
        _compareButton.Clicked += OnBuildComparisonClicked;
        _smartTrimButton = CreateButton("Smart Trim", ThemeKey.ButtonPrimaryBackground);
        _smartTrimButton.IsEnabled = false;
        _smartTrimButton.Clicked += OnSmartTrimClicked;

        BuildUI();
        if (!string.IsNullOrWhiteSpace(initialKeepData))
            _keepListEntry.Text = initialKeepData;
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
                (new Label
                {
                    Text = "Trim & Compare",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold
                }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true),
                BuildCard("Trim Prompt", new View[]
                {
                    CreateLabeledEntry("Target duration (minutes)", _targetMinutesEntry),
                    CreateActionButton("Copy Trim Prompt", OnCopyTrimPromptClicked),
                    CreateLabeledEditor("Paste the LLM response here (both lines):\nkeep = 1,2,3...\nratings = 1:8,2:7...", _keepListEntry),
                    _keepListStatusLabel
                }),
                BuildCard("Original Transcript", new View[]
                {
                    originalView,
                    CreateMutedLabel($"{CountWords(_originalText)} words")
                }),
                BuildCard("Compare", new View[]
                {
                    _compareButton,
                    _smartTrimButton
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

        var sentences = BuildOriginalSentenceItems();
        string prompt =
            $"Here is a spoken transcript split into numbered sentences. I want to trim it to approximately {targetMinutes} minutes. Return ONLY a comma-separated list of sentence numbers to KEEP \u2014 the ones that form the best version. Keep the strongest hook, core ideas, and natural conclusion. Cut filler, repetition, and weaker tangents.\n\n" +
            "Sentences:\n" +
            string.Join("\n", sentences.Select((sentence, index) => $"{index + 1}. {sentence.Text}")) +
            "\n\nReturn format:\n" +
            "keep = 1,3,5,7...\n" +
            "ratings = 1:8,2:3,3:7,4:5... (sentence number : rating 1-10 for ALL sentences)";

        await Clipboard.Default.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Trim prompt copied.", "OK");
    }

    private async void OnBuildComparisonClicked(object? sender, EventArgs e)
    {
        string responseText = _keepListEntry.Text ?? "";
        var selection = ParseTrimResponse(responseText);
        if (selection.KeepNumbers.Count == 0)
        {
            await DisplayAlert("No Sentences", "Paste a comma-separated keep list first.", "OK");
            return;
        }

        var page = BuildComparisonPage(selection);
        await Navigation.PushAsync(page);

        string keepLine = Regex.Match(responseText, @"(?im)^\s*keep\s*=.*$").Value.Trim();
        string ratingsLine = Regex.Match(responseText, @"(?im)^\s*ratings\s*=.*$").Value.Trim();
        _onKeepDataChanged?.Invoke(keepLine, ratingsLine);
    }

    private async void OnSmartTrimClicked(object? sender, EventArgs e)
    {
        var selection = ParseTrimResponse(_keepListEntry.Text ?? "");
        await Navigation.PushAsync(new SmartTrimPage(_transcriptWords, _videoPath, _ffmpeg, path =>
        {
            _videoPath = path;
            _onVideoPathChanged?.Invoke(path);
        }, selection.Ratings));
    }

    private void UpdateKeepListState()
    {
        var selection = ParseTrimResponse(_keepListEntry.Text ?? "");
        _keepListStatusLabel.Text = selection.KeepNumbers.Count == 0
            ? "Paste the sentence numbers to keep."
            : $"{selection.KeepNumbers.Count} sentence number(s) selected | {selection.Ratings.Count} rating(s)";
        _compareButton.IsEnabled = selection.KeepNumbers.Count > 0;
        _smartTrimButton.IsEnabled = selection.Ratings.Count > 0;
    }

    private ContentPage BuildComparisonPage(TrimSelection selection)
    {
        var original = BuildOriginalSentenceItems();
        var rows = original
            .Select((sentence, index) =>
            {
                int number = index + 1;
                selection.Ratings.TryGetValue(number, out int rating);
                return new InteractiveComparisonRow(sentence, number, selection.KeepNumbers.Contains(number), rating);
            })
            .ToList();

        var grid = (new Grid
        {
            RowSpacing = 1,
            ColumnSpacing = 1,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 34 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator);

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddHeaderCell(grid, "", 0);
        AddHeaderCell(grid, "Original", 1);
        AddHeaderCell(grid, "Revised", 2);

        var statsLabel = CreateStatsLabel();
        var progressLabel = CreateMutedLabel("");
        var compileButton = CreateButton("Compile Video", ThemeKey.HighlightMintText);

        int gridRow = 1;
        Action refreshState = () =>
        {
            foreach (var row in rows)
                ApplyRowState(row);

            var enabledWithTimestamps = rows
                .Where(r => r.IsEnabled && r.Original.Timestamp != null)
                .ToList();
            int totalSentences = rows.Count(r => r.Original.Timestamp != null);
            var segments = BuildKeepSegmentsFromRows(rows);
            if (enabledWithTimestamps.Count == 0)
            {
                statsLabel.Text = "0 sentences enabled";
            }
            else
            {
                var lastTimestamp = rows
                    .Where(r => r.Original.Timestamp != null)
                    .Max(r => r.Original.Timestamp!.Value.TotalSeconds);
                var durationCap = lastTimestamp + 60;
                var totalDuration = TimeSpan.FromSeconds(
                    segments.Sum(s => Math.Max(0, Math.Min(s.end, durationCap) - s.start)));
                var spokenDuration = TimeSpan.FromSeconds(enabledWithTimestamps.Sum(r =>
                    Math.Max(0, (r.Original.LastWordTimestamp!.Value - r.Original.Timestamp!.Value).TotalSeconds)));
                var silenceDuration = totalDuration - spokenDuration;
                statsLabel.Text = $"{enabledWithTimestamps.Count} of {totalSentences} sentences enabled | Total: {FormatDuration(totalDuration)} (spoken: {FormatDuration(spokenDuration)}, silence: {FormatDuration(silenceDuration)})";
            }
            System.Diagnostics.Debug.WriteLine($"[REFRESH_STATE] segments={segments.Count}, videoPath={_videoPath}, fileExists={File.Exists(_videoPath ?? "")}");
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

        refreshState();

        compileButton.Clicked += async (_, _) =>
        {
            System.Diagnostics.Debug.WriteLine($"[COMPILE_BTN] clicked, IsEnabled={compileButton.IsEnabled}, videoPath={_videoPath}");
            await CompileVideoAsync(rows, progressLabel);
            refreshState();
        };

        var statsFrame = (new Frame
        {
            CornerRadius = 0,
            HasShadow = false,
            Padding = new Thickness(16, 10),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    statsLabel,
                    (new Label
                    {
                        Text = "⚠️ Recommended workflow: compile first, then truncate silence on the result.",
                        FontSize = 12
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningStrongText, ThemeKey.PageText3, classifyPageText: true)
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var comparisonScroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    (new Label
                    {
                        Text = "Transcript Comparison",
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            CreateLegendItem("Enabled", ThemeKey.SuccessBackground),
                            CreateLegendItem("Disabled", ThemeKey.RowAltBackground)
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

        return (new ContentPage
        {
            Title = "Transcript Comparison",
            Content = contentGrid
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
    }

    private List<SentenceItem> BuildOriginalSentenceItems()
    {
        var sentences = new List<SentenceItem>();
        var words = new List<string>();
        TimeSpan? start = null;
        TimeSpan? lastWordTimestamp = null;

        foreach (var word in _transcriptWords)
        {
            if (start == null)
                start = word.Timestamp;

            lastWordTimestamp = word.Timestamp;
            words.Add(word.Word);
            if (IsSentenceEndingWord(word.Word))
            {
                sentences.Add(new SentenceItem(string.Join(" ", words).Trim(), start, lastWordTimestamp, null));
                words.Clear();
                start = null;
                lastWordTimestamp = null;
            }
        }

        if (words.Count > 0)
            sentences.Add(new SentenceItem(string.Join(" ", words).Trim(), start, lastWordTimestamp, null));

        for (int i = 0; i < sentences.Count; i++)
        {
            var end = i + 1 < sentences.Count && sentences[i + 1].Timestamp != null
                ? sentences[i + 1].Timestamp
                : sentences[i].Timestamp?.Add(TimeSpan.FromSeconds(3));
            sentences[i] = sentences[i] with { EndTimestamp = end };
        }

        return sentences;
    }

    private static bool IsSentenceEndingWord(string word) =>
        Regex.IsMatch(word.Trim(), @"[.!?][""')\]]*$");

    private static int CountWords(string text) =>
        Regex.Matches(text, @"\b[\w']+\b").Count;

    private static TrimSelection ParseTrimResponse(string text)
    {
        var keepMatch = Regex.Match(
            text,
            @"keep\s*=\s*(.*?)(?:\r?\n|ratings\s*=|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        string keepText = keepMatch.Success ? keepMatch.Groups[1].Value : text;
        var keepNumbers = Regex.Matches(keepText, @"\d+")
            .Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture))
            .Where(n => n > 0)
            .ToHashSet();

        var ratings = new Dictionary<int, int>();
        var ratingsMatch = Regex.Match(text, @"ratings\s*=\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (ratingsMatch.Success)
        {
            foreach (Match ratingMatch in Regex.Matches(ratingsMatch.Groups[1].Value, @"(\d+)\s*:\s*(10|[1-9])"))
            {
                int sentenceNumber = int.Parse(ratingMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                int rating = int.Parse(ratingMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                ratings[sentenceNumber] = rating;
            }
        }

        return new TrimSelection(keepNumbers, ratings);
    }

    private static string BuildOriginalDisplay(InteractiveComparisonRow row)
    {
        return $"[{FormatTimestamp(row.Original.Timestamp)}] {row.Original.Text}";
    }

    private static string BuildRevisedDisplay(InteractiveComparisonRow row)
    {
        if (!row.IsEnabled)
            return "[cut]";

        return row.Original.Text;
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

    private static ThemeKey GetInteractiveRowBackground(InteractiveComparisonRow row) =>
        row.IsEnabled ? ThemeKey.SuccessBackground : ThemeKey.RowAltBackground;

    private static List<(double start, double end)> BuildKeepSegmentsFromRows(List<InteractiveComparisonRow> rows)
    {
        var sentences = rows
            .Select(r => r.Original)
            .OrderBy(s => s.Timestamp)
            .ToList();
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

        int lastEnabledIndex = sentences.IndexOf(enabled[^1]);
        currentEnd = lastEnabledIndex == sentences.Count - 1
            ? 99999.0
            : sentences[lastEnabledIndex + 1].Timestamp?.TotalSeconds ?? currentEnd;

        segments.Add((currentStart, currentEnd));
        return segments;
    }

    private static TimeSpan GetSentenceEnd(SentenceItem sentence)
    {
        TimeSpan start = sentence.Timestamp ?? TimeSpan.Zero;
        return sentence.EndTimestamp ?? start.Add(TimeSpan.FromSeconds(3));
    }

    private async Task CompileVideoAsync(List<InteractiveComparisonRow> rows, Label progressLabel)
    {
        System.Diagnostics.Debug.WriteLine($"[COMPILE] videoPath={_videoPath}, exists={File.Exists(_videoPath ?? "")}");
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
            if (_onVideoPathChanged != null)
                _onVideoPathChanged(_videoPath);
        }

        var segments = BuildKeepSegmentsFromRows(rows);
        System.Diagnostics.Debug.WriteLine($"[COMPILE] segments count={segments.Count}");
        if (segments.Count == 0)
        {
            await DisplayAlert("No Sentences", "Enable at least one timestamped sentence first.", "OK");
            return;
        }

        string? outputFolder = await WebsiteFolderHelper.PickParentFolderPathAsync(this);
        System.Diagnostics.Debug.WriteLine($"[COMPILE] outputFolder={outputFolder}");
        if (string.IsNullOrWhiteSpace(outputFolder))
            return;

        try
        {
            progressLabel.Text = "Preparing FFmpeg...";
            string ffmpegPath = await _ffmpeg.EnsureFFmpegAsync();
            System.Diagnostics.Debug.WriteLine($"[COMPILE] ffmpegPath={ffmpegPath}");
            string originalName = Path.GetFileNameWithoutExtension(_videoPath);
            string outputPath = Path.Combine(outputFolder, $"{Slugify(originalName)}_trimmed_comparison.mp4");

            var progress = new Progress<double>(pct =>
            {
                progressLabel.Text = $"Compiling video... {pct * 100:0}%";
            });

            System.Diagnostics.Debug.WriteLine($"[COMPILE] calling CutSegmentsAsync");
            var result = await _ffmpeg.CutSegmentsAsync(
                ffmpegPath,
                _videoPath,
                outputPath,
                segments,
                progress);
            System.Diagnostics.Debug.WriteLine($"[COMPILE] result.success={result.success}");

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
        row.NumberLabel = (new Label
        {
            Text = row.Number.ToString(CultureInfo.InvariantCulture),
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        row.RatingLabel = (new Label
        {
            Text = row.Rating > 0 ? $"\u2605{row.Rating}" : "\u2605-",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(7, 2),
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText3, classifyPageText: true);
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
        (new Label(){
            Text = text,
            FontSize = 14,
            Padding = new Thickness(10),
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);

    private static Frame CreateComparisonFrame(View content) =>
        (new Frame(){
            BorderColor = Colors.Transparent,
            CornerRadius = 0,
            HasShadow = false,
            Padding = 0,
            Content = content
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

    private static Frame CreateRatingFrame(View content, int rating) =>
        (new Frame(){
            BorderColor = Colors.Transparent,
            CornerRadius = 8,
            HasShadow = false,
            Padding = 0,
            Content = content,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, GetRatingColor(rating));

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
        row.IndicatorFrame.ThemeColor(VisualElement.BackgroundColorProperty, background);
        row.OriginalFrame.ThemeColor(VisualElement.BackgroundColorProperty, background);
        row.RevisedFrame.ThemeColor(VisualElement.BackgroundColorProperty, background);
        row.IndicatorLabel.Text = row.IsEnabled ? "\u2713" : "\u2717";
        row.IndicatorLabel.ThemeColor(Label.TextColorProperty, row.IsEnabled ? ThemeKey.SuccessStrongText : ThemeKey.ErrorDeepText, ThemeKey.PageText1, classifyPageText: true);

        var textDecorations = row.IsEnabled
            ? TextDecorations.None
            : TextDecorations.Strikethrough;
        row.OriginalLabel.TextDecorations = textDecorations;
        row.RevisedLabel.TextDecorations = textDecorations;
        row.RevisedLabel.Text = BuildRevisedDisplay(row);
        row.NumberLabel.ThemeColor(Label.TextColorProperty, row.IsEnabled ? ThemeKey.CardText1 : ThemeKey.NeutralMutedButton, ThemeKey.PageText1, classifyPageText: true);
        row.OriginalLabel.ThemeColor(Label.TextColorProperty, row.IsEnabled ? ThemeKey.CardText1 : ThemeKey.NeutralMutedButton, ThemeKey.PageText1, classifyPageText: true);
        row.RevisedLabel.ThemeColor(Label.TextColorProperty, row.IsEnabled ? ThemeKey.CardText1 : ThemeKey.NeutralMutedButton, ThemeKey.PageText1, classifyPageText: true);
    }

    private static ThemeKey GetRatingColor(int rating)
    {
        if (rating >= 7)
            return ThemeKey.SuccessText;
        if (rating >= 4)
            return ThemeKey.WarningAmber;
        if (rating >= 1)
            return ThemeKey.ErrorText;
        return ThemeKey.NeutralMutedButton;
    }

    private static void AddHeaderCell(Grid grid, string text, int column)
    {
        var label = (new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            Padding = new Thickness(12, 10)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText1, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralStrongButtonBackground);

        Grid.SetRow(label, 0);
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    private static View CreateLegendItem(string text, ThemeKey color) =>
        new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                (new BoxView
                {
                    WidthRequest = 14,
                    HeightRequest = 14,
                    VerticalOptions = LayoutOptions.Center
                }).ThemeColor(BoxView.ColorProperty, color),
                (new Label
                {
                    Text = text,
                    FontSize = 13,
                    VerticalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.SlateText, ThemeKey.PageText1, classifyPageText: true)
            }
        };

    private Frame BuildCard(string title, IEnumerable<View> children)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                (new Label
                {
                    Text = title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true)
            }
        };

        foreach (var child in children)
            stack.Children.Add(child);

        return (new Frame
        {
            CornerRadius = 10,
            HasShadow = false,
            Padding = 14,
            Content = stack
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
    }

    private View CreateLabeledEntry(string label, Entry entry) =>
        new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                (new Label
                {
                    Text = label,
                    FontSize = 13
                }).ThemeColor(Label.TextColorProperty, ThemeKey.NeutralMutedButton, ThemeKey.PageText1, classifyPageText: true),
                entry
            }
        };

    private View CreateLabeledEditor(string label, Editor editor) =>
        new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                (new Label
                {
                    Text = label,
                    FontSize = 13,
                    LineBreakMode = LineBreakMode.WordWrap
                }).ThemeColor(Label.TextColorProperty, ThemeKey.NeutralMutedButton, ThemeKey.PageText1, classifyPageText: true),
                editor
            }
        };

    private Entry CreateEntry(string text) =>
        (new Entry(){
            Text = text,
            Keyboard = Keyboard.Numeric
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);

    private Entry CreateTextEntry(string placeholder) =>
        (new Entry(){
            Placeholder = placeholder
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);

    private Editor CreateEditor(string placeholder, double height) =>
        (new Editor(){
            Placeholder = placeholder,
            HeightRequest = height,
            AutoSize = EditorAutoSizeOption.Disabled
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);

    private Button CreateActionButton(string text, EventHandler clicked)
    {
        var button = CreateButton(text, ThemeKey.ButtonPrimaryBackground);
        button.Clicked += clicked;
        return button;
    }

    private static Button CreateButton(string text, ThemeKey color) =>
        (new Button(){
            Text = text,
            CornerRadius = 8,
            Padding = new Thickness(12, 10)
        }).ThemeColor(VisualElement.BackgroundColorProperty, color).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);

    private static Label CreateMutedLabel(string text) =>
        (new Label(){
            Text = text,
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.NeutralMutedButton, ThemeKey.PageText1, classifyPageText: true);

    private static Label CreateStatsLabel() =>
        (new Label(){
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);

    private sealed record SentenceItem(string Text, TimeSpan? Timestamp, TimeSpan? LastWordTimestamp, TimeSpan? EndTimestamp);

    private sealed record TrimSelection(HashSet<int> KeepNumbers, Dictionary<int, int> Ratings);

    private sealed class InteractiveComparisonRow
    {
        public InteractiveComparisonRow(SentenceItem original, int number, bool isEnabled, int rating)
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
