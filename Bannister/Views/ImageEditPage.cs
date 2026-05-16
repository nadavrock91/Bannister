using SkiaSharp;

namespace Bannister.Views;

public class ImageEditPage : ContentPage
{
    private const string PrimaryColor = "#5B63EE";
    private const string LightButtonColor = "#ECEFF1";

    private readonly Label _sourcePathLabel;
    private readonly Image _sourcePreview;
    private readonly Label _sourceDimensionsLabel;
    private readonly Picker _presetPicker;
    private readonly Entry _customWidthEntry;
    private readonly Entry _customHeightEntry;
    private readonly Grid _customSizeGrid;
    private readonly Label _statusLabel;
    private readonly Button _cropModeButton;
    private readonly Button _scaleModeButton;
    private readonly VerticalStackLayout _anchorSection;
    private readonly Button _actionButton;
    private readonly List<Button> _anchorButtons = new();

    private string? _sourceWorkingPath;
    private string? _sourceFileName;
    private int _sourceWidth;
    private int _sourceHeight;
    private CropAnchor _selectedAnchor = CropAnchor.TopLeft;
    private CropMode _selectedMode = CropMode.Crop;

    public ImageEditPage()
    {
        Title = "Image Edit";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _sourcePathLabel = new Label
        {
            Text = "No image selected.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _sourcePreview = new Image
        {
            IsVisible = false,
            Aspect = Aspect.AspectFit,
            MaximumHeightRequest = 300,
            HeightRequest = 260,
            BackgroundColor = Color.FromArgb("#EEEEEE")
        };

        _sourceDimensionsLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            IsVisible = false
        };

        _presetPicker = CreatePicker("Resolution preset");
        _presetPicker.ItemsSource = new List<string>
        {
            "1080 x 1920 (portrait)",
            "720 x 1280 (portrait)",
            "1920 x 1080 (landscape)",
            "1280 x 720 (landscape)",
            "Custom..."
        };
        _presetPicker.SelectedIndexChanged += (_, _) =>
        {
            _customSizeGrid.IsVisible = _presetPicker.SelectedItem?.ToString() == "Custom...";
        };

        _customWidthEntry = CreateEntry("Width");
        _customHeightEntry = CreateEntry("Height");

        _customSizeGrid = new Grid
        {
            IsVisible = false,
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
        };
        Grid.SetColumn(_customWidthEntry, 0);
        Grid.SetColumn(_customHeightEntry, 1);
        _customSizeGrid.Children.Add(_customWidthEntry);
        _customSizeGrid.Children.Add(_customHeightEntry);

        _statusLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _cropModeButton = CreateModeButton("Crop", CropMode.Crop);
        _scaleModeButton = CreateModeButton("Scale", CropMode.Scale);
        _anchorSection = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Crop anchor",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333")
                },
                BuildAnchorGrid()
            }
        };

        _actionButton = new Button
        {
            Text = "Crop and Save",
            BackgroundColor = Color.FromArgb(PrimaryColor),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };
        _actionButton.Clicked += OnCropAndSaveClicked;
        UpdateModeButtons();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 16,
                Children =
                {
                    BuildCropToolFrame()
                    // Future tools added here as additional Frames.
                }
            }
        };
    }

    private Frame BuildCropToolFrame()
    {
        var browseButton = new Button
        {
            Text = "Browse Image",
            BackgroundColor = Color.FromArgb(PrimaryColor),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 46
        };
        browseButton.Clicked += OnBrowseImageClicked;

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#DDDDDD"),
            CornerRadius = 8,
            HasShadow = false,
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Crop to Resolution",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    BuildModeToggle(),
                    browseButton,
                    _sourcePathLabel,
                    _sourcePreview,
                    _sourceDimensionsLabel,
                    _presetPicker,
                    _customSizeGrid,
                    _anchorSection,
                    _actionButton,
                    _statusLabel
                }
            }
        };
    }

    private Grid BuildModeToggle()
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        Grid.SetColumn(_cropModeButton, 0);
        Grid.SetColumn(_scaleModeButton, 1);
        grid.Children.Add(_cropModeButton);
        grid.Children.Add(_scaleModeButton);
        return grid;
    }

    private Button CreateModeButton(string text, CropMode mode)
    {
        var button = new Button
        {
            Text = text,
            CornerRadius = 8,
            HeightRequest = 42,
            FontAttributes = FontAttributes.Bold,
            BindingContext = mode
        };
        button.Clicked += (_, _) =>
        {
            _selectedMode = mode;
            UpdateModeButtons();
        };

        return button;
    }

    private void UpdateModeButtons()
    {
        UpdateModeButton(_cropModeButton, CropMode.Crop);
        UpdateModeButton(_scaleModeButton, CropMode.Scale);

        _anchorSection.IsVisible = _selectedMode == CropMode.Crop;
        _actionButton.Text = _selectedMode == CropMode.Crop ? "Crop and Save" : "Scale and Save";
    }

    private void UpdateModeButton(Button button, CropMode mode)
    {
        bool selected = _selectedMode == mode;
        button.BackgroundColor = selected ? Color.FromArgb(PrimaryColor) : Color.FromArgb(LightButtonColor);
        button.TextColor = selected ? Colors.White : Color.FromArgb("#333333");
    }

    private Grid BuildAnchorGrid()
    {
        var grid = new Grid
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        AddAnchorButton(grid, CropAnchor.TopLeft, "Top-Left", 0, 0);
        AddAnchorButton(grid, CropAnchor.TopCenter, "Top-Center", 0, 1);
        AddAnchorButton(grid, CropAnchor.TopRight, "Top-Right", 0, 2);
        AddAnchorButton(grid, CropAnchor.MiddleLeft, "Middle-Left", 1, 0);
        AddAnchorButton(grid, CropAnchor.Center, "Center", 1, 1);
        AddAnchorButton(grid, CropAnchor.MiddleRight, "Middle-Right", 1, 2);
        AddAnchorButton(grid, CropAnchor.BottomLeft, "Bottom-Left", 2, 0);
        AddAnchorButton(grid, CropAnchor.BottomCenter, "Bottom-Center", 2, 1);
        AddAnchorButton(grid, CropAnchor.BottomRight, "Bottom-Right", 2, 2);

        UpdateAnchorButtons();
        return grid;
    }

    private void AddAnchorButton(Grid grid, CropAnchor anchor, string text, int row, int column)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            CornerRadius = 8,
            HeightRequest = 42,
            Padding = new Thickness(4, 0)
        };
        button.Clicked += (_, _) =>
        {
            _selectedAnchor = anchor;
            UpdateAnchorButtons();
        };

        button.BindingContext = anchor;
        _anchorButtons.Add(button);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    private void UpdateAnchorButtons()
    {
        foreach (var button in _anchorButtons)
        {
            bool selected = button.BindingContext is CropAnchor anchor && anchor == _selectedAnchor;
            button.BackgroundColor = selected ? Color.FromArgb(PrimaryColor) : Color.FromArgb(LightButtonColor);
            button.TextColor = selected ? Colors.White : Color.FromArgb("#333333");
        }
    }

    private async void OnBrowseImageClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select image",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" } },
                    { DevicePlatform.Android, new[] { "image/png", "image/jpeg", "image/bmp", "image/webp" } }
                })
            });

            if (result == null)
                return;

            _sourceFileName = result.FileName;
            _sourceWorkingPath = await CopyPickedFileToCacheAsync(result);

            using var bitmap = SKBitmap.Decode(_sourceWorkingPath);
            if (bitmap == null)
            {
                SetError("Could not read the selected image.");
                return;
            }

            _sourceWidth = bitmap.Width;
            _sourceHeight = bitmap.Height;

            _sourcePathLabel.Text = result.FullPath ?? result.FileName;
            _sourcePreview.Source = ImageSource.FromFile(_sourceWorkingPath);
            _sourcePreview.IsVisible = true;
            _sourceDimensionsLabel.Text = $"Source: {_sourceWidth} x {_sourceHeight}";
            _sourceDimensionsLabel.IsVisible = true;
            SetStatus("");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async void OnCropAndSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_sourceWorkingPath) || !File.Exists(_sourceWorkingPath))
        {
            SetError("Pick an image first");
            return;
        }

        if (!TryGetTargetSize(out int targetWidth, out int targetHeight, out var error))
        {
            SetError(error);
            return;
        }

        try
        {
            double sourceAspect = (double)_sourceWidth / _sourceHeight;
            double targetAspect = (double)targetWidth / targetHeight;
            bool aspectMatches = Math.Abs(sourceAspect - targetAspect) < 0.001;

            if (_selectedMode == CropMode.Crop)
            {
                if (_sourceWidth < targetWidth || _sourceHeight < targetHeight)
                {
                    await DisplayAlert(
                        "Source image is too small",
                        $"Source is {_sourceWidth}x{_sourceHeight}, target is {targetWidth}x{targetHeight}. Pick a larger image or smaller target resolution.",
                        "OK");
                    return;
                }

                var croppedPath = CropAndSave(_sourceWorkingPath, _sourceFileName ?? "image", targetWidth, targetHeight);
                SetStatus($"Cropped image saved to: {croppedPath}");
                return;
            }

            if (!aspectMatches)
            {
                await DisplayAlert(
                    "Aspect ratios don't match",
                    $"Source is {_sourceWidth}x{_sourceHeight} (aspect {sourceAspect:F3}), target is {targetWidth}x{targetHeight} (aspect {targetAspect:F3}). Scale only works when source and target have the same aspect ratio. Switch to Crop mode to extract a region with anchor.",
                    "OK");
                return;
            }

            var scaledPath = ScaleAndSave(_sourceWorkingPath, _sourceFileName ?? "image", targetWidth, targetHeight);
            SetStatus($"Scaled image saved to: {scaledPath}");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private bool TryGetTargetSize(out int width, out int height, out string error)
    {
        width = 0;
        height = 0;
        error = "";

        var preset = _presetPicker.SelectedItem?.ToString();
        switch (preset)
        {
            case "1080 x 1920 (portrait)":
                width = 1080;
                height = 1920;
                return true;
            case "720 x 1280 (portrait)":
                width = 720;
                height = 1280;
                return true;
            case "1920 x 1080 (landscape)":
                width = 1920;
                height = 1080;
                return true;
            case "1280 x 720 (landscape)":
                width = 1280;
                height = 720;
                return true;
            case "Custom...":
                if (int.TryParse(_customWidthEntry.Text, out width) &&
                    int.TryParse(_customHeightEntry.Text, out height) &&
                    width > 0 &&
                    height > 0)
                {
                    return true;
                }

                error = "Enter valid width and height";
                return false;
            default:
                error = "Select a resolution preset";
                return false;
        }
    }

    private string CropAndSave(string sourcePath, string sourceFileName, int targetWidth, int targetHeight)
    {
        using var source = SKBitmap.Decode(sourcePath);
        if (source == null)
            throw new InvalidOperationException("Could not read the selected image.");

        var rect = ComputeCropRect(source.Width, source.Height, targetWidth, targetHeight);
        using var cropped = new SKBitmap(targetWidth, targetHeight, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(source, rect, new SKRect(0, 0, targetWidth, targetHeight));

        var (format, extension) = GetOutputFormat(sourceFileName);
        var outputPath = GetAvailableOutputPath(sourceFileName, targetWidth, targetHeight, extension);

        using var image = SKImage.FromBitmap(cropped);
        using var data = image.Encode(format, 95);
        if (data == null)
            throw new InvalidOperationException("Could not encode cropped image.");

        try
        {
            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
        }
        catch
        {
            var fallbackPath = GetAvailableOutputPath(sourceFileName, targetWidth, targetHeight, extension, FileSystem.AppDataDirectory);
            using var stream = File.Open(fallbackPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
            return fallbackPath;
        }

        return outputPath;
    }

    private string ScaleAndSave(string sourcePath, string sourceFileName, int targetWidth, int targetHeight)
    {
        using var source = SKBitmap.Decode(sourcePath);
        if (source == null)
            throw new InvalidOperationException("Could not read the selected image.");

        var info = new SKImageInfo(targetWidth, targetHeight, source.ColorType, source.AlphaType);
        using var scaled = source.Resize(info, SKFilterQuality.High);
        if (scaled == null)
            throw new InvalidOperationException("Could not scale the image.");

        var (format, extension) = GetOutputFormat(sourceFileName);
        var outputPath = GetAvailableOutputPath(sourceFileName, targetWidth, targetHeight, extension);

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(format, 95);
        if (data == null)
            throw new InvalidOperationException("Could not encode scaled image.");

        try
        {
            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
        }
        catch
        {
            var fallbackPath = GetAvailableOutputPath(sourceFileName, targetWidth, targetHeight, extension, FileSystem.AppDataDirectory);
            using var stream = File.Open(fallbackPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
            return fallbackPath;
        }

        return outputPath;
    }

    private SKRectI ComputeCropRect(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        int x = _selectedAnchor switch
        {
            CropAnchor.TopCenter or CropAnchor.Center or CropAnchor.BottomCenter => (sourceWidth - targetWidth) / 2,
            CropAnchor.TopRight or CropAnchor.MiddleRight or CropAnchor.BottomRight => sourceWidth - targetWidth,
            _ => 0
        };

        int y = _selectedAnchor switch
        {
            CropAnchor.MiddleLeft or CropAnchor.Center or CropAnchor.MiddleRight => (sourceHeight - targetHeight) / 2,
            CropAnchor.BottomLeft or CropAnchor.BottomCenter or CropAnchor.BottomRight => sourceHeight - targetHeight,
            _ => 0
        };

        return new SKRectI(x, y, x + targetWidth, y + targetHeight);
    }

    private static (SKEncodedImageFormat Format, string Extension) GetOutputFormat(string sourceFileName)
    {
        var extension = Path.GetExtension(sourceFileName).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg"
            ? (SKEncodedImageFormat.Jpeg, ".jpg")
            : (SKEncodedImageFormat.Png, ".png");
    }

    private static string GetAvailableOutputPath(string sourceFileName, int width, int height, string extension, string? directoryOverride = null)
    {
        var directory = directoryOverride ?? GetDefaultOutputDirectory();
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var fileName = $"{baseName}_cropped_{width}x{height}{extension}";
        var path = Path.Combine(directory, fileName);

        int suffix = 2;
        while (File.Exists(path))
        {
            fileName = $"{baseName}_cropped_{width}x{height}_{suffix}{extension}";
            path = Path.Combine(directory, fileName);
            suffix++;
        }

        return path;
    }

    private static string GetDefaultOutputDirectory()
    {
#if ANDROID
        try
        {
            var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
            if (!string.IsNullOrWhiteSpace(downloads?.AbsolutePath))
                return downloads.AbsolutePath;
        }
        catch
        {
        }

        return FileSystem.AppDataDirectory;
#else
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return string.IsNullOrWhiteSpace(desktop) ? FileSystem.AppDataDirectory : desktop;
#endif
    }

    private static async Task<string> CopyPickedFileToCacheAsync(FileResult result)
    {
        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".img";

        var targetPath = Path.Combine(FileSystem.CacheDirectory, $"image_edit_source_{Guid.NewGuid():N}{extension}");
        await using var input = await result.OpenReadAsync();
        await using var output = File.Open(targetPath, FileMode.Create, FileAccess.Write);
        await input.CopyToAsync(output);
        return targetPath;
    }

    private static Entry CreateEntry(string placeholder)
    {
        return new Entry
        {
            Placeholder = placeholder,
            Keyboard = Keyboard.Numeric,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
    }

    private static Picker CreatePicker(string title)
    {
        return new Picker
        {
            Title = title,
            TextColor = Color.FromArgb("#222"),
            TitleColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.TextColor = Color.FromArgb("#2E7D32");
    }

    private void SetError(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.TextColor = Color.FromArgb("#C62828");
    }

    private enum CropAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    private enum CropMode
    {
        Crop,
        Scale
    }
}
