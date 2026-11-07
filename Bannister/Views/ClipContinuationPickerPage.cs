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
    private List<string> _continuations = new();
    private int _currentIndex;
    private bool _playingBase;
    private MediaElement _player = null!;
    private Label _statusLabel = null!;
    private Label _noClipsLabel = null!;
    private Label _countLabel = null!;
    private Button _removeBtn = null!;
    private Button _nextBtn = null!;
    private Button _pickBaseBtn = null!;
    private Button _pickFolderBtn = null!;
    private Button _playPauseBtn = null!;

    public ClipContinuationPickerPage()
    {
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
        root.Add(_statusLabel, 0, 1);
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
        _removeBtn.IsEnabled = true;
        _nextBtn.IsEnabled = true;
        _playPauseBtn.IsEnabled = true;
        _noClipsLabel.IsVisible = false;
        await PlayBaseAsync();
    }

    private Task PlayBaseAsync()
    {
        if (_baseClipPath == null) return Task.CompletedTask;
        _playingBase = true;
        _player.Source = MediaSource.FromFile(_baseClipPath);
        _player.Play();
        _playPauseBtn.Text = "⏸ Pause";
        UpdateStatus();
        return Task.CompletedTask;
    }

    private Task PlayCurrentContinuationAsync()
    {
        if (_continuations.Count == 0) return Task.CompletedTask;
        _playingBase = false;
        _player.Source = MediaSource.FromFile(_continuations[_currentIndex]);
        _player.Play();
        _playPauseBtn.Text = "⏸ Pause";
        UpdateStatus();
        return Task.CompletedTask;
    }

    private async void OnMediaEndedAsync(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_continuations.Count == 0) return;
            if (_playingBase)
            {
                await PlayCurrentContinuationAsync();
                return;
            }
            await OnNextAsync();
        });
    }

    private async Task OnNextAsync()
    {
        if (_continuations.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _continuations.Count;
        await PlayBaseAsync();
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
            _playPauseBtn.IsEnabled = false;
            _statusLabel.Text = "No continuations left.";
            _countLabel.Text = "0 remaining";
            return;
        }
        if (_currentIndex >= _continuations.Count) _currentIndex = 0;
        await PlayBaseAsync();
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
}
