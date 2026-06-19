using Bannister.Services;

namespace Bannister.Views;

public class ChatGptImageGenerationPage : ContentPage
{
    private readonly OpenAIKeyService _keyService;
    private readonly OpenAIImageService _imageService;
    private readonly Editor _promptEditor;
    private readonly Label _keyStatusLabel;
    private readonly Label _keySourceLabel;
    private readonly Button _configureKeyButton;
    private readonly Button _clearKeyButton;
    private readonly Button _generateButton;
    private readonly ActivityIndicator _generationIndicator;
    private readonly Label _generationStatusLabel;
    private readonly Image _generatedImage;
    private bool _isGenerating;

    public ChatGptImageGenerationPage(OpenAIKeyService keyService, OpenAIImageService imageService)
    {
        _keyService = keyService;
        _imageService = imageService;
        Title = "ChatGPT Image Generation";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _keyStatusLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828")
        };

        _keySourceLabel = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.TailTruncation
        };

        _configureKeyButton = new Button
        {
            Text = "Configure Key File",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _configureKeyButton.Clicked += async (_, _) => await ConfigureKeyFileAsync();

        _clearKeyButton = new Button
        {
            Text = "Clear Stored Key",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 42,
            IsVisible = false
        };
        _clearKeyButton.Clicked += async (_, _) => await ClearStoredKeyAsync();

        _promptEditor = new Editor
        {
            Placeholder = "Describe the image you want to generate...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 160,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            FontSize = 15
        };

        _generateButton = new Button
        {
            Text = "Generate Image",
            BackgroundColor = Color.FromArgb("#C2185B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        _generateButton.Clicked += async (_, _) => await GenerateImageAsync();

        _generationIndicator = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            Color = Color.FromArgb("#C2185B"),
            HorizontalOptions = LayoutOptions.Center
        };

        _generationStatusLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        _generatedImage = new Image
        {
            IsVisible = false,
            HeightRequest = 400,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.White
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "ChatGPT Image Generation",
                        FontSize = 26,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = "Generate images using OpenAI's image API.",
                        FontSize = 15,
                        TextColor = Color.FromArgb("#666"),
                        Margin = new Thickness(0, -6, 0, 10)
                    },
                    CreateKeySettingsSection(),
                    new Label
                    {
                        Text = "Prompt",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    _promptEditor,
                    _generateButton,
                    _generationIndicator,
                    _generationStatusLabel,
                    _generatedImage
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshKeyStatusAsync();
    }

    private Frame CreateKeySettingsSection()
    {
        return new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    _keyStatusLabel,
                    _keySourceLabel,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            _configureKeyButton,
                            _clearKeyButton
                        }
                    }
                }
            }
        };
    }

    private async Task RefreshKeyStatusAsync()
    {
        var configured = await _keyService.IsKeyConfiguredAsync();
        var sourcePath = await _keyService.GetSourcePathAsync();

        if (configured)
        {
            _keyStatusLabel.Text = "✓ API key configured";
            _keyStatusLabel.TextColor = Color.FromArgb("#2E7D32");
            _configureKeyButton.Text = "Re-select Key File";
            _clearKeyButton.IsVisible = true;
        }
        else if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            _keyStatusLabel.Text = "⚠ Key file moved or missing — re-select";
            _keyStatusLabel.TextColor = Color.FromArgb("#F57C00");
            _configureKeyButton.Text = "Configure Key File";
            _clearKeyButton.IsVisible = false;
        }
        else
        {
            _keyStatusLabel.Text = "⚠ API key not configured";
            _keyStatusLabel.TextColor = Color.FromArgb("#C62828");
            _configureKeyButton.Text = "Configure Key File";
            _clearKeyButton.IsVisible = false;
        }

        _keySourceLabel.Text = string.IsNullOrWhiteSpace(sourcePath)
            ? "No key file selected."
            : $"Source: {sourcePath}";
    }

    private async Task ConfigureKeyFileAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select OpenAI API key file"
            });

            if (result == null)
                return;

            var filePath = await EnsureReadableFilePathAsync(result);
            var sourcePath = !string.IsNullOrWhiteSpace(result.FullPath)
                ? result.FullPath
                : result.FileName;

            if (await _keyService.ConfigureFromFileAsync(filePath, sourcePath))
            {
                await RefreshKeyStatusAsync();
                await DisplayAlert("Key configured", "Key configured successfully.", "OK");
            }
            else
            {
                await DisplayAlert("Key not saved", _keyService.LastError ?? "The selected key file could not be used.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("File picker error", ex.Message, "OK");
        }
    }

    private static async Task<string> EnsureReadableFilePathAsync(FileResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.FullPath) && File.Exists(result.FullPath))
            return result.FullPath;

        var tempPath = Path.Combine(FileSystem.AppDataDirectory, "openai_key_selection.tmp");
        using var source = await result.OpenReadAsync();
        await using var destination = File.Create(tempPath);
        await source.CopyToAsync(destination);
        return tempPath;
    }

    private async Task ClearStoredKeyAsync()
    {
        var confirm = await DisplayAlert(
            "Clear stored key?",
            "You'll need to re-select the key file to use image generation.",
            "Clear",
            "Cancel");

        if (!confirm)
            return;

        await _keyService.ClearAsync();
        await RefreshKeyStatusAsync();
    }

    private async Task GenerateImageAsync()
    {
        if (_isGenerating)
            return;

        if (!await _keyService.IsKeyConfiguredAsync())
        {
            await DisplayAlert("API key required", "Configure API key first.", "OK");
            return;
        }

        var prompt = _promptEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            await DisplayAlert("Prompt required", "Enter a prompt first.", "OK");
            return;
        }

        try
        {
            SetGeneratingState(true, "Generating image...");

            var result = await _imageService.GenerateImageAsync(prompt);
            if (!result.Success || result.ImageBytes == null)
            {
                await DisplayAlert("Image generation failed", result.ErrorMessage ?? "Image generation failed.", "OK");
                return;
            }

            var filename = $"bannister_dalle_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HH-mm-ss}.png";
            var saveResult = await SaveImageToDownloadsAsync(result.ImageBytes, filename);

            _generatedImage.Source = ImageSource.FromStream(() => new MemoryStream(result.ImageBytes));
            _generatedImage.IsVisible = true;
            _generationStatusLabel.Text = saveResult.SavedToDownloads
                ? $"Saved to: {saveResult.Path}"
                : $"Saved to app storage (couldn't access Downloads): {saveResult.Path}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Image generation failed", $"Image generation failed: {ex.Message}", "OK");
        }
        finally
        {
            SetGeneratingState(false);
        }
    }

    private void SetGeneratingState(bool isGenerating, string status = "")
    {
        _isGenerating = isGenerating;
        _generateButton.IsEnabled = !isGenerating;
        _configureKeyButton.IsEnabled = !isGenerating;
        _clearKeyButton.IsEnabled = !isGenerating;
        _generationIndicator.IsVisible = isGenerating;
        _generationIndicator.IsRunning = isGenerating;
        if (!string.IsNullOrWhiteSpace(status))
            _generationStatusLabel.Text = status;
    }

    private static async Task<(string Path, bool SavedToDownloads)> SaveImageToDownloadsAsync(byte[] imageBytes, string filename)
    {
        try
        {
            var downloadsPath = GetDownloadsFolderPath();
            Directory.CreateDirectory(downloadsPath);
            var outputPath = Path.Combine(downloadsPath, filename);
            await File.WriteAllBytesAsync(outputPath, imageBytes);
            return (outputPath, true);
        }
        catch
        {
            var fallbackPath = Path.Combine(FileSystem.AppDataDirectory, filename);
            await File.WriteAllBytesAsync(fallbackPath, imageBytes);
            return (fallbackPath, false);
        }
    }

    private static string GetDownloadsFolderPath()
    {
#if ANDROID
        var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
        return downloads?.AbsolutePath ?? FileSystem.AppDataDirectory;
#else
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? FileSystem.AppDataDirectory
            : Path.Combine(userProfile, "Downloads");
#endif
    }
}
