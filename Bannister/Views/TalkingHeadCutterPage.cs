using Bannister.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class TalkingHeadCutterPage : ContentPage
{
    private readonly FFmpegService _ffmpeg;
    private readonly List<TranscriptWord> _transcriptWords = new();
    private readonly List<ProposedCut> _cuts = new();

    private string? _selectedVideoPath;
    private string? _outputFolder;

    private readonly Label _selectedFileLabel;
    private readonly Editor _transcriptEditor;
    private readonly Label _transcriptStatusLabel;
    private readonly Button _copyAnalysisPromptButton;
    private readonly Editor _shortsEditor;
    private readonly VerticalStackLayout _cutsList;
    private readonly Label _outputFolderLabel;
    private readonly Label _progressLabel;
    private readonly Button _cutAllButton;

    public TalkingHeadCutterPage(FFmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;

        Title = "Talking Head Shorts Cutter";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _selectedFileLabel = CreateMutedLabel("No video selected");
        _transcriptEditor = CreateEditor("Paste timestamped words here...", 180);
        _transcriptStatusLabel = CreateMutedLabel("No transcript imported");
        _copyAnalysisPromptButton = CreateButton("Copy Shorts Analysis Prompt", Color.FromArgb("#1565C0"));
        _copyAnalysisPromptButton.IsEnabled = false;
        _shortsEditor = CreateEditor("Paste suggested shorts here...", 160);
        _cutsList = new VerticalStackLayout { Spacing = 10 };
        _outputFolderLabel = CreateMutedLabel("No output folder selected");
        _progressLabel = CreateMutedLabel("");
        _cutAllButton = CreateButton("\u2702\ufe0f Cut All Shorts", Color.FromArgb("#00695C"));

        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        stack.Children.Add(new Label
        {
            Text = "Talking Head Shorts Cutter",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#00695C")
        });

        stack.Children.Add(BuildStageCard("1", "Pick Video", new View[]
        {
            CreateActionButton("Choose Video File", OnChooseVideoClicked),
            _selectedFileLabel
        }));

        stack.Children.Add(BuildStageCard("2", "Get Transcript", new View[]
        {
            CreateActionButton("Copy Transcription Prompt", OnCopyTranscriptionPromptClicked),
            _transcriptEditor,
            CreateActionButton("Import Transcript", OnImportTranscriptClicked),
            _transcriptStatusLabel
        }));

        _copyAnalysisPromptButton.Clicked += OnCopyAnalysisPromptClicked;
        stack.Children.Add(BuildStageCard("3", "Find Best Shorts", new View[]
        {
            _copyAnalysisPromptButton,
            _shortsEditor,
            CreateActionButton("Import Shorts", OnImportShortsClicked)
        }));

        _cutAllButton.Clicked += OnCutAllClicked;
        stack.Children.Add(BuildStageCard("4", "Review & Cut", new View[]
        {
            _cutsList,
            CreateActionButton("Choose Output Folder", OnChooseOutputFolderClicked),
            _outputFolderLabel,
            _cutAllButton,
            _progressLabel
        }));

        Content = new ScrollView { Content = stack };
    }

    private Frame BuildStageCard(string number, string title, IEnumerable<View> children)
    {
        var content = new VerticalStackLayout { Spacing = 10 };
        content.Children.Add(new Label
        {
            Text = $"Stage {number} - {title}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        });

        foreach (var child in children)
            content.Children.Add(child);

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CFD8DC"),
            CornerRadius = 10,
            HasShadow = false,
            Padding = 14,
            Content = content
        };
    }

    private Button CreateActionButton(string text, EventHandler clicked)
    {
        var button = CreateButton(text, Color.FromArgb("#455A64"));
        button.Clicked += clicked;
        return button;
    }

    private Button CreateButton(string text, Color backgroundColor) =>
        new()
        {
            Text = text,
            BackgroundColor = backgroundColor,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 46
        };

    private Editor CreateEditor(string placeholder, double height) =>
        new()
        {
            Placeholder = placeholder,
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = height,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222")
        };

    private Label CreateMutedLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };

    private async void OnChooseVideoClicked(object? sender, EventArgs e)
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp4", ".mov", ".avi", ".mkv" } }
            });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose video file",
                FileTypes = fileTypes
            });

            if (result == null)
                return;

            _selectedVideoPath = result.FullPath;
            _selectedFileLabel.Text = Path.GetFileName(result.FullPath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Video Picker Error", ex.Message, "OK");
        }
    }

    private async void OnCopyTranscriptionPromptClicked(object? sender, EventArgs e)
    {
        const string prompt =
            "Please transcribe the following video with word-level timestamps. Return the transcript in this exact format, one line per word:\n" +
            "[MM:SS.mmm] word\n" +
            "Example:\n" +
            "[00:01.234] Hello\n" +
            "[00:01.456] everyone\n" +
            "Return ONLY the timestamped words, nothing else.";

        await Clipboard.Default.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Transcription prompt copied.", "OK");
    }

    private async void OnImportTranscriptClicked(object? sender, EventArgs e)
    {
        _transcriptWords.Clear();
        var regex = new Regex(@"^\[(\d{2}):(\d{2})\.(\d{3})\]\s+(.+)$");
        foreach (var rawLine in (_transcriptEditor.Text ?? "").Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var timestamp = new TimeSpan(
                0,
                0,
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
            _transcriptWords.Add(new TranscriptWord(timestamp, match.Groups[4].Value.Trim()));
        }

        if (_transcriptWords.Count == 0)
        {
            _transcriptStatusLabel.Text = "No timestamped words found";
            _copyAnalysisPromptButton.IsEnabled = false;
            await DisplayAlert("Import Transcript", "No lines matched [MM:SS.mmm] word.", "OK");
            return;
        }

        var duration = _transcriptWords[^1].Timestamp;
        _transcriptStatusLabel.Text = $"{_transcriptWords.Count} words imported. Duration: {FormatTime(duration)}";
        _copyAnalysisPromptButton.IsEnabled = true;
    }

    private async void OnCopyAnalysisPromptClicked(object? sender, EventArgs e)
    {
        if (_transcriptWords.Count == 0)
            return;

        var prompt =
            "Here is a timestamped transcript of a talking head video. Identify the 3-5 best segments to cut as short-form vertical videos (15-90 seconds each) that would perform best on social media. Look for: strong hooks in the opening 3 seconds, complete standalone insights or stories, high energy or emotional moments, clear value delivery, and natural start/end points.\n\n" +
            "Transcript:\n" +
            BuildAnalysisTranscript() +
            "\n\nReturn ONLY this C#-parsable format:\n" +
            "shortStart[1] = \"MM:SS\";\n" +
            "shortEnd[1] = \"MM:SS\";\n" +
            "shortTitle[1] = \"brief description\";\n" +
            "shortStart[2] = \"MM:SS\";\n" +
            "shortEnd[2] = \"MM:SS\";\n" +
            "shortTitle[2] = \"brief description\";\n" +
            "(continue for all suggested shorts)";

        await Clipboard.Default.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Shorts analysis prompt copied.", "OK");
    }

    private string BuildAnalysisTranscript()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < _transcriptWords.Count; i++)
        {
            if (i % 10 == 0)
                builder.Append($"[{FormatTime(_transcriptWords[i].Timestamp)}] ");
            builder.Append(_transcriptWords[i].Word);
            builder.Append(' ');
        }

        return builder.ToString().Trim();
    }

    private async void OnImportShortsClicked(object? sender, EventArgs e)
    {
        var starts = new Dictionary<int, string>();
        var ends = new Dictionary<int, string>();
        var titles = new Dictionary<int, string>();
        var regex = new Regex(@"short(Start|End|Title)\[(\d+)\]\s*=\s*""(.*?)"";", RegexOptions.IgnoreCase);

        foreach (Match match in regex.Matches(_shortsEditor.Text ?? ""))
        {
            int index = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            string value = match.Groups[3].Value.Trim();
            switch (match.Groups[1].Value.ToLowerInvariant())
            {
                case "start":
                    starts[index] = value;
                    break;
                case "end":
                    ends[index] = value;
                    break;
                case "title":
                    titles[index] = value;
                    break;
            }
        }

        _cuts.Clear();
        foreach (var index in starts.Keys.OrderBy(x => x))
        {
            if (!ends.TryGetValue(index, out var end))
                continue;

            _cuts.Add(new ProposedCut
            {
                Title = titles.TryGetValue(index, out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : $"Short {index}",
                StartTime = starts[index],
                EndTime = end
            });
        }

        RefreshCutsList();
        await DisplayAlert("Import Shorts", $"{_cuts.Count} proposed cuts imported.", "OK");
    }

    private void RefreshCutsList()
    {
        _cutsList.Children.Clear();
        if (_cuts.Count == 0)
        {
            _cutsList.Children.Add(CreateMutedLabel("No proposed cuts imported"));
            return;
        }

        for (int i = 0; i < _cuts.Count; i++)
        {
            int index = i;
            var cut = _cuts[i];
            var title = new Label
            {
                Text = $"{i + 1}. {cut.Title}",
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#263238")
            };
            var details = CreateMutedLabel($"{cut.StartTime} - {cut.EndTime} ({GetDurationText(cut)})");
            var buttons = new HorizontalStackLayout { Spacing = 8 };
            buttons.Children.Add(CreateSmallButton("Edit Start", async () => await EditCutTimeAsync(index, true)));
            buttons.Children.Add(CreateSmallButton("Edit End", async () => await EditCutTimeAsync(index, false)));
            buttons.Children.Add(CreateSmallButton("Remove", () =>
            {
                _cuts.RemoveAt(index);
                RefreshCutsList();
                return Task.CompletedTask;
            }));

            _cutsList.Children.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#F8F9FA"),
                BorderColor = Color.FromArgb("#ECEFF1"),
                CornerRadius = 8,
                Padding = 10,
                HasShadow = false,
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children = { title, details, buttons }
                }
            });
        }
    }

    private Button CreateSmallButton(string text, Func<Task> onClick)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#263238"),
            CornerRadius = 6,
            HeightRequest = 36,
            FontSize = 12,
            Padding = new Thickness(8, 0)
        };
        button.Clicked += async (_, _) => await onClick();
        return button;
    }

    private async Task EditCutTimeAsync(int index, bool isStart)
    {
        if (index < 0 || index >= _cuts.Count)
            return;

        var cut = _cuts[index];
        string current = isStart ? cut.StartTime : cut.EndTime;
        string? updated = await DisplayPromptAsync(
            isStart ? "Edit Start" : "Edit End",
            "Enter time as MM:SS or HH:MM:SS",
            initialValue: current);

        if (string.IsNullOrWhiteSpace(updated))
            return;

        if (isStart)
            cut.StartTime = updated.Trim();
        else
            cut.EndTime = updated.Trim();
        RefreshCutsList();
    }

    private async void OnChooseOutputFolderClicked(object? sender, EventArgs e)
    {
        _outputFolder = await WebsiteFolderHelper.PickParentFolderPathAsync(this);
        if (!string.IsNullOrWhiteSpace(_outputFolder))
            _outputFolderLabel.Text = _outputFolder;
    }

    private async void OnCutAllClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedVideoPath) || !File.Exists(_selectedVideoPath))
        {
            await DisplayAlert("Pick Video", "Choose a video file first.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_outputFolder) || !Directory.Exists(_outputFolder))
        {
            await DisplayAlert("Output Folder", "Choose an output folder first.", "OK");
            return;
        }

        if (_cuts.Count == 0)
        {
            await DisplayAlert("No Shorts", "Import at least one proposed cut first.", "OK");
            return;
        }

        _cutAllButton.IsEnabled = false;
        try
        {
            _progressLabel.IsVisible = true;
            _progressLabel.Text = "Downloading FFmpeg (~160MB, one time only)...";
            string ffmpegPath = await _ffmpeg.EnsureFFmpegAsync();
            _progressLabel.Text = "";
            _progressLabel.IsVisible = false;
            string originalName = Path.GetFileNameWithoutExtension(_selectedVideoPath);

            for (int i = 0; i < _cuts.Count; i++)
            {
                var cut = _cuts[i];
                _progressLabel.IsVisible = true;
                _progressLabel.Text = $"Cutting {i + 1} of {_cuts.Count}: {cut.Title}";
                string outputPath = Path.Combine(
                    _outputFolder,
                    $"{Slugify(originalName)}_{i + 1}_{Slugify(cut.Title)}.mp4");

                var result = await _ffmpeg.RunCutAsync(
                    ffmpegPath,
                    _selectedVideoPath,
                    cut.StartTime,
                    cut.EndTime,
                    outputPath);

                if (!result.Success)
                {
                    await DisplayAlert("FFmpeg Error", result.ErrorMessage, "OK");
                    _progressLabel.Text = "Cutting stopped.";
                    return;
                }
            }

            _progressLabel.Text = $"\u2713 Done: {_outputFolder}";
            await DisplayAlert("Done", $"Shorts saved to:\n{_outputFolder}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cutting Error", ex.Message, "OK");
            _progressLabel.Text = "Cutting failed.";
        }
        finally
        {
            _cutAllButton.IsEnabled = true;
        }
    }

    private string GetDurationText(ProposedCut cut)
    {
        if (!TryParseTime(cut.StartTime, out var start) || !TryParseTime(cut.EndTime, out var end) || end <= start)
            return "duration unknown";
        return FormatTime(end - start);
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        var parts = value.Trim().Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], out int minutes)
            && int.TryParse(parts[1], out int seconds))
        {
            time = new TimeSpan(0, 0, minutes, seconds);
            return true;
        }

        if (parts.Length == 3
            && int.TryParse(parts[0], out int hours)
            && int.TryParse(parts[1], out minutes)
            && int.TryParse(parts[2], out seconds))
        {
            time = new TimeSpan(hours, minutes, seconds);
            return true;
        }

        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
    }

    private static string FormatTime(TimeSpan time)
    {
        int totalMinutes = (int)time.TotalMinutes;
        return $"{totalMinutes:00}:{time.Seconds:00}";
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (char ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '_')
                builder.Append('_');
        }

        return builder.ToString().Trim('_');
    }

    private sealed record TranscriptWord(TimeSpan Timestamp, string Word);

    private sealed class ProposedCut
    {
        public string Title { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
    }
}
