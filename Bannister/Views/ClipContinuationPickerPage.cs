using Bannister.Services;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Storage;

namespace Bannister.Views;

public class ClipContinuationPickerPage : Bannister.Views.ModeThemedPage
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v"
    };

    private string? _baseClipPath;
    private readonly FFmpegService _ffmpeg;
    private List<string> _continuations = new();
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
    private Button _copyToFolderBtn = null!;

    public ClipContinuationPickerPage(FFmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;
        Title = "Clip Comparator";

        BuildUI();
#if !WINDOWS
        _pickFolderBtn.Text = " Pick Continuation Clips";
#endif
    }

    private void BuildUI()
    {
        _player = (new MediaElement
        {
            ShouldAutoPlay = true,
            ShouldMute = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground);
        _player.MediaEnded += OnMediaEndedAsync;

        _noClipsLabel = (new Label
        {
            Text = "Pick a base clip and a continuations folder to begin",
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true);

        var preview = new Grid();
        preview.Children.Add(_player);
        preview.Children.Add(_noClipsLabel);

        _statusLabel = (new Label
        {
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = 8
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground);
        _stitchingLabel = (new Label
        {
            Text = "",
            FontSize = 11,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellow, ThemeKey.PageText3, classifyPageText: true);
        var statusArea = (new VerticalStackLayout
        {
            Children = { _statusLabel, _stitchingLabel }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground);

        _pickBaseBtn = CreateButton(" Base Clip", ThemeKey.ButtonPrimaryBackground);
        _pickFolderBtn = CreateButton(" Continuations Folder", ThemeKey.NeutralButtonText);
        _playPauseBtn = CreateButton("⏸ Pause", ThemeKey.NeutralDarkButton);
        _playPauseBtn.IsEnabled = false;
        _removeBtn = CreateButton("✕ Remove", ThemeKey.ErrorText);
        _removeBtn.IsEnabled = false;
        _nextBtn = CreateButton("⏭ Next", ThemeKey.SuccessText);
        _nextBtn.IsEnabled = false;
        _copyToFolderBtn = (new Button
        {
            Text = " Copy",
            CornerRadius = 8,
            HeightRequest = 44,
            Padding = new Thickness(12, 0),
            FontSize = 13,
            IsEnabled = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _countLabel = (new Label
        {
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);

        _pickBaseBtn.Clicked += async (_, _) => await OnPickBaseAsync();
        _pickFolderBtn.Clicked += async (_, _) => await OnPickFolderAsync();
        _removeBtn.Clicked += async (_, _) => await OnRemoveAsync();
        _nextBtn.Clicked += async (_, _) => await OnNextAsync();
        _copyToFolderBtn.Clicked += async (_, _) => await OnCopyToFolderAsync();
        _playPauseBtn.Clicked += (_, _) => OnPlayPauseAsync();

        var controls = (new VerticalStackLayout
        {
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
                    Children = { _removeBtn, _nextBtn, _copyToFolderBtn, _countLabel }
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground);

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

    private static Button CreateButton(string text, ThemeKey backgroundColor) => (new Button(){
        Text = text
    }).ThemeColor(VisualElement.BackgroundColorProperty, backgroundColor).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

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

#if WINDOWS
        _continuations = Directory
            .EnumerateFiles(result.Folder.Path, "*", SearchOption.TopDirectoryOnly)
            .Where(p => VideoExtensions.Contains(Path.GetExtension(p)))
            .OrderBy(p => Path.GetFileName(p))
            .ToList();
#else
        // On Android, FolderPicker.Folder.Path is a content URI.
        // Use the StorageFile approach: pick multiple files instead.
        // Fall back to letting user pick files individually via FilePicker.
        await DisplayAlert("Android",
            "Folder picking isn't supported on Android for video files.\n\nPlease use 'Pick Files' instead.",
            "OK");
        var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "video/*" } }
        });
        var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = "Pick continuation clips",
            FileTypes = fileTypes
        });
        if (files == null) return;
        _continuations = files.Select(f => f.FullPath).OrderBy(Path.GetFileName).ToList();
#endif

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
        _stitchingLabel.IsVisible = false;
        _removeBtn.IsEnabled = true;
        _nextBtn.IsEnabled = true;
        _copyToFolderBtn.IsEnabled = true;
        _playPauseBtn.IsEnabled = true;
        await PlayCurrentAsync();
    }

    private async Task PlayCurrentAsync()
    {
        if (_continuations.Count == 0) return;
        _playingBase = true;
        _player.Source = MediaSource.FromFile(_baseClipPath!);
        _player.Play();
        UpdateStatus();
        await Task.CompletedTask;
    }

    private async void OnMediaEndedAsync(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_playingBase)
            {
                _playingBase = false;
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
        _continuations.RemoveAt(_currentIndex);
        if (_continuations.Count == 0)
        {
            _player.Stop();
            _removeBtn.IsEnabled = false;
            _nextBtn.IsEnabled = false;
            _copyToFolderBtn.IsEnabled = false;
            _playPauseBtn.IsEnabled = false;
            _statusLabel.Text = "No continuations left — winner found!";
            return;
        }
        if (_currentIndex >= _continuations.Count) _currentIndex = 0;
        await PlayCurrentAsync();
    }

    private async Task OnCopyToFolderAsync()
    {
        if (_continuations.Count == 0 || _currentIndex >= _continuations.Count)
            return;

        string src = _continuations[_currentIndex];

        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful) return;

#if WINDOWS
        string destFolder = result.Folder.Path;
#else
        string destFolder = result.Folder.Path; // content URI on Android — may need handling
#endif

        string dest = Path.Combine(destFolder, Path.GetFileName(src));
        if (File.Exists(dest))
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(src);
            string ext = Path.GetExtension(src);
            dest = Path.Combine(destFolder,
                $"{nameNoExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
        }

        try
        {
            File.Copy(src, dest);
            _statusLabel.Text = $"✓ Copied to {Path.GetFileName(destFolder)}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Copy failed", ex.Message, "OK");
        }
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
    }
}
