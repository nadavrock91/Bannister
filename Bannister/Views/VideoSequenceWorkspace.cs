using Bannister.Models;
using Bannister.Services;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;

namespace Bannister.Views;

public class VideoGenerationReviewView : ContentView
{
    private readonly DenseVideoGeneration _generation;
    private readonly MediaElement _player;
    private readonly Slider _scrubber;
    private readonly Label _timeLabel;
    private readonly Label _rangeLabel;
    private double _start;
    private double _end;
    private bool _updating;

    public VideoGenerationReviewView(
        DenseVideoGeneration generation,
        string clipPath,
        Func<double, double, Task> saveRun,
        Func<double, double, Task> continueFromHere,
        Func<Task> markFailed)
    {
        _generation = generation;
        _start = generation.UsableStartSeconds ?? 0;
        _end = generation.UsableEndSeconds ?? 0;
        _player = new MediaElement
        {
            Source = MediaSource.FromFile(clipPath),
            ShouldAutoPlay = false,
            ShouldShowPlaybackControls = true,
            HeightRequest = 260,
            BackgroundColor = Colors.Black
        };
        _scrubber = new Slider { Minimum = 0, Maximum = 1 };
        _timeLabel = Muted("0.00 / 0.00 s");
        _rangeLabel = Muted("");
        _player.MediaOpened += (_, _) =>
        {
            _scrubber.Maximum = Math.Max(0.1, _player.Duration.TotalSeconds);
            if (_end <= 0) _end = _player.Duration.TotalSeconds;
            UpdateRange();
        };
        _scrubber.DragCompleted += async (_, _) =>
            await _player.SeekTo(TimeSpan.FromSeconds(_scrubber.Value), CancellationToken.None);

        var setStart = Button("Set start here", "#1565C0");
        setStart.Clicked += (_, _) => { _start = _player.Position.TotalSeconds; UpdateRange(); };
        var setEnd = Button("Set end here", "#6A1B9A");
        setEnd.Clicked += (_, _) => { _end = _player.Position.TotalSeconds; UpdateRange(); };
        var save = Button("Keep usable run", "#2E7D32");
        save.Clicked += async (_, _) =>
        {
            if (!ValidRange()) return;
            await saveRun(_start, _end);
        };
        var next = Button("Continue from here", "#C2185B");
        next.Clicked += async (_, _) =>
        {
            if (!ValidRange()) return;
            await continueFromHere(_start, _end);
        };
        var failed = Button("Mark failed", "#B71C1C");
        failed.Clicked += async (_, _) => await markFailed();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _player, _scrubber, _timeLabel, _rangeLabel,
                new HorizontalStackLayout { Spacing = 6, Children = { setStart, setEnd } },
                new HorizontalStackLayout { Spacing = 6, Children = { save, next, failed } }
            }
        };

        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(250), () =>
        {
            if (!IsLoaded) return false;
            if (!_updating)
            {
                _updating = true;
                _scrubber.Value = Math.Min(_scrubber.Maximum, _player.Position.TotalSeconds);
                _timeLabel.Text = $"{_player.Position.TotalSeconds:0.00} / {_player.Duration.TotalSeconds:0.00} s";
                _updating = false;
            }
            return true;
        });
        UpdateRange();
    }

    private bool ValidRange()
    {
        if (_end > _start) return true;
        _rangeLabel.Text = "End must be after start.";
        _rangeLabel.TextColor = Color.FromArgb("#C62828");
        return false;
    }

    private void UpdateRange()
    {
        _rangeLabel.TextColor = Color.FromArgb("#555");
        _rangeLabel.Text = $"Selected: {_start:0.00}s → {_end:0.00}s  •  usable {Math.Max(0, _end - _start):0.00}s";
    }

    private static Label Muted(string text) => new() { Text = text, FontSize = 12, TextColor = Color.FromArgb("#555") };
    private static Button Button(string text, string color) => new()
    {
        Text = text, FontSize = 11, HeightRequest = 36,
        Padding = new Thickness(9, 0), BackgroundColor = Color.FromArgb(color), TextColor = Colors.White
    };
}

public class VideoSequenceWorkspacePage : ContentPage
{
    private readonly VideoSequenceWorkspaceView _workspace;

    public VideoSequenceWorkspacePage(
        DenseVideoSequence sequence,
        DenseVideoSequenceService service,
        FFmpegService ffmpeg)
    {
        Title = sequence.Name;
        BackgroundColor = Color.FromArgb("#F5F5F5");
        _workspace = new VideoSequenceWorkspaceView(sequence, service, ffmpeg, DisplayAlertAsync);
        Content = new ScrollView { Content = _workspace };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _workspace.RefreshAsync();
    }

    private Task DisplayAlertAsync(string title, string message) => DisplayAlert(title, message, "OK");
}

public class VideoSequenceWorkspaceView : ContentView
{
    private readonly DenseVideoSequence _sequence;
    private readonly DenseVideoSequenceService _service;
    private readonly FFmpegService _ffmpeg;
    private readonly Func<string, string, Task> _alert;
    private readonly VerticalStackLayout _body = new() { Spacing = 12, Padding = 14 };

    public VideoSequenceWorkspaceView(
        DenseVideoSequence sequence,
        DenseVideoSequenceService service,
        FFmpegService ffmpeg,
        Func<string, string, Task> alert)
    {
        _sequence = sequence;
        _service = service;
        _ffmpeg = ffmpeg;
        _alert = alert;
        Content = _body;
    }

    public async Task RefreshAsync()
    {
        var generations = await _service.GetGenerationsAsync(_sequence.Id);
        _body.Children.Clear();
        double total = generations.Sum(x => x.UsableSeconds);
        _body.Children.Add(new Label
        {
            Text = _sequence.Name, FontSize = 24, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C2185B")
        });
        _body.Children.Add(new Label
        {
            Text = $"{_sequence.SequenceType}  •  Story: {(_sequence.StoryName.Length == 0 ? "—" : _sequence.StoryName)}  •  Total usable: {total:0.00}s",
            FontSize = 13, TextColor = Color.FromArgb("#555")
        });
        var done = new Button
        {
            Text = _sequence.IsDone ? "Reopen sequence" : "Good enough / Done",
            BackgroundColor = Color.FromArgb(_sequence.IsDone ? "#ECEFF1" : "#2E7D32"),
            TextColor = _sequence.IsDone ? Color.FromArgb("#333") : Colors.White
        };
        done.Clicked += async (_, _) =>
        {
            _sequence.IsDone = !_sequence.IsDone;
            await _service.SaveSequenceAsync(_sequence);
            await RefreshAsync();
        };
        _body.Children.Add(done);

        if (!_sequence.IsDone)
            _body.Children.Add(await BuildAddGenerationAsync(generations));

        _body.Children.Add(Heading("Generation chain"));
        if (generations.Count == 0) _body.Children.Add(Muted("No attempts yet."));
        foreach (var generation in generations)
            _body.Children.Add(await BuildGenerationCardAsync(generation));

        await BuildStatsAsync();
    }

    private async Task<View> BuildAddGenerationAsync(List<DenseVideoGeneration> generations)
    {
        var previous = generations.LastOrDefault();
        string thumbnailPath = previous?.StartingThumbnailPath ?? _sequence.InitialThumbnailPath;
        var preview = new Image { HeightRequest = 130, Aspect = Aspect.AspectFit };
        void SetPreview() => preview.Source = File.Exists(_service.ResolvePath(thumbnailPath))
            ? ImageSource.FromFile(_service.ResolvePath(thumbnailPath)) : null;
        SetPreview();
        var prompt = new Editor
        {
            Placeholder = "Generation prompt", Text = previous?.Prompt ?? "",
            HeightRequest = 100, AutoSize = EditorAutoSizeOption.TextChanges
        };
        var transition = new Entry { Placeholder = "Transition type (punch, pan, jump, morph...)" };
        var suggestions = await _service.GetTransitionSuggestionsAsync(_sequence.Username);
        var suggestionPicker = new Picker { Title = "Previously used transition tags", ItemsSource = suggestions };
        suggestionPicker.SelectedIndexChanged += (_, _) =>
        {
            if (suggestionPicker.SelectedItem is string value) transition.Text = value;
        };
        var chooseImage = Action("Choose starting thumbnail", "#546E7A");
        chooseImage.Clicked += async (_, _) =>
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose starting thumbnail",
                FileTypes = FilePickerFileType.Images
            });
            if (file == null) return;
            thumbnailPath = await _service.ImportFileAsync(file, "images");
            SetPreview();
        };
        var save = Action("Add generation attempt", "#C2185B");
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(thumbnailPath) || string.IsNullOrWhiteSpace(prompt.Text))
            {
                await _alert("Missing details", "Choose a thumbnail and enter a prompt.");
                return;
            }
            await _service.SaveGenerationAsync(new DenseVideoGeneration
            {
                Username = _sequence.Username, SequenceId = _sequence.Id,
                SequenceOrder = generations.Count + 1,
                StartingThumbnailPath = thumbnailPath,
                Prompt = prompt.Text.Trim(), TransitionType = transition.Text?.Trim() ?? "",
                Status = "pending"
            });
            await RefreshAsync();
        };
        return Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children = { Heading("Next generation"), preview, chooseImage, prompt, transition, suggestionPicker, save }
        });
    }

    private async Task<View> BuildGenerationCardAsync(DenseVideoGeneration generation)
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(new Label
        {
            Text = $"#{generation.SequenceOrder}  {generation.TransitionType}  •  {generation.Status}",
            FontSize = 16, FontAttributes = FontAttributes.Bold
        });
        string imagePath = _service.ResolvePath(generation.StartingThumbnailPath);
        if (File.Exists(imagePath))
            stack.Children.Add(new Image { Source = ImageSource.FromFile(imagePath), HeightRequest = 130, Aspect = Aspect.AspectFit });
        stack.Children.Add(new Label { Text = generation.Prompt, FontSize = 13, TextColor = Color.FromArgb("#333") });
        stack.Children.Add(Muted($"Result: {(generation.ResultClipPath.Length == 0 ? "not attached" : Path.GetFileName(generation.ResultClipPath))}  •  usable {generation.UsableSeconds:0.00}s"));

        var attach = Action("Attach generated clip", "#1565C0");
        attach.Clicked += async (_, _) =>
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Attach generated video",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".mp4", ".mov", ".m4v", ".avi", ".mkv" } },
                    { DevicePlatform.Android, new[] { "video/*" } }
                })
            });
            if (file == null) return;
            generation.ResultClipPath = await _service.ImportFileAsync(file, "clips");
            generation.Status = "pending";
            await _service.SaveGenerationAsync(generation);
            await RefreshAsync();
        };
        stack.Children.Add(attach);

        string clipPath = _service.ResolvePath(generation.ResultClipPath);
        if (File.Exists(clipPath) && generation.Status != "failed")
        {
            stack.Children.Add(new VideoGenerationReviewView(
                generation, clipPath,
                async (start, end) =>
                {
                    generation.UsableStartSeconds = start;
                    generation.UsableEndSeconds = end;
                    generation.Status = "reviewed";
                    await _service.SaveGenerationAsync(generation);
                    await RefreshAsync();
                },
                async (start, end) => await ContinueAsync(generation, clipPath, start, end),
                async () =>
                {
                    generation.Status = "failed";
                    generation.UsableStartSeconds = null;
                    generation.UsableEndSeconds = null;
                    await _service.SaveGenerationAsync(generation);
                    await RefreshAsync();
                }));
        }
        else if (generation.Status == "failed")
            stack.Children.Add(new Label { Text = "Failed — zero usable runs", TextColor = Color.FromArgb("#B71C1C"), FontAttributes = FontAttributes.Bold });

        var delete = Action("Delete generation record", "#B71C1C");
        delete.Clicked += async (_, _) =>
        {
            await _service.DeleteGenerationAsync(generation.Id);
            await RefreshAsync();
        };
        stack.Children.Add(delete);
        return Card(stack);
    }

    private async Task ContinueAsync(DenseVideoGeneration generation, string clipPath, double start, double end)
    {
        generation.UsableStartSeconds = start;
        generation.UsableEndSeconds = end;
        generation.Status = "reviewed";
        await _service.SaveGenerationAsync(generation);

        string temp = Path.Combine(FileSystem.CacheDirectory, $"dense_frame_{Guid.NewGuid():N}.png");
        var result = await _ffmpeg.ExtractFrameAsync(clipPath, end, temp);
        if (!result.success)
        {
            await _alert("Frame extraction failed", result.message);
            return;
        }
        string frame = await _service.ImportFileAsync(temp, "frames");
        try { File.Delete(temp); } catch { }
        var rows = await _service.GetGenerationsAsync(_sequence.Id);
        await _service.SaveGenerationAsync(new DenseVideoGeneration
        {
            Username = _sequence.Username, SequenceId = _sequence.Id,
            SequenceOrder = rows.Count + 1, StartingThumbnailPath = frame,
            Prompt = generation.Prompt, TransitionType = generation.TransitionType,
            Status = "pending"
        });
        await RefreshAsync();
    }

    private async Task BuildStatsAsync()
    {
        _body.Children.Add(Heading("Stats by transition type"));
        foreach (var stat in await _service.GetStatsAsync(_sequence.Username, false))
            _body.Children.Add(StatLabel(stat));
        _body.Children.Add(Heading("Stats by prompt"));
        foreach (var stat in await _service.GetStatsAsync(_sequence.Username, true))
            _body.Children.Add(StatLabel(stat));
    }

    private static Label StatLabel(DenseSequenceStats x) => new()
    {
        Text = $"{x.Key}\n{x.Attempts} attempts • {x.UsableCount} usable • {x.SuccessRate:0.#}% • avg {x.AverageUsableSeconds:0.00}s",
        FontSize = 12, TextColor = Color.FromArgb("#444"), Margin = new Thickness(4)
    };
    private static Label Heading(string text) => new() { Text = text, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") };
    private static Label Muted(string text) => new() { Text = text, FontSize = 12, TextColor = Color.FromArgb("#666") };
    private static Button Action(string text, string color) => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, HeightRequest = 40 };
    private static Frame Card(View content) => new() { Content = content, Padding = 12, CornerRadius = 10, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
}
