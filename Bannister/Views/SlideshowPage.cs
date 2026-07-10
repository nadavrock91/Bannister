using Bannister.Services;
using System.Globalization;
using System.Text;

namespace Bannister.Views;

public class SlideshowPage : ContentPage
{
    private readonly AuthService _auth;
    private string? _sourceFolder;
    private List<string> _sourceImages = new();
    private string? _outputPath;
    private double _slideDurationSeconds = 3.0;
    private bool _randomOrder = false;
    private string _resolutionPreset = "1080p Landscape (1920x1080)";
    private bool _isGenerating = false;

    private Label _sourceFolderLabel = null!;
    private Label _imageCountLabel = null!;
    private Picker _durationPicker = null!;
    private Entry _customDurationEntry = null!;
    private Switch _randomOrderSwitch = null!;
    private Picker _resolutionPicker = null!;
    private Label _outputPathLabel = null!;
    private Button _generateButton = null!;
    private ActivityIndicator _progressIndicator = null!;
    private Label _progressLabel = null!;

    public SlideshowPage(AuthService auth)
    {
        _auth = auth;

        Title = "Slideshow";
        BackgroundColor = Color.FromArgb("#F5F5F5");

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
            Text = "️ Slideshow",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C2185B")
        });

        stack.Children.Add(new Label
        {
            Text = "Turn a folder of images into an MP4 slideshow.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(BuildSourceSection());
        stack.Children.Add(BuildOptionsSection());
        stack.Children.Add(BuildOutputSection());
        stack.Children.Add(BuildGenerateSection());

        Content = new ScrollView { Content = stack };
    }

    private View BuildSourceSection()
    {
        _sourceFolderLabel = new Label
        {
            Text = "(not set)",
            FontSize = 12,
            TextColor = Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var pickButton = SmallButton("Pick folder", "#C2185B");
        pickButton.Clicked += OnPickSourceFolderClicked;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        row.Add(_sourceFolderLabel, 0, 0);
        row.Add(pickButton, 1, 0);

        _imageCountLabel = new Label
        {
            Text = "No folder selected.",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        };

        return SectionFrame(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                SectionTitle("Source folder:"),
                row,
                _imageCountLabel
            }
        });
    }

    private View BuildOptionsSection()
    {
        _durationPicker = new Picker
        {
            Title = "Slide duration",
            ItemsSource = new List<string> { "1 second", "2 seconds", "3 seconds", "5 seconds", "10 seconds", "Custom..." },
            SelectedIndex = 2
        };
        _durationPicker.SelectedIndexChanged += (_, _) =>
        {
            var selected = _durationPicker.SelectedItem as string ?? "";
            if (selected == "Custom...")
            {
                _customDurationEntry.IsVisible = true;
            }
            else
            {
                _customDurationEntry.IsVisible = false;
                _slideDurationSeconds = selected switch
                {
                    "1 second" => 1.0,
                    "2 seconds" => 2.0,
                    "3 seconds" => 3.0,
                    "5 seconds" => 5.0,
                    "10 seconds" => 10.0,
                    _ => 3.0
                };
            }
        };

        _customDurationEntry = new Entry
        {
            Placeholder = "Custom duration in seconds",
            Keyboard = Keyboard.Numeric,
            IsVisible = false,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
        _customDurationEntry.TextChanged += (_, _) =>
        {
            if (double.TryParse(_customDurationEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) && val > 0)
                _slideDurationSeconds = val;
        };

        _randomOrderSwitch = new Switch { IsToggled = false };
        _randomOrderSwitch.Toggled += (_, e) => _randomOrder = e.Value;

        var randomRow = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                _randomOrderSwitch,
                new Label
                {
                    Text = "Random order (off = alphabetical)",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#333"),
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

        _resolutionPicker = new Picker
        {
            Title = "Resolution",
            ItemsSource = new List<string>
            {
                "720p Landscape (1280x720)",
                "1080p Landscape (1920x1080)",
                "1080p Portrait (1080x1920)",
                "4K Landscape (3840x2160)"
            },
            SelectedIndex = 1
        };
        _resolutionPicker.SelectedIndexChanged += (_, _) =>
        {
            _resolutionPreset = _resolutionPicker.SelectedItem as string ?? "1080p Landscape (1920x1080)";
        };

        return SectionFrame(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                SectionTitle("Options"),
                new Label { Text = "Slide duration:", FontSize = 12, TextColor = Color.FromArgb("#666") },
                _durationPicker,
                _customDurationEntry,
                randomRow,
                new Label { Text = "Resolution:", FontSize = 12, TextColor = Color.FromArgb("#666") },
                _resolutionPicker
            }
        });
    }

    private View BuildOutputSection()
    {
        _outputPathLabel = new Label
        {
            Text = "(not set)",
            FontSize = 12,
            TextColor = Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var pickButton = SmallButton("Pick output...", "#C2185B");
        pickButton.Clicked += OnPickOutputClicked;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        row.Add(_outputPathLabel, 0, 0);
        row.Add(pickButton, 1, 0);

        return SectionFrame(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                SectionTitle("Output file:"),
                row
            }
        });
    }

    private View BuildGenerateSection()
    {
        _generateButton = new Button
        {
            Text = " Generate Video",
            BackgroundColor = Color.FromArgb("#C2185B"),
            TextColor = Colors.White,
            HeightRequest = 56,
            CornerRadius = 12,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16
        };
        _generateButton.Clicked += OnGenerateClicked;

        _progressIndicator = new ActivityIndicator
        {
            Color = Color.FromArgb("#C2185B"),
            IsVisible = false,
            IsRunning = false
        };

        _progressLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };

        return SectionFrame(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                _generateButton,
                _progressIndicator,
                _progressLabel
            }
        });
    }

    private async void OnPickSourceFolderClicked(object? sender, EventArgs e)
    {
        var last = await SecureStorage.GetAsync("slideshow_last_source_folder") ?? "";
        var picked = await WebsiteFolderHelper.PickParentFolderPathAsync(this);
        if (string.IsNullOrWhiteSpace(picked))
        {
            picked = await DisplayPromptAsync(
                "Pick source folder",
                "Paste folder path:",
                initialValue: last);
        }

        if (string.IsNullOrWhiteSpace(picked)) return;
        picked = picked.Trim().Trim('"');
        if (!Directory.Exists(picked))
        {
            await DisplayAlert("Folder not found", "That folder does not exist.", "OK");
            return;
        }

        _sourceFolder = picked;
        await SecureStorage.SetAsync("slideshow_last_source_folder", picked);
        await ScanSourceImagesAsync();
        _sourceFolderLabel.Text = $"Folder: {picked}";
        _imageCountLabel.Text = _sourceImages.Count == 0
            ? "No images found in this folder."
            : $"{_sourceImages.Count} image(s) found.";
    }

    private Task ScanSourceImagesAsync()
    {
        _sourceImages.Clear();
        if (string.IsNullOrWhiteSpace(_sourceFolder) || !Directory.Exists(_sourceFolder))
            return Task.CompletedTask;

        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        _sourceImages = Directory.EnumerateFiles(_sourceFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(p => exts.Contains(Path.GetExtension(p)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.CompletedTask;
    }

    private async void OnPickOutputClicked(object? sender, EventArgs e)
    {
        var defaultName = $"slideshow_{DateTime.Now:yyyy-MM-dd_HHmmss}.mp4";
        var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var defaultPath = Path.Combine(defaultDir, defaultName);

        var picked = await DisplayPromptAsync(
            "Output file",
            "Paste output file path (must end in .mp4):",
            initialValue: defaultPath);

        if (string.IsNullOrWhiteSpace(picked)) return;
        picked = picked.Trim().Trim('"');

        if (!picked.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            picked += ".mp4";

        var outputDir = Path.GetDirectoryName(picked);
        if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir))
        {
            await DisplayAlert("Invalid output", "Output directory does not exist.", "OK");
            return;
        }

        _outputPath = picked;
        _outputPathLabel.Text = $"Output: {picked}";
    }

    private async void OnGenerateClicked(object? sender, EventArgs e)
    {
        if (_isGenerating) return;
        if (string.IsNullOrWhiteSpace(_sourceFolder) || _sourceImages.Count == 0)
        {
            await DisplayAlert("No source", "Pick a source folder with images first.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            await DisplayAlert("No output", "Pick an output file path first.", "OK");
            return;
        }
        if (_slideDurationSeconds <= 0)
        {
            await DisplayAlert("Invalid duration", "Slide duration must be greater than 0 seconds.", "OK");
            return;
        }

        _isGenerating = true;
        _generateButton.IsEnabled = false;
        _progressIndicator.IsVisible = true;
        _progressIndicator.IsRunning = true;
        _progressLabel.IsVisible = true;
        _progressLabel.Text = "Preparing images...";

        try
        {
            var images = new List<string>(_sourceImages);
            if (_randomOrder)
            {
                var rng = new Random();
                images = images.OrderBy(_ => rng.Next()).ToList();
            }

            var (width, height) = ParseResolution(_resolutionPreset);

            _progressLabel.Text = "Encoding video (this may take a while)...";
            await Task.Run(async () => await GenerateSlideshowAsync(images, _outputPath!, _slideDurationSeconds, width, height));

            await DisplayAlert("Done", $"Slideshow saved to:\n{_outputPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Generation failed", ex.Message, "OK");
        }
        finally
        {
            _isGenerating = false;
            _generateButton.IsEnabled = true;
            _progressIndicator.IsVisible = false;
            _progressIndicator.IsRunning = false;
            _progressLabel.IsVisible = false;
        }
    }

    private static (int Width, int Height) ParseResolution(string preset) => preset switch
    {
        "720p Landscape (1280x720)" => (1280, 720),
        "1080p Portrait (1080x1920)" => (1080, 1920),
        "4K Landscape (3840x2160)" => (3840, 2160),
        _ => (1920, 1080)
    };

    private static async Task GenerateSlideshowAsync(List<string> images, string outputPath, double durationPerSlide, int width, int height)
    {
        var ffmpegDir = Path.Combine(FileSystem.CacheDirectory, "ffmpeg");
        Directory.CreateDirectory(ffmpegDir);
        await Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(Xabe.FFmpeg.Downloader.FFmpegVersion.Official, ffmpegDir);
        Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegDir);

        var concatFile = Path.Combine(Path.GetTempPath(), $"slideshow_concat_{Guid.NewGuid():N}.txt");
        var sb = new StringBuilder();
        foreach (var img in images)
        {
            sb.AppendLine($"file '{EscapeConcatPath(img)}'");
            sb.AppendLine($"duration {durationPerSlide.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
        sb.AppendLine($"file '{EscapeConcatPath(images[^1])}'");
        await File.WriteAllTextAsync(concatFile, sb.ToString());

        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            var conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                .AddParameter($"-f concat -safe 0 -i \"{concatFile}\"", Xabe.FFmpeg.ParameterPosition.PreInput)
                .AddParameter($"-vf \"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black,setsar=1\"")
                .AddParameter("-c:v libx264 -pix_fmt yuv420p -r 30")
                .SetOutput(outputPath);

            await conversion.Start();
        }
        finally
        {
            try { File.Delete(concatFile); } catch { }
        }
    }

    private static string EscapeConcatPath(string path)
    {
        return path
            .Replace("\\", "/", StringComparison.Ordinal)
            .Replace("'", "'\\''", StringComparison.Ordinal);
    }

    private static Frame SectionFrame(View content) => new()
    {
        BackgroundColor = Colors.White,
        BorderColor = Color.FromArgb("#F8BBD0"),
        CornerRadius = 12,
        Padding = 14,
        HasShadow = false,
        Content = content
    };

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#333")
    };

    private static Button SmallButton(string text, string color) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(color),
        TextColor = Colors.White,
        CornerRadius = 8,
        HeightRequest = 38,
        FontSize = 12
    };
}
