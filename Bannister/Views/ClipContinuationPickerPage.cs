using Bannister.Services;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Storage;

namespace Bannister.Views;

public class ClipContinuationPickerPage : ContentPage
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v"
    };

    private string? _baseClipPath;
    private readonly FFmpegService _ffmpeg;
    private List<string> _continuations = new();
    private List<string> _stitchedPaths = new();
    private string? _ffmpegPath;
    private bool _stitchingDone = false;
    private int _currentIndex;
    private bool _playingBase;
    private MediaElement _player = null!;
    private Label _statusLabel = null!;
    private Label _noClipsLabel = null!;
    private Label _countLabel = null!;
    private Label _stitchingLabel = null!;
    private Button _removeBtn = null!;
    private Button _nextBtn = null!;
    private Button _pickBaseBtn = null!;
    private Button _pickFolderBtn = null!;
    private Button _playPauseBtn = null!;

    public ClipContinuationPickerPage(FFmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;
        Title = "Clip Comparator";
        BackgroundColor = Color.FromArgb("#111111");
        BuildUI();
    }

    private void BuildUI()
    {
        _player = new MediaElement
        {
            BackgroundColor = Colors.Black,
            ShouldAutoPlay = true,
            ShouldMute = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _player.MediaEnded += OnMediaEndedAsync;

        _noClipsLabel = new Label
        {
            Text = "Pick a base clip and a continuations folder to begin",
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var preview = new Grid();
        preview.Children.Add(_player);
        preview.Children.Add(_noClipsLabel);

        _statusLabel = new Label
        {
            TextColor = Colors.White,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = 8,
            BackgroundColor = Color.FromArgb("#1A1A1A")
        };
        _stitchingLabel = new Label
        {
            Text = "",
            FontSize = 11,
            TextColor = Color.FromArgb("#FFCC80"),
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        var statusArea = new VerticalStackLayout
        {
            BackgroundColor = Color.FromArgb("#1A1A1A"),
            Children = { _statusLabel, _stitchingLabel }
        };

        _pickBaseBtn = CreateButton(" Base Clip", "#1565C0");
        _pickFolderBtn = CreateButton(" Continuations Folder", "#37474F");
        _playPauseBtn = CreateButton("⏸ Pause", "#424242");
        _playPauseBtn.IsEnabled = false;
        _removeBtn = CreateButton("✕ Remove", "#B71C1C");
        _removeBtn.IsEnabled = false;
        _nextBtn = CreateButton("⏭ Next", "#1B5E20");
        _nextBtn.IsEnabled = false;
        _countLabel = new Label
        {
            TextColor = Colors.White,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };

        _pickBaseBtn.Clicked += async (_, _) => await OnPickBaseAsync();
        _pickFolderBtn.Clicked += async (_, _) => await OnPickFolderAsync();
        _removeBtn.Clicked += async (_, _) => await OnRemoveAsync();
        _nextBtn.Clicked += async (_, _) => await OnNextAsync();
        _playPauseBtn.Clicked += (_, _) => OnPlayPauseAsync();

        var controls = new VerticalStackLayout
        {
            BackgroundColor = Color.FromArgb("#1A1A1A"),
            Padding = 8,
            Spacing = 8,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { _pickBaseBtn, _pickFolderBtn, _playPauseBtn }
                },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { _removeBtn, _nextBtn, _countLabel }
                }
            }
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Add(preview, 0, 0);
        root.Add(statusArea, 0, 1);
        root.Add(controls, 0, 2);
        Content = root;
        UpdateStatus();
    }

    private static Button CreateButton(string text, string backgroundColor) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(backgroundColor),
        TextColor = Colors.White
    };

    private async Task OnPickBaseAsync()
    {
        var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, VideoExtensions },
            { DevicePlatform.Android, new[] { "video/*" } }
        });
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Pick a base clip",
            FileTypes = fileTypes
        });
        if (result == null) return;

        _baseClipPath = result.FullPath;
        if (_continuations.Count > 0)
            await StartSessionAsync();
        else
            UpdateStatus();
    }

    private async Task OnPickFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful) return;

        _continuations = Directory.EnumerateFiles(result.Folder.Path, "*", SearchOption.TopDirectoryOnly)
            .Where(path => VideoExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path))
            .ToList();
        _currentIndex = 0;
        if (_baseClipPath != null && _continuations.Count > 0)
            await StartSessionAsync();
        else
            UpdateStatus();
    }

    private async Task StartSessionAsync()
    {
        _currentIndex = 0;
        _playingBase = true;
        _noClipsLabel.IsVisible = false;
        _playPauseBtn.IsEnabled = false;
        _removeBtn.IsEnabled = false;
        _nextBtn.IsEnabled = false;

#if WINDOWS
        // Clean up previous stitched files
        foreach (var p in _stitchedPaths)
            try { if (IsGeneratedStitchPath(p)) File.Delete(p); } catch { }
        _stitchedPaths.Clear();
        _stitchingDone = false;
        _stitchingLabel.IsVisible = true;
        _stitchingLabel.Text = "Preparing FFmpeg...";

        _ffmpegPath ??= await _ffmpeg.EnsureFFmpegAsync();
        string tempDir = Path.Combine(FileSystem.CacheDirectory, "clip_comparator");
        Directory.CreateDirectory(tempDir);

        for (int i = 0; i < _continuations.Count; i++)
        {
            _stitchingLabel.Text = $"Stitching {i + 1} of {_continuations.Count}...";
            string outPath = Path.Combine(tempDir, $"stitch_{i:D3}.mp4");
            var result = await _ffmpeg.ConcatVideosAsync(
                _ffmpegPath,
                new List<string> { _baseClipPath!, _continuations[i] },
                outPath);
            _stitchedPaths.Add(result.success ? outPath : _continuations[i]);
        }

        _stitchingLabel.Text = $"✓ {_stitchedPaths.Count} clips ready";
        _stitchingDone = true;
#else
        _stitchingDone = false;
#endif

        _removeBtn.IsEnabled = true;
        _nextBtn.IsEnabled = true;
        _playPauseBtn.IsEnabled = true;
        await PlayCurrentAsync();
    }

    private async Task PlayCurrentAsync()
    {
        if (_continuations.Count == 0) return;
        _player.Stop();

#if WINDOWS
        if (_stitchingDone && _currentIndex < _stitchedPaths.Count)
        {
            _player.Source = MediaSource.FromFile(_stitchedPaths[_currentIndex]);
            _player.Play();
            UpdateStatus();
            return;
        }
#endif

        // Fallback: play base then continuation
        _playingBase = true;
        _player.Source = MediaSource.FromFile(_baseClipPath!);
        _player.Play();
        UpdateStatus();
        await Task.CompletedTask;
    }

    private async void OnMediaEndedAsync(object? sender, EventArgs e)
    {
#if WINDOWS
        if (_stitchingDone) return;
#endif
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_playingBase)
            {
                _playingBase = false;
                _player.Stop();
                _player.Source = MediaSource.FromFile(
                    _continuations[_currentIndex]);
                _player.Play();
                UpdateStatus();
            }
            await Task.CompletedTask;
        });
    }

    private async Task OnNextAsync()
    {
        if (_continuations.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _continuations.Count;
        await PlayCurrentAsync();
    }

    private async Task OnRemoveAsync()
    {
        if (_continuations.Count == 0) return;
#if WINDOWS
        if (_currentIndex < _stitchedPaths.Count)
        {
            try
            {
                if (IsGeneratedStitchPath(_stitchedPaths[_currentIndex]))
                    File.Delete(_stitchedPaths[_currentIndex]);
            }
            catch { }
            _stitchedPaths.RemoveAt(_currentIndex);
        }
#endif
        _continuations.RemoveAt(_currentIndex);
        if (_continuations.Count == 0)
        {
            _player.Stop();
            _removeBtn.IsEnabled = false;
            _nextBtn.IsEnabled = false;
            _playPauseBtn.IsEnabled = false;
            _statusLabel.Text = "No continuations left — winner found!";
            return;
        }
        if (_currentIndex >= _continuations.Count) _currentIndex = 0;
        await PlayCurrentAsync();
    }

    private void OnPlayPauseAsync()
    {
        if (_player.CurrentState == MediaElementState.Playing)
        {
            _player.Pause();
            _playPauseBtn.Text = "▶ Play";
        }
        else
        {
            _player.Play();
            _playPauseBtn.Text = "⏸ Pause";
        }
    }

    private void UpdateStatus()
    {
        _countLabel.Text = $"{_continuations.Count} remaining";
        if (_baseClipPath == null)
        {
            _statusLabel.Text = "Pick a base clip.";
            return;
        }
        if (_continuations.Count == 0)
        {
            _statusLabel.Text = "Pick a continuations folder.";
            return;
        }
        string phase = _playingBase ? "▶ Base" : "▶ Continuation";
        _statusLabel.Text = $"{phase}  —  {_currentIndex + 1} of {_continuations.Count} remaining";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _player.Stop();
#if WINDOWS
        foreach (var p in _stitchedPaths)
            try { if (IsGeneratedStitchPath(p)) File.Delete(p); } catch { }
        _stitchedPaths.Clear();
#endif
    }

    private static bool IsGeneratedStitchPath(string path)
    {
        string tempDir = Path.GetFullPath(
            Path.Combine(FileSystem.CacheDirectory, "clip_comparator"));
        string fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(tempDir + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }
}
