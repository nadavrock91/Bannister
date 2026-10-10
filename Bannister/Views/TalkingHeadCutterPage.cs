using Bannister.Models;
using Bannister.Services;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class TalkingHeadCutterPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly FFmpegService _ffmpeg;
    private readonly TruncatePresetService _truncatePresets;
    private readonly ShortsCutterProjectService _shortsProjects;
    private readonly List<TranscriptWord> _transcriptWords = new();
    private readonly List<ProposedCut> _cuts = new();
    private readonly List<TruncatePreset> _truncatePresetItems = new();
    private readonly List<ShortsCutterProject> _projectItems = new();

    private string? _selectedVideoPath;
    private string? _outputFolder;
    private ShortsCutterProject? _currentProject;
    private bool _truncatePresetsLoaded;
    private bool _loadingTruncatePresets;
    private bool _loadingProjects;

    private readonly Picker _projectPicker;
    private readonly Label _currentProjectLabel;
    private readonly Label _selectedFileLabel;
    private readonly Frame _truncateSilenceCard;
    private readonly Picker _truncatePresetPicker;
    private readonly Entry _silenceThresholdEntry;
    private readonly Entry _minSilenceDurationEntry;
    private readonly Entry _truncateToEntry;
    private readonly Label _truncateProgressLabel;
    private readonly ProgressBar _truncateProgressBar;
    private readonly Editor _transcriptEditor;
    private readonly Label _transcriptStatusLabel;
    private readonly Button _trimCompareButton;
    private readonly Button _smartTrimButton;
    private readonly Button _copyAnalysisPromptButton;
    private readonly Editor _shortsEditor;
    private readonly VerticalStackLayout _cutsList;
    private readonly Label _outputFolderLabel;
    private readonly Label _progressLabel;
    private readonly ProgressBar _cutProgressBar;
    private readonly Label _etaLabel;
    private readonly Button _cutAllButton;
    private Grid _wrapTranscriptOverlay = null!;
    private Grid _ffmpegSetupOverlay = null!;
    private ProgressBar _ffmpegSetupProgressBar = null!;
    private Label _ffmpegSetupPercentLabel = null!;
    private Editor _rawTranscriptEditor = null!;

    public TalkingHeadCutterPage(AuthService auth, FFmpegService ffmpeg, TruncatePresetService truncatePresets, ShortsCutterProjectService shortsProjects)
    {
        _auth = auth;
        _ffmpeg = ffmpeg;
        _truncatePresets = truncatePresets;
        _shortsProjects = shortsProjects;

        Title = "Talking Head Shorts Cutter";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _projectPicker = new Picker
        {
            Title = "Select project",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            TitleColor = Color.FromArgb("#666")
        };
        _projectPicker.SelectedIndexChanged += OnProjectSelected;
        _currentProjectLabel = CreateMutedLabel("No project selected");
        _selectedFileLabel = CreateMutedLabel("No video selected");
        _truncatePresetPicker = new Picker
        {
            Title = "-- Select Preset --",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            TitleColor = Color.FromArgb("#666")
        };
        _truncatePresetPicker.SelectedIndexChanged += OnTruncatePresetSelected;
        _silenceThresholdEntry = CreateNumericEntry("-35");
        _minSilenceDurationEntry = CreateNumericEntry("0.3");
        _truncateToEntry = CreateNumericEntry("0.2");
        _truncateProgressLabel = CreateMutedLabel("");
        _truncateProgressBar = new ProgressBar
        {
            Progress = 0,
            IsVisible = false,
            ProgressColor = Color.FromArgb("#00695C")
        };
        _transcriptEditor = CreateEditor("Paste timestamped words here...", 180);
        _transcriptStatusLabel = CreateMutedLabel("No transcript imported");
        _trimCompareButton = CreateButton("\u2702\ufe0f Trim & Compare", Color.FromArgb("#6A1B9A"));
        _trimCompareButton.IsEnabled = false;
        _smartTrimButton = CreateButton("Smart Trim", Color.FromArgb("#1565C0"));
        _smartTrimButton.IsEnabled = false;
        _copyAnalysisPromptButton = CreateButton("Copy Shorts Analysis Prompt", Color.FromArgb("#1565C0"));
        _copyAnalysisPromptButton.IsEnabled = false;
        _shortsEditor = CreateEditor("Paste suggested shorts here...", 160);
        _cutsList = new VerticalStackLayout { Spacing = 10 };
        _outputFolderLabel = CreateMutedLabel("No output folder selected");
        _progressLabel = CreateMutedLabel("");
        _cutProgressBar = new ProgressBar
        {
            Progress = 0,
            IsVisible = false,
            ProgressColor = Color.FromArgb("#00695C")
        };
        _etaLabel = CreateMutedLabel("");
        _etaLabel.IsVisible = false;
        _cutAllButton = CreateButton("\u2702\ufe0f Cut All Shorts", Color.FromArgb("#00695C"));
        _truncateSilenceCard = BuildStageCard("1b", "Truncate Silence (optional)", new View[]
        {
            CreateLabeledPicker("Preset", _truncatePresetPicker),
            CreateLabeledEntry("Silence threshold (dB)", _silenceThresholdEntry),
            CreateLabeledEntry("Min silence duration (s)", _minSilenceDurationEntry),
            CreateLabeledEntry("Truncate to (s)", _truncateToEntry),
            CreateActionButton("Save as Preset", OnSaveTruncatePresetClicked),
            CreateActionButton("Set as Default", OnSetDefaultTruncatePresetClicked),
            CreateActionButton("Delete Preset", OnDeleteTruncatePresetClicked),
            CreateActionButton("Analyse", OnAnalyseSilenceClicked),
            CreateActionButton("\u2702\ufe0f Truncate Silence", OnTruncateSilenceClicked),
            _truncateProgressLabel,
            _truncateProgressBar
        });
        _truncateSilenceCard.IsVisible = false;

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_truncatePresetsLoaded)
            return;

        _truncatePresetsLoaded = true;
        await EnsureFFmpegOnFirstLoadAsync();
        await LoadProjectsAsync();
        await LoadTruncatePresetsAsync(selectDefault: true);
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

        stack.Children.Add(BuildProjectBar());

        stack.Children.Add(BuildStageCard("1", "Pick Video", new View[]
        {
            CreateActionButton("Choose Video File", OnChooseVideoClicked),
            _selectedFileLabel
        }));

        stack.Children.Add(_truncateSilenceCard);

        stack.Children.Add(BuildStageCard("2", "Get Transcript", new View[]
        {
            CreateActionButton("Copy Transcription Prompt", OnCopyTranscriptionPromptClicked),
            CreateActionButton("Wrap Transcript for Import", OnWrapTranscriptClicked),
            _transcriptEditor,
            CreateActionButton("Import Transcript", OnImportTranscriptClicked),
            _trimCompareButton,
            _smartTrimButton,
            _transcriptStatusLabel
        }));

        _trimCompareButton.Clicked += OnTrimCompareClicked;
        _smartTrimButton.Clicked += OnSmartTrimClicked;
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
            _progressLabel,
            _cutProgressBar,
            _etaLabel
        }));

        var root = new Grid();
        root.Children.Add(new ScrollView { Content = stack });
        _wrapTranscriptOverlay = CreateWrapTranscriptOverlay();
        root.Children.Add(_wrapTranscriptOverlay);
        _ffmpegSetupOverlay = CreateFFmpegSetupOverlay();
        root.Children.Add(_ffmpegSetupOverlay);

        Content = root;
    }

    private async Task EnsureFFmpegOnFirstLoadAsync()
    {
        string ffmpegPath = Path.Combine(FileSystem.AppDataDirectory, "ffmpeg.exe");
        if (File.Exists(ffmpegPath))
            return;

        _ffmpegSetupProgressBar.Progress = 0;
        _ffmpegSetupPercentLabel.Text = "0%";
        _ffmpegSetupOverlay.IsVisible = true;

        var progress = new Progress<(long bytesReceived, long totalBytes)>(value =>
        {
            double pct = value.totalBytes > 0
                ? Math.Clamp((double)value.bytesReceived / value.totalBytes, 0, 1)
                : 0;
            _ffmpegSetupProgressBar.Progress = pct;
            _ffmpegSetupPercentLabel.Text = $"{pct * 100:0}%";
        });

        try
        {
            await _ffmpeg.EnsureFFmpegAsync(progress);
            _ffmpegSetupProgressBar.Progress = 1;
            _ffmpegSetupPercentLabel.Text = "100%";
        }
        catch (Exception ex)
        {
            await DisplayAlert("FFmpeg Setup Error", ex.Message, "OK");
        }
        finally
        {
            _ffmpegSetupOverlay.IsVisible = false;
        }
    }

    private Grid CreateFFmpegSetupOverlay()
    {
        _ffmpegSetupProgressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromArgb("#00695C")
        };
        _ffmpegSetupPercentLabel = new Label
        {
            Text = "0%",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CFD8DC"),
            CornerRadius = 10,
            HasShadow = true,
            Padding = 20,
            WidthRequest = 420,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Downloading FFmpeg (~160MB, one-time setup...)",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#263238"),
                        HorizontalTextAlignment = TextAlignment.Center,
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    _ffmpegSetupProgressBar,
                    _ffmpegSetupPercentLabel
                }
            }
        };

        return new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.70),
            Padding = 16,
            Children = { card }
        };
    }

    private Frame BuildProjectBar()
    {
        var saveButton = CreateButton("Save Project", Color.FromArgb("#00695C"));
        saveButton.Clicked += OnSaveProjectClicked;

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CFD8DC"),
            CornerRadius = 10,
            HasShadow = false,
            Padding = 12,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = "Project",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#263238")
                    },
                    _projectPicker,
                    saveButton,
                    _currentProjectLabel
                }
            }
        };
    }

    private Grid CreateWrapTranscriptOverlay()
    {
        _rawTranscriptEditor = CreateEditor("Paste ElevenLabs JSON here...", 260);

        var copyButton = CreateButton("Copy Reformat Prompt", Color.FromArgb("#1565C0"));
        copyButton.Clicked += OnCopyReformatPromptClicked;

        var cancelButton = CreateButton("Cancel", Color.FromArgb("#607D8B"));
        cancelButton.Clicked += (_, _) => _wrapTranscriptOverlay.IsVisible = false;

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#CFD8DC"),
            CornerRadius = 10,
            HasShadow = true,
            Padding = 16,
            WidthRequest = 520,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Wrap Transcript for Import",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#263238")
                    },
                    new Label
                    {
                        Text = "Paste ElevenLabs JSON transcript here. The prompt will ask an LLM to convert it to the [MM:SS.mmm] word format for import.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#666"),
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    _rawTranscriptEditor,
                    copyButton,
                    cancelButton
                }
            }
        };

        return new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.55),
            Padding = 16,
            Children = { card }
        };
    }

    private Frame BuildStageCard(string number, string title, IEnumerable<View> children)
    {
        var content = new VerticalStackLayout { Spacing = 10 };
        content.Children.Add(new Label
        {
            Text = number == "1b"
                ? $"Stage {number} \u2014 {title}"
                : $"Stage {number} - {title}",
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

    private Entry CreateNumericEntry(string defaultValue) =>
        new()
        {
            Text = defaultValue,
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222")
        };

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
                    TextColor = Color.FromArgb("#455A64")
                },
                entry
            }
        };

    private View CreateLabeledPicker(string label, Picker picker) =>
        new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#455A64")
                },
                picker
            }
        };

    private Label CreateMutedLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };

    private async Task LoadProjectsAsync(int? selectProjectId = null)
    {
        _loadingProjects = true;
        try
        {
            _projectItems.Clear();
            _projectPicker.Items.Clear();
            _projectPicker.Items.Add("New Project");

            var projects = await _shortsProjects.GetProjectsAsync(_auth.CurrentUsername);
            _projectItems.AddRange(projects);
            foreach (var project in projects)
                _projectPicker.Items.Add(project.Name);

            if (selectProjectId.HasValue)
            {
                int index = _projectItems.FindIndex(p => p.Id == selectProjectId.Value);
                _projectPicker.SelectedIndex = index >= 0 ? index + 1 : -1;
            }
            else if (_currentProject != null)
            {
                int index = _projectItems.FindIndex(p => p.Id == _currentProject.Id);
                _projectPicker.SelectedIndex = index >= 0 ? index + 1 : -1;
            }
        }
        catch (Exception ex)
        {
            _currentProjectLabel.Text = $"Could not load projects: {ex.Message}";
        }
        finally
        {
            _loadingProjects = false;
        }
    }

    private async void OnProjectSelected(object? sender, EventArgs e)
    {
        if (_loadingProjects)
            return;

        if (_projectPicker.SelectedIndex == 0)
        {
            await CreateNewProjectAsync();
            return;
        }

        int index = _projectPicker.SelectedIndex - 1;
        if (index >= 0 && index < _projectItems.Count)
            await LoadProjectAsync(_projectItems[index].Id);
    }

    private async Task CreateNewProjectAsync()
    {
        string? name = await DisplayPromptAsync("New Project", "Project name:");
        if (string.IsNullOrWhiteSpace(name))
        {
            await LoadProjectsAsync(_currentProject?.Id);
            return;
        }

        var project = new ShortsCutterProject
        {
            Username = _auth.CurrentUsername,
            Name = name.Trim(),
            VideoPath = _selectedVideoPath ?? "",
            TranscriptJson = JsonSerializer.Serialize(_transcriptWords),
            CreatedDate = DateTime.UtcNow
        };

        await _shortsProjects.SaveProjectAsync(project);
        _currentProject = project;
        await SaveCutsToProjectAsync();
        await LoadProjectsAsync(project.Id);
        UpdateCurrentProjectLabel();
    }

    private async Task LoadProjectAsync(int projectId)
    {
        var project = await _shortsProjects.GetProjectAsync(projectId);
        if (project == null)
            return;

        _currentProject = project;
        _selectedVideoPath = string.IsNullOrWhiteSpace(project.VideoPath) ? null : project.VideoPath;
        if (string.IsNullOrWhiteSpace(project.VideoPath) && !string.IsNullOrWhiteSpace(_selectedVideoPath))
        {
            _currentProject.VideoPath = _selectedVideoPath;
            await _shortsProjects.SaveProjectAsync(_currentProject);
        }
        _selectedFileLabel.Text = string.IsNullOrWhiteSpace(_selectedVideoPath)
            ? "No video selected"
            : Path.GetFileName(_selectedVideoPath);
        _truncateSilenceCard.IsVisible = !string.IsNullOrWhiteSpace(_selectedVideoPath);

        _transcriptWords.Clear();
        if (!string.IsNullOrWhiteSpace(project.TranscriptJson))
        {
            try
            {
                var words = JsonSerializer.Deserialize<List<TranscriptWord>>(project.TranscriptJson) ?? new();
                _transcriptWords.AddRange(words);
            }
            catch
            {
                _transcriptWords.Clear();
            }
        }

        _transcriptEditor.Text = BuildTranscriptEditorText();
        UpdateTranscriptStatus();

        _cuts.Clear();
        var dbCuts = await _shortsProjects.GetCutsAsync(project.Id);
        _cuts.AddRange(dbCuts.Select(ToProposedCut));
        RefreshCutsList();
        UpdateCurrentProjectLabel();
    }

    private async void OnSaveProjectClicked(object? sender, EventArgs e)
    {
        if (_currentProject != null)
            _currentProject.VideoPath = _selectedVideoPath ?? "";

        await SaveProjectAsync(showAlert: true);
    }

    private async Task SaveProjectAsync(bool showAlert = false)
    {
        if (_currentProject == null)
        {
            await CreateNewProjectAsync();
            return;
        }

        _currentProject.VideoPath = _selectedVideoPath ?? "";
        _currentProject.TranscriptJson = JsonSerializer.Serialize(_transcriptWords);
        await _shortsProjects.SaveProjectAsync(_currentProject);
        await SaveCutsToProjectAsync();
        await LoadProjectsAsync(_currentProject.Id);
        UpdateCurrentProjectLabel();

        if (showAlert)
            await DisplayAlert("Saved", "Project saved.", "OK");
    }

    private async Task SaveCutsToProjectAsync()
    {
        if (_currentProject == null)
            return;

        for (int i = 0; i < _cuts.Count; i++)
        {
            var cut = ToDbCut(_cuts[i], _currentProject.Id, i);
            await _shortsProjects.SaveCutAsync(cut);
            _cuts[i].Id = cut.Id;
        }
    }

    private void UpdateCurrentProjectLabel()
    {
        _currentProjectLabel.Text = _currentProject == null
            ? "No project selected"
            : $"Current project: {_currentProject.Name}";
    }

    private string BuildTranscriptEditorText()
    {
        var builder = new StringBuilder();
        foreach (var word in _transcriptWords)
            builder.AppendLine($"[{FormatTimeWithMilliseconds(word.Timestamp)}] {word.Word}");
        return builder.ToString().TrimEnd();
    }

    private void UpdateTranscriptStatus()
    {
        if (_transcriptWords.Count == 0)
        {
            _transcriptStatusLabel.Text = "No transcript imported";
            _copyAnalysisPromptButton.IsEnabled = false;
            _trimCompareButton.IsEnabled = false;
            _smartTrimButton.IsEnabled = false;
            return;
        }

        var duration = _transcriptWords[^1].Timestamp;
        _transcriptStatusLabel.Text = $"{_transcriptWords.Count} words imported. Duration: {FormatTime(duration)}";
        _copyAnalysisPromptButton.IsEnabled = true;
        _trimCompareButton.IsEnabled = true;
        _smartTrimButton.IsEnabled = true;
    }

    private static ProposedCut ToProposedCut(ShortsCutterCut cut) =>
        new()
        {
            Id = cut.Id,
            Title = cut.Title,
            StartTime = cut.StartTime,
            EndTime = cut.EndTime,
            HookRating = cut.HookRating,
            ContentRating = cut.ContentRating,
            TranscriptText = cut.TranscriptText
        };

    private static ShortsCutterCut ToDbCut(ProposedCut cut, int projectId, int sortOrder) =>
        new()
        {
            Id = cut.Id,
            ProjectId = projectId,
            Title = cut.Title,
            StartTime = cut.StartTime,
            EndTime = cut.EndTime,
            HookRating = cut.HookRating,
            ContentRating = cut.ContentRating,
            TranscriptText = cut.TranscriptText,
            SortOrder = sortOrder
        };

    private async Task LoadTruncatePresetsAsync(bool selectDefault = false, int? selectPresetId = null)
    {
        _loadingTruncatePresets = true;
        try
        {
            _truncatePresetItems.Clear();
            _truncatePresetPicker.Items.Clear();
            _truncatePresetPicker.Items.Add("-- Select Preset --");

            var presets = await _truncatePresets.GetPresetsAsync(_auth.CurrentUsername);
            _truncatePresetItems.AddRange(presets);
            foreach (var preset in presets)
                _truncatePresetPicker.Items.Add(preset.Title);

            TruncatePreset? selected = null;
            if (selectPresetId.HasValue)
                selected = presets.FirstOrDefault(p => p.Id == selectPresetId.Value);
            else if (selectDefault)
                selected = presets.FirstOrDefault(p => p.IsDefault)
                    ?? await _truncatePresets.GetDefaultPresetAsync(_auth.CurrentUsername);

            if (selected != null)
            {
                int index = presets.FindIndex(p => p.Id == selected.Id);
                _truncatePresetPicker.SelectedIndex = index >= 0 ? index + 1 : 0;
                ApplyTruncatePreset(selected);
            }
            else
            {
                _truncatePresetPicker.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _truncateProgressLabel.Text = $"Could not load presets: {ex.Message}";
        }
        finally
        {
            _loadingTruncatePresets = false;
        }
    }

    private void OnTruncatePresetSelected(object? sender, EventArgs e)
    {
        if (_loadingTruncatePresets)
            return;

        var preset = GetSelectedTruncatePreset();
        if (preset != null)
            ApplyTruncatePreset(preset);
    }

    private TruncatePreset? GetSelectedTruncatePreset()
    {
        int index = _truncatePresetPicker.SelectedIndex - 1;
        if (index < 0 || index >= _truncatePresetItems.Count)
            return null;

        return _truncatePresetItems[index];
    }

    private void ApplyTruncatePreset(TruncatePreset preset)
    {
        _silenceThresholdEntry.Text = FormatPresetNumber(preset.ThresholdDb);
        _minSilenceDurationEntry.Text = FormatPresetNumber(preset.MinSilenceDuration);
        _truncateToEntry.Text = FormatPresetNumber(preset.TruncateTo);
    }

    private async void OnSaveTruncatePresetClicked(object? sender, EventArgs e)
    {
        if (!TryReadTruncatePresetValues(out double thresholdDb, out double minDurationSec, out double truncateToSec))
            return;

        string title = await DisplayPromptAsync("Save Preset", "Preset title:");
        if (string.IsNullOrWhiteSpace(title))
            return;

        var preset = new TruncatePreset
        {
            Username = _auth.CurrentUsername,
            Title = title.Trim(),
            ThresholdDb = thresholdDb,
            MinSilenceDuration = minDurationSec,
            TruncateTo = truncateToSec,
            CreatedDate = DateTime.UtcNow
        };

        await _truncatePresets.SavePresetAsync(preset);
        await LoadTruncatePresetsAsync(selectPresetId: preset.Id);
    }

    private async void OnSetDefaultTruncatePresetClicked(object? sender, EventArgs e)
    {
        var preset = GetSelectedTruncatePreset();
        if (preset == null)
        {
            await DisplayAlert("Select Preset", "Choose a preset first.", "OK");
            return;
        }

        preset.IsDefault = true;
        await _truncatePresets.SavePresetAsync(preset);
        await LoadTruncatePresetsAsync(selectPresetId: preset.Id);
    }

    private async void OnDeleteTruncatePresetClicked(object? sender, EventArgs e)
    {
        var preset = GetSelectedTruncatePreset();
        if (preset == null)
        {
            await DisplayAlert("Select Preset", "Choose a preset first.", "OK");
            return;
        }

        await _truncatePresets.DeletePresetAsync(preset.Id);
        await LoadTruncatePresetsAsync();
    }

    private bool TryReadTruncatePresetValues(out double thresholdDb, out double minDurationSec, out double truncateToSec)
    {
        if (!TryParseDouble(_silenceThresholdEntry.Text, out thresholdDb)
            || !TryParseDouble(_minSilenceDurationEntry.Text, out minDurationSec)
            || !TryParseDouble(_truncateToEntry.Text, out truncateToSec))
        {
            DisplayAlert("Invalid Values", "Enter numeric silence settings.", "OK");
            thresholdDb = 0;
            minDurationSec = 0;
            truncateToSec = 0;
            return false;
        }

        return true;
    }

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
            _truncateSilenceCard.IsVisible = true;

            if (_currentProject != null)
            {
                _currentProject.VideoPath = _selectedVideoPath;
                await _shortsProjects.SaveProjectAsync(_currentProject);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Video Picker Error", ex.Message, "OK");
        }
    }

    private async void OnTruncateSilenceClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedVideoPath) || !File.Exists(_selectedVideoPath))
        {
            await DisplayAlert("Pick Video", "Choose a video file first.", "OK");
            return;
        }

        if (!TryParseDouble(_silenceThresholdEntry.Text, out double thresholdDb)
            || !TryParseDouble(_minSilenceDurationEntry.Text, out double minDurationSec)
            || !TryParseDouble(_truncateToEntry.Text, out double truncateToSec))
        {
            await DisplayAlert("Invalid Values", "Enter numeric silence settings.", "OK");
            return;
        }

        try
        {
            _truncateProgressLabel.Text = "Truncating silence...";
            _truncateProgressBar.Progress = 0;
            _truncateProgressBar.IsVisible = true;

            string ffmpegPath = await _ffmpeg.EnsureFFmpegAsync();
            string folder = Path.GetDirectoryName(_selectedVideoPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(_selectedVideoPath);
            string outputPath = Path.Combine(folder, $"{fileName}_truncated.mp4");

            var truncateProgress = new Progress<double>(percent =>
            {
                _truncateProgressBar.Progress = Math.Clamp(percent, 0, 1);
                _truncateProgressLabel.Text = $"Truncating silence... {percent * 100:0}%";
            });

            var result = await _ffmpeg.TruncateSilenceAsync(
                ffmpegPath,
                _selectedVideoPath,
                outputPath,
                thresholdDb,
                minDurationSec,
                truncateToSec,
                truncateProgress);

            if (!result.success)
            {
                _truncateProgressLabel.Text = "Truncate failed.";
                var errorLines = result.error.Split('\n');
                var lastLines = string.Join("\n", errorLines.TakeLast(5));
                await DisplayAlert("FFmpeg Error", lastLines, "OK");
                return;
            }

            _selectedVideoPath = outputPath;
            _selectedFileLabel.Text = Path.GetFileName(outputPath);
            _truncateProgressBar.Progress = 1;
            _truncateProgressLabel.Text = $"\u2713 Done \u2014 using truncated version: {Path.GetFileName(outputPath)}";
        }
        catch (Exception ex)
        {
            _truncateProgressLabel.Text = "Truncate failed.";
            await DisplayAlert("Truncate Error", ex.Message, "OK");
        }
    }

    private async void OnAnalyseSilenceClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedVideoPath) || !File.Exists(_selectedVideoPath))
        {
            await DisplayAlert("Pick Video", "Choose a video file first.", "OK");
            return;
        }

        if (!TryParseDouble(_silenceThresholdEntry.Text, out double thresholdDb)
            || !TryParseDouble(_minSilenceDurationEntry.Text, out double minDurationSec)
            || !TryParseDouble(_truncateToEntry.Text, out double truncateToSec))
        {
            await DisplayAlert("Invalid Values", "Enter numeric silence settings.", "OK");
            return;
        }

        try
        {
            _truncateProgressLabel.Text = "Analysing silence...";
            _truncateProgressBar.IsVisible = false;

            string ffmpegPath = await _ffmpeg.EnsureFFmpegAsync();
            var result = await _ffmpeg.AnalyseSilenceAsync(
                ffmpegPath,
                _selectedVideoPath,
                thresholdDb,
                minDurationSec);

            if (!result.success)
            {
                _truncateProgressLabel.Text = "Analysis failed.";
                var errorLines = result.error.Split('\n');
                var lastLines = string.Join("\n", errorLines.TakeLast(5));
                await DisplayAlert("FFmpeg Error", lastLines, "OK");
                return;
            }

            double rawSilenceSeconds = result.silences.Sum(s => Math.Max(0, s.end - s.start));
            double inputSeconds = result.estimatedDuration.TotalSeconds + rawSilenceSeconds;
            double savedSeconds = result.silences.Sum(s => Math.Max(0, (s.end - s.start) - (truncateToSec * 2)));
            var estimatedOutput = TimeSpan.FromSeconds(Math.Max(0, inputSeconds - savedSeconds));

            _truncateProgressLabel.Text =
                $"Estimated output duration: {FormatDuration(estimatedOutput)} (saving {savedSeconds:0}s)";
        }
        catch (Exception ex)
        {
            _truncateProgressLabel.Text = "Analysis failed.";
            await DisplayAlert("Analysis Error", ex.Message, "OK");
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

    private void OnWrapTranscriptClicked(object? sender, EventArgs e)
    {
        _wrapTranscriptOverlay.IsVisible = true;
    }

    private async void OnCopyReformatPromptClicked(object? sender, EventArgs e)
    {
        string rawTranscript = _rawTranscriptEditor.Text?.Trim() ?? "";
        string prompt =
            "Convert the following ElevenLabs JSON transcript to this exact format, one word per line with its timestamp: [MM:SS.mmm] word\n\n" +
            "Rules:\n" +
            "- One word per line\n" +
            "- Timestamp is when that word starts speaking\n" +
            "- Format: [MM:SS.mmm] where MM=minutes, SS=seconds, mmm=milliseconds\n" +
            "- No other text, no numbering, no blank lines\n\n" +
            "JSON to convert:\n" +
            rawTranscript;

        await Clipboard.Default.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Reformat prompt copied.", "OK");
    }

    private async void OnImportTranscriptClicked(object? sender, EventArgs e)
    {
        _transcriptWords.Clear();
        var regex = new Regex(@"^\[(\d{2}):(\d{2})\.(\d{3})\]\s+(.+)$");
        foreach (var rawLine in (_transcriptEditor.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var totalMs =
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60000
                + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 1000
                + int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            var timestamp = TimeSpan.FromMilliseconds(totalMs);
            _transcriptWords.Add(new TranscriptWord(timestamp, match.Groups[4].Value.Trim()));
        }

        if (_transcriptWords.Count == 0)
        {
            _transcriptStatusLabel.Text = "No timestamped words found";
            _copyAnalysisPromptButton.IsEnabled = false;
            _trimCompareButton.IsEnabled = false;
            _smartTrimButton.IsEnabled = false;
            await DisplayAlert("Import Transcript", "No lines matched [MM:SS.mmm] word.", "OK");
            return;
        }

        UpdateTranscriptStatus();
    }

    private async void OnTrimCompareClicked(object? sender, EventArgs e)
    {
        if (_transcriptWords.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(_selectedVideoPath) || !File.Exists(_selectedVideoPath))
        {
            await DisplayAlert("No Video Selected", "Please select a video file in Stage 1 before using Trim & Compare.", "OK");
            return;
        }

        await Navigation.PushAsync(new TranscriptTrimPage(_transcriptWords, _selectedVideoPath, _ffmpeg, path =>
        {
            _selectedVideoPath = path;
            if (_currentProject != null)
            {
                _currentProject.VideoPath = path;
                _ = _shortsProjects.SaveProjectAsync(_currentProject);
            }
        }));
    }

    private async void OnSmartTrimClicked(object? sender, EventArgs e)
    {
        if (_transcriptWords.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(_selectedVideoPath) || !File.Exists(_selectedVideoPath))
        {
            await DisplayAlert("No Video Selected", "Please select a video file in Stage 1 before using Smart Trim.", "OK");
            return;
        }

        await Navigation.PushAsync(new SmartTrimPage(_transcriptWords, _selectedVideoPath, _ffmpeg, path =>
        {
            _selectedVideoPath = path;
            if (_currentProject != null)
            {
                _currentProject.VideoPath = path;
                _ = _shortsProjects.SaveProjectAsync(_currentProject);
            }
        }));
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
            "shortHook[1] = 8;\n" +
            "shortContent[1] = 7;\n" +
            "shortTranscript[1] = \"full text of the short verbatim from the transcript\";\n" +
            "shortStart[2] = \"MM:SS\";\n" +
            "shortEnd[2] = \"MM:SS\";\n" +
            "shortTitle[2] = \"brief description\";\n" +
            "shortHook[2] = 8;\n" +
            "shortContent[2] = 7;\n" +
            "shortTranscript[2] = \"full text of the short verbatim from the transcript\";\n" +
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
        var hooks = new Dictionary<int, int>();
        var contents = new Dictionary<int, int>();
        var transcripts = new Dictionary<int, string>();
        var regex = new Regex(@"short(Start|End|Title|Transcript)\[(\d+)\]\s*=\s*""(.*?)"";|short(Hook|Content)\[(\d+)\]\s*=\s*(\d+)\s*;", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in regex.Matches(_shortsEditor.Text ?? ""))
        {
            if (match.Groups[1].Success)
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
                    case "transcript":
                        transcripts[index] = value;
                        break;
                }
            }
            else
            {
                int index = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
                int value = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);
                switch (match.Groups[4].Value.ToLowerInvariant())
                {
                    case "hook":
                        hooks[index] = value;
                        break;
                    case "content":
                        contents[index] = value;
                        break;
                }
            }
        }

        var importedCuts = new List<ProposedCut>();
        foreach (var index in starts.Keys.OrderBy(x => x))
        {
            if (!ends.TryGetValue(index, out var end))
                continue;

            importedCuts.Add(new ProposedCut
            {
                Title = titles.TryGetValue(index, out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : $"Short {index}",
                StartTime = starts[index],
                EndTime = end,
                HookRating = hooks.TryGetValue(index, out var hook) ? hook : 0,
                ContentRating = contents.TryGetValue(index, out var content) ? content : 0,
                TranscriptText = transcripts.TryGetValue(index, out var transcript) ? transcript : ""
            });
        }

        if (_currentProject != null)
            _cuts.AddRange(importedCuts);
        else
        {
            _cuts.Clear();
            _cuts.AddRange(importedCuts);
        }

        if (_currentProject != null)
            await SaveProjectAsync();

        RefreshCutsList();
        await DisplayAlert("Import Shorts", $"{importedCuts.Count} proposed cuts imported.", "OK");
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
            var ratings = new HorizontalStackLayout { Spacing = 10 };
            ratings.Children.Add(new Label
            {
                Text = $"Hook: {cut.HookRating}/10",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F57C00")
            });
            ratings.Children.Add(new Label
            {
                Text = $"\u2b50 Content: {cut.ContentRating}/10",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0")
            });

            var transcriptLabel = new Label
            {
                Text = cut.TranscriptText,
                FontSize = 12,
                TextColor = Color.FromArgb("#455A64"),
                LineBreakMode = LineBreakMode.WordWrap,
                IsVisible = false
            };
            var transcriptToggle = CreateSmallButton("Show transcript", () =>
            {
                transcriptLabel.IsVisible = !transcriptLabel.IsVisible;
                return Task.CompletedTask;
            });
            var buttons = new HorizontalStackLayout { Spacing = 8 };
            buttons.Children.Add(CreateSmallButton("Edit Start", async () => await EditCutTimeAsync(index, true)));
            buttons.Children.Add(CreateSmallButton("Edit End", async () => await EditCutTimeAsync(index, false)));
            buttons.Children.Add(CreateSmallButton("Remove", async () =>
            {
                if (_currentProject != null && cut.Id > 0)
                    await _shortsProjects.DeleteCutAsync(cut.Id);
                _cuts.RemoveAt(index);
                if (_currentProject != null)
                    await SaveProjectAsync();
                RefreshCutsList();
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
                    Children = { title, details, ratings, transcriptToggle, transcriptLabel, buttons }
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
            _cutProgressBar.IsVisible = true;
            _etaLabel.IsVisible = false;
            _cutProgressBar.Progress = 0;

            var downloadProgress = new Progress<(long bytesReceived, long totalBytes)>(progress =>
            {
                long displayTotal = progress.totalBytes > 0
                    ? progress.totalBytes
                    : 161L * 1024L * 1024L;
                double percent = displayTotal > 0
                    ? Math.Clamp((double)progress.bytesReceived / displayTotal, 0, 1)
                    : 0;
                _cutProgressBar.Progress = percent;
                _progressLabel.Text =
                    $"Downloading FFmpeg... {percent * 100:0}% ({FormatMegabytes(progress.bytesReceived)} MB / {FormatMegabytes(displayTotal)} MB)";
            });

            string ffmpegPath = await _ffmpeg.EnsureFFmpegAsync(downloadProgress);
            _progressLabel.Text = "";
            _progressLabel.IsVisible = false;
            _cutProgressBar.Progress = 0;
            _cutProgressBar.IsVisible = false;
            string originalName = Path.GetFileNameWithoutExtension(_selectedVideoPath);
            var cutStartedAt = DateTime.UtcNow;

            for (int i = 0; i < _cuts.Count; i++)
            {
                var cut = _cuts[i];
                double completedProgress = (double)i / _cuts.Count;
                _progressLabel.IsVisible = true;
                _cutProgressBar.IsVisible = true;
                _etaLabel.IsVisible = true;
                _cutProgressBar.Progress = completedProgress;
                _progressLabel.Text =
                    $"Cutting {i + 1} of {_cuts.Count}: {cut.Title} \u2014 {completedProgress * 100:0}% complete";
                _etaLabel.Text = i == 0
                    ? "Estimated time remaining: calculating..."
                    : $"Estimated time remaining: {FormatDuration(TimeSpan.FromTicks((DateTime.UtcNow - cutStartedAt).Ticks / i * (_cuts.Count - i)))}";
                string outputPath = Path.Combine(
                    _outputFolder,
                    $"{Slugify(originalName)}_{i + 1}_{Slugify(cut.Title)}.mp4");

                TryParseTime(cut.StartTime, out var clipStart);
                TryParseTime(cut.EndTime, out var clipEnd);
                var clipProgress = new Progress<double>(clipPercent =>
                {
                    double overallProgress = Math.Clamp((i + clipPercent) / _cuts.Count, 0, 1);
                    _cutProgressBar.Progress = overallProgress;
                    _progressLabel.Text =
                        $"Cutting {i + 1} of {_cuts.Count}: {cut.Title} \u2014 {overallProgress * 100:0}% complete";
                    if (i > 0 || clipPercent > 0)
                    {
                        double completedClips = i + clipPercent;
                        var elapsed = DateTime.UtcNow - cutStartedAt;
                        var remainingClips = _cuts.Count - completedClips;
                        var estimatedRemaining = TimeSpan.FromSeconds(elapsed.TotalSeconds / completedClips * remainingClips);
                        _etaLabel.Text =
                            $"Estimated time remaining: {FormatDuration(estimatedRemaining)}";
                    }
                });

                var result = await _ffmpeg.RunCutAsync(
                    ffmpegPath,
                    _selectedVideoPath,
                    cut.StartTime,
                    cut.EndTime,
                    outputPath,
                    clipProgress,
                    clipStart,
                    clipEnd);

                if (!result.success)
                {
                    var errorLines = result.error.Split('\n');
                    var lastLines = string.Join("\n", errorLines.TakeLast(5));
                    await DisplayAlert("FFmpeg Error", lastLines, "OK");
                    _progressLabel.Text = "Cutting stopped.";
                    return;
                }

                double updatedProgress = (double)(i + 1) / _cuts.Count;
                _cutProgressBar.Progress = updatedProgress;
                _progressLabel.Text =
                    $"Cutting {i + 1} of {_cuts.Count}: {cut.Title} \u2014 {updatedProgress * 100:0}% complete";
                int remaining = _cuts.Count - (i + 1);
                if (remaining > 0)
                {
                    var averageTicks = (DateTime.UtcNow - cutStartedAt).Ticks / (i + 1);
                    _etaLabel.Text =
                        $"Estimated time remaining: {FormatDuration(TimeSpan.FromTicks(averageTicks * remaining))}";
                }
                else
                {
                    _etaLabel.Text = "Estimated time remaining: 00:00";
                }
            }

            _progressLabel.Text = $"\u2713 Done: {_outputFolder}";
            _cutProgressBar.Progress = 1;
            _etaLabel.IsVisible = false;
            await DisplayAlert("Done", $"Shorts saved to:\n{_outputFolder}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cutting Error", ex.Message, "OK");
            _progressLabel.Text = "Cutting failed.";
            _etaLabel.IsVisible = false;
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

    private static string FormatTimeWithMilliseconds(TimeSpan time)
    {
        int totalMinutes = (int)time.TotalMinutes;
        return $"{totalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    private static string FormatDuration(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private static string FormatMegabytes(long bytes) =>
        (bytes / 1024d / 1024d).ToString("0", CultureInfo.InvariantCulture);

    private static bool TryParseDouble(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
        || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);

    private static string FormatPresetNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

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

    public sealed record TranscriptWord(TimeSpan Timestamp, string Word);

    private sealed class ProposedCut
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int HookRating { get; set; }
        public int ContentRating { get; set; }
        public string TranscriptText { get; set; } = "";
    }
}
