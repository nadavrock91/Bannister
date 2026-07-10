using Bannister.Services;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class ChatGptImageGenerationPage : ContentPage
{
    private const decimal CostPerImageUsd = 0.011m;
    private const string CostPerImageNote = "as of June 2026";
    private const decimal VisionRenameEstimatedCostUsd = 0.001m;
    private const string RenameLastFolderKey = "chatgpt_image_rename_last_folder";
    private const string RenameRetryCapKey = "chatgpt_rename_retry_cap";
    private const int DefaultRetryCap = 5;
    private const int MinRetryCap = 1;
    private const int MaxRetryCap = 20;
    private const double AssumedSuccessRatePerPass = 0.6;

    private enum QueueStatus
    {
        Queued,
        Generating,
        Done,
        Failed,
        Cancelled
    }

    private sealed class QueuedPrompt
    {
        public int Index { get; init; }
        public string Text { get; init; } = "";
        public QueueStatus Status { get; set; } = QueueStatus.Queued;
        public string? ErrorMessage { get; set; }
        public byte[]? ImageBytes { get; set; }
        public string? SavedPath { get; set; }
    }

    private readonly OpenAIKeyService _keyService;
    private readonly OpenAIImageService _imageService;
    private readonly OpenAIVisionService _visionService;
    private readonly OwnerModeService _ownerMode;
    private readonly View _normalContent;
    private readonly Editor _promptEditor;
    private readonly Label _keyStatusLabel;
    private readonly Label _keySourceLabel;
    private readonly Button _configureKeyButton;
    private readonly Button _clearKeyButton;
    private readonly Button _generateButton;
    private readonly ActivityIndicator _generationIndicator;
    private readonly Label _generationStatusLabel;
    private readonly Image _generatedImage;
    private readonly Editor _batchPasteEditor;
    private readonly Button _parseBatchButton;
    private readonly Frame _batchPasteFrame;
    private readonly Frame _queueFrame;
    private readonly Label _queueHeaderLabel;
    private readonly Label _queueCostLabel;
    private readonly Button _viewPricingButton;
    private readonly VerticalStackLayout _queueItemsStack;
    private readonly Button _startQueueButton;
    private readonly Button _cancelQueueButton;
    private readonly Button _clearQueueButton;
    private readonly VerticalStackLayout _batchGalleryStack;
    private string? _renameFolderPath;
    private bool _renameRunning;
    private CancellationTokenSource? _renameCts;
    private int _renameRetryCap = DefaultRetryCap;
    private bool _updatingRenameRetryCapEntry;
    private readonly Label _renameFolderLabel;
    private readonly Label _renameCountLabel;
    private readonly Label _renameProgressLabel;
    private readonly Entry _renameRetryCapEntry;
    private readonly Button _renamePickFolderButton;
    private readonly Button _renameRunButton;
    private readonly Button _renameCancelButton;
    private readonly List<QueuedPrompt> _queue = new();
    private bool _isGenerating;
    private bool _isQueueRunning;
    private bool _cancelQueueRequested;

    public ChatGptImageGenerationPage(
        OpenAIKeyService keyService,
        OpenAIImageService imageService,
        OwnerModeService ownerMode,
        OpenAIVisionService? visionService = null)
    {
        _keyService = keyService;
        _imageService = imageService;
        _visionService = visionService ?? new OpenAIVisionService(keyService);
        _ownerMode = ownerMode;
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

        _batchPasteEditor = new Editor
        {
            Placeholder = "Paste a C# string array, for example:\nvar prompts = new[]\n{\n    \"painterly storybook scene of a wooden door\",\n    \"an old man holding a glowing pocket watch\"\n};",
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 200,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            FontSize = 14
        };

        _parseBatchButton = new Button
        {
            Text = "Parse Prompts",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _parseBatchButton.Clicked += async (_, _) => await ParseBatchPromptsAsync();

        _batchPasteFrame = CreateBatchPasteSection();
        _queueHeaderLabel = new Label
        {
            Text = "0 prompts queued",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        };
        _queueCostLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666666")
        };
        _viewPricingButton = new Button
        {
            Text = "View current OpenAI pricing",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Start
        };
        _viewPricingButton.Clicked += async (_, _) => await Launcher.OpenAsync("https://openai.com/api/pricing/");
        _queueItemsStack = new VerticalStackLayout { Spacing = 8 };
        _startQueueButton = new Button
        {
            Text = "Start Queue",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44
        };
        _startQueueButton.Clicked += async (_, _) => await RunQueueAsync();
        _cancelQueueButton = new Button
        {
            Text = "Cancel Queue",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 44,
            IsVisible = false
        };
        _cancelQueueButton.Clicked += (_, _) => _cancelQueueRequested = true;
        _clearQueueButton = new Button
        {
            Text = "Clear Queue",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 36
        };
        _clearQueueButton.Clicked += async (_, _) => await ClearQueueAsync();
        _queueFrame = CreateQueueSection();
        _batchGalleryStack = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false
        };
        _renameFolderLabel = new Label
        {
            Text = "Folder: (not set)",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        _renameCountLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        _renameProgressLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#1565C0"),
            IsVisible = false
        };
        _renameRetryCapEntry = new Entry
        {
            Text = _renameRetryCap.ToString(),
            Keyboard = Keyboard.Numeric,
            WidthRequest = 60,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HeightRequest = 36
        };
        _renameRetryCapEntry.TextChanged += OnRetryCapChanged;
        _renamePickFolderButton = new Button
        {
            Text = "Pick folder",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 40
        };
        _renamePickFolderButton.Clicked += OnPickRenameFolderClicked;
        _renameRunButton = new Button
        {
            Text = "▶ Run",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            IsEnabled = false,
            FontAttributes = FontAttributes.Bold
        };
        _renameRunButton.Clicked += OnRunRenameClicked;
        _renameCancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#B71C1C"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 36,
            IsVisible = false
        };
        _renameCancelButton.Clicked += (_, _) => _renameCts?.Cancel();

        _normalContent = new ScrollView
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
                    _generatedImage,
                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#DDDDDD"),
                        Margin = new Thickness(0, 14, 0, 2)
                    },
                    new Label
                    {
                        Text = "Batch from Pasted Prompts",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    _batchPasteFrame,
                    _queueFrame,
                    _batchGalleryStack,
                    CreateBulkRenameSection()
                }
            }
        };
        Content = _normalContent;
        RefreshQueueDisplay();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await _ownerMode.IsUnlockedAsync())
        {
            Content = CreateLockedContent();
            return;
        }

        Content = _normalContent;
        await RefreshKeyStatusAsync();
        await LoadRetryCapAsync();
        await LoadRenameLastFolderAsync();
    }

    private View CreateLockedContent()
    {
        var backButton = new Button
        {
            Text = "Back",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8,
            WidthRequest = 140,
            HorizontalOptions = LayoutOptions.Center
        };
        backButton.Clicked += async (_, _) => await Navigation.PopAsync();

        return new Grid
        {
            Padding = 24,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Owner Mode Locked",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#222"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "ChatGPT Image Generation is available only when Owner Mode is unlocked.",
                            FontSize = 14,
                            TextColor = Color.FromArgb("#666"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        backButton
                    }
                }
            }
        };
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

    private Frame CreateBatchPasteSection()
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
                Spacing = 10,
                Children =
                {
                    _batchPasteEditor,
                    _parseBatchButton
                }
            }
        };
    }

    private Frame CreateQueueSection()
    {
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };
        headerGrid.Add(_queueHeaderLabel, 0, 0);
        headerGrid.Add(_clearQueueButton, 1, 0);

        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        buttonGrid.Add(_startQueueButton, 0, 0);
        buttonGrid.Add(_cancelQueueButton, 1, 0);

        return new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            IsVisible = false,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    headerGrid,
                    _queueCostLabel,
                    _viewPricingButton,
                    _queueItemsStack,
                    buttonGrid
                }
            }
        };
    }

    private Frame CreateBulkRenameSection()
    {
        var folderRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };
        folderRow.Add(_renameFolderLabel, 0, 0);
        folderRow.Add(_renamePickFolderButton, 1, 0);

        var retryCapLabel = new Label
        {
            Text = "Max retry passes:",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };

        var retryCapRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { retryCapLabel, _renameRetryCapEntry }
        };

        return new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Bulk Image Rename via Vision",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = "Pick a folder. The vision API names each top-level image, renames successful files, and moves them into a 'renamed' subfolder. Unnamed files remain in the source folder for retries.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    },
                    folderRow,
                    retryCapRow,
                    _renameCountLabel,
                    _renameRunButton,
                    _renameProgressLabel,
                    _renameCancelButton
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
        _parseBatchButton.IsEnabled = !isGenerating && !_isQueueRunning;
        _generationIndicator.IsVisible = isGenerating;
        _generationIndicator.IsRunning = isGenerating;
        if (!string.IsNullOrWhiteSpace(status))
            _generationStatusLabel.Text = status;
    }

    private async Task ParseBatchPromptsAsync()
    {
        if (_isGenerating || _isQueueRunning)
            return;

        var prompts = ParsePromptsFromCSharp(_batchPasteEditor.Text ?? "");
        if (prompts.Count == 0)
        {
            await DisplayAlert(
                "No prompts found",
                "Couldn't find any prompts in the pasted text. Make sure you pasted a C# string array.",
                "OK");
            return;
        }

        _queue.Clear();
        for (int i = 0; i < prompts.Count; i++)
        {
            _queue.Add(new QueuedPrompt
            {
                Index = i + 1,
                Text = prompts[i]
            });
        }

        _batchGalleryStack.Children.Clear();
        _batchGalleryStack.IsVisible = false;
        RefreshQueueDisplay();
    }

    private async Task RunQueueAsync()
    {
        if (_isQueueRunning || _queue.Count == 0)
            return;

        if (!await _keyService.IsKeyConfiguredAsync())
        {
            await DisplayAlert("API key required", "Configure API key first.", "OK");
            return;
        }

        var queuedCount = _queue.Count(q => q.Status == QueueStatus.Queued);
        var estimatedCost = queuedCount * CostPerImageUsd;
        var confirm = await DisplayAlert(
            "Confirm batch generation",
            $"Generate {queuedCount} image(s) for an estimated ${estimatedCost:0.000} USD?\n\n" +
            $"(Estimated at ${CostPerImageUsd:0.000} per image, {CostPerImageNote}. Actual cost may vary slightly.)",
            "Generate",
            "Cancel");

        if (!confirm)
            return;

        _isQueueRunning = true;
        _cancelQueueRequested = false;
        SetGeneratingState(true, "Generating batch...");
        RefreshQueueDisplay();

        try
        {
            var batchTimestamp = DateTime.Now;
            foreach (var item in _queue.Where(q => q.Status == QueueStatus.Queued).ToList())
            {
                if (_cancelQueueRequested)
                {
                    MarkRemainingQueuedAsCancelled();
                    break;
                }

                item.Status = QueueStatus.Generating;
                item.ErrorMessage = null;
                RefreshQueueDisplay();

                var result = await _imageService.GenerateImageAsync(item.Text);
                if (result.Success && result.ImageBytes != null)
                {
                    var filename = $"bannister_dalle_{batchTimestamp:yyyy-MM-dd}_{batchTimestamp:HH-mm-ss}_{item.Index:000}.png";
                    var saveResult = await SaveImageToDownloadsAsync(result.ImageBytes, filename);
                    item.ImageBytes = result.ImageBytes;
                    item.SavedPath = saveResult.Path;
                    item.Status = QueueStatus.Done;
                    AppendGalleryImage(item);
                }
                else
                {
                    item.Status = QueueStatus.Failed;
                    item.ErrorMessage = result.ErrorMessage ?? "Image generation failed.";
                }

                RefreshQueueDisplay();
            }

            if (_cancelQueueRequested)
                MarkRemainingQueuedAsCancelled();

            var done = _queue.Count(q => q.Status == QueueStatus.Done);
            await DisplayAlert("Batch complete", $"{done} of {_queue.Count} images generated successfully.", "OK");
        }
        finally
        {
            _isQueueRunning = false;
            _cancelQueueRequested = false;
            SetGeneratingState(false);
            RefreshQueueDisplay();
        }
    }

    private void MarkRemainingQueuedAsCancelled()
    {
        foreach (var item in _queue.Where(q => q.Status == QueueStatus.Queued))
            item.Status = QueueStatus.Cancelled;
    }

    private async Task ClearQueueAsync()
    {
        if (_isQueueRunning)
            return;

        var confirm = await DisplayAlert(
            "Clear queue?",
            "Clear current queue and generated images?",
            "Clear",
            "Cancel");

        if (!confirm)
            return;

        _queue.Clear();
        _batchPasteEditor.Text = "";
        _batchGalleryStack.Children.Clear();
        _batchGalleryStack.IsVisible = false;
        RefreshQueueDisplay();
    }

    private async Task LoadRenameLastFolderAsync()
    {
        if (!string.IsNullOrWhiteSpace(_renameFolderPath))
            return;

        try
        {
            var last = await SecureStorage.GetAsync(RenameLastFolderKey);
            if (!string.IsNullOrWhiteSpace(last) && Directory.Exists(last))
                SetRenameFolder(last);
        }
        catch
        {
        }
    }

    private async Task LoadRetryCapAsync()
    {
        try
        {
            var raw = await SecureStorage.GetAsync(RenameRetryCapKey);
            if (int.TryParse(raw, out var val))
            {
                val = Math.Clamp(val, MinRetryCap, MaxRetryCap);
                _renameRetryCap = val;
                _updatingRenameRetryCapEntry = true;
                _renameRetryCapEntry.Text = val.ToString();
                _updatingRenameRetryCapEntry = false;
                UpdateCostEstimate();
            }
        }
        catch
        {
        }
        finally
        {
            _updatingRenameRetryCapEntry = false;
        }
    }

    private async void OnRetryCapChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingRenameRetryCapEntry)
            return;

        if (string.IsNullOrWhiteSpace(_renameRetryCapEntry.Text))
            return;

        if (!int.TryParse(_renameRetryCapEntry.Text, out var val))
        {
            UpdateCostEstimate();
            return;
        }

        val = Math.Clamp(val, MinRetryCap, MaxRetryCap);
        _renameRetryCap = val;

        if (_renameRetryCapEntry.Text != val.ToString())
        {
            _updatingRenameRetryCapEntry = true;
            _renameRetryCapEntry.Text = val.ToString();
            _updatingRenameRetryCapEntry = false;
        }

        try
        {
            await SecureStorage.SetAsync(RenameRetryCapKey, val.ToString());
        }
        catch
        {
        }

        UpdateCostEstimate();
    }

    private async void OnPickRenameFolderClicked(object? sender, EventArgs e)
    {
        if (_renameRunning)
            return;

        string? last = null;
        try
        {
            last = await SecureStorage.GetAsync(RenameLastFolderKey);
        }
        catch
        {
        }

        var picked = await DisplayPromptAsync(
            "Pick folder",
            "Paste folder path:",
            initialValue: last ?? _renameFolderPath ?? "");

        if (string.IsNullOrWhiteSpace(picked))
            return;

        picked = picked.Trim().Trim('"');
        if (!Directory.Exists(picked))
        {
            await DisplayAlert("Folder not found", "That folder does not exist.", "OK");
            return;
        }

        SetRenameFolder(picked);

        try
        {
            await SecureStorage.SetAsync(RenameLastFolderKey, picked);
        }
        catch
        {
        }
    }

    private void SetRenameFolder(string folderPath)
    {
        _renameFolderPath = folderPath;
        var count = CountRenameableImages(folderPath);
        _renameFolderLabel.Text = $"Folder: {folderPath}";
        UpdateCostEstimate();
        _renameRunButton.IsEnabled = count > 0 && !_renameRunning && Directory.Exists(folderPath);
    }

    private void UpdateCostEstimate()
    {
        if (string.IsNullOrWhiteSpace(_renameFolderPath) || !Directory.Exists(_renameFolderPath))
        {
            _renameCountLabel.Text = "";
            return;
        }

        var count = GetRenameableImageFiles(_renameFolderPath).Count;
        if (count == 0)
        {
            _renameCountLabel.Text = "No images found at top level.";
            return;
        }

        var cap = Math.Clamp(_renameRetryCap, MinRetryCap, MaxRetryCap);
        var bestCase = count * VisionRenameEstimatedCostUsd;
        var maxCase = count * cap * VisionRenameEstimatedCostUsd;
        var q = 1.0 - AssumedSuccessRatePerPass;
        var expectedFactor = (1.0 - Math.Pow(q, cap)) / AssumedSuccessRatePerPass;
        var expectedCase = (decimal)(count * expectedFactor) * VisionRenameEstimatedCostUsd;

        _renameCountLabel.Text =
            $"Found {count} image{(count == 1 ? "" : "s")}. " +
            $"Est. cost: ${expectedCase:0.000} " +
            $"(best ${bestCase:0.000}, max ${maxCase:0.000} at {cap} passes)";
    }

    private async void OnRunRenameClicked(object? sender, EventArgs e)
    {
        if (_renameRunning)
            return;

        if (string.IsNullOrWhiteSpace(_renameFolderPath) || !Directory.Exists(_renameFolderPath))
            return;

        if (!await _keyService.IsKeyConfiguredAsync())
        {
            await DisplayAlert("API key required", "Configure API key first.", "OK");
            return;
        }

        var initialFiles = GetRenameableImageFiles(_renameFolderPath);
        if (initialFiles.Count == 0)
        {
            await DisplayAlert("No images", "No images found at the top level of that folder.", "OK");
            return;
        }

        var cap = Math.Clamp(_renameRetryCap, MinRetryCap, MaxRetryCap);
        var q = 1.0 - AssumedSuccessRatePerPass;
        var expectedFactor = (1.0 - Math.Pow(q, cap)) / AssumedSuccessRatePerPass;
        var expectedCost = (decimal)(initialFiles.Count * expectedFactor) * VisionRenameEstimatedCostUsd;
        var maxCost = initialFiles.Count * cap * VisionRenameEstimatedCostUsd;

        var confirm = await DisplayAlert(
            "Run bulk rename",
            $"Rename {initialFiles.Count} image(s) automatically.\n\n" +
            $"Est. cost: ${expectedCost:0.000} (60% success/pass, {cap} max passes).\n" +
            $"Max cost: ${maxCost:0.000}.\n\n" +
            "Successfully renamed files will move to a 'renamed' subfolder. Unnamed files remain in the source folder.",
            "Run",
            "Cancel");

        if (!confirm)
            return;

        _renameRunning = true;
        _renamePickFolderButton.IsEnabled = false;
        _renameRunButton.IsEnabled = false;
        _renameProgressLabel.IsVisible = true;
        _renameCancelButton.IsVisible = true;
        _renameCts = new CancellationTokenSource();
        var ct = _renameCts.Token;

        var renamedDir = Path.Combine(_renameFolderPath, "renamed");
        try
        {
            Directory.CreateDirectory(renamedDir);
        }
        catch (Exception ex)
        {
            _renameCts.Dispose();
            _renameCts = null;
            _renameRunning = false;
            _renamePickFolderButton.IsEnabled = true;
            _renameRunButton.IsEnabled = Directory.Exists(_renameFolderPath);
            _renameProgressLabel.IsVisible = false;
            _renameCancelButton.IsVisible = false;
            await DisplayAlert("Cannot create output folder", ex.Message, "OK");
            return;
        }

        var totalApiCalls = 0;
        var totalRenamed = 0;
        var passNumber = 0;
        var cancelled = false;
        var failureReasons = new List<(string fileName, int passNumber, string reason, string rawSnippet)>();

        try
        {
            while (passNumber < cap)
            {
                if (ct.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                passNumber++;

                var pending = GetRenameableImageFiles(_renameFolderPath);
                if (pending.Count == 0)
                    break;

                for (var i = 0; i < pending.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    var path = pending[i];
                    _renameProgressLabel.Text =
                        $"Pass {passNumber}/{cap} - {i + 1}/{pending.Count} " +
                        $"({totalRenamed}/{initialFiles.Count} renamed)";
                    await Task.Yield();

                    VisionNameResult result;
                    try
                    {
                        result = await _visionService.SuggestImageNameAsync(path);
                    }
                    catch (Exception ex)
                    {
                        result = VisionNameResult.Fail($"API error: {ex.Message}");
                    }

                    totalApiCalls++;

                    if (!result.Success || string.IsNullOrWhiteSpace(result.SuggestedName))
                    {
                        failureReasons.Add((
                            Path.GetFileName(path),
                            passNumber,
                            result.ErrorMessage ?? "Unknown failure",
                            BuildRawSnippet(result.RawResponse)));
                        continue;
                    }

                    try
                    {
                        var sanitized = SanitizeForFilename(result.SuggestedName);
                        if (string.IsNullOrWhiteSpace(sanitized))
                        {
                            failureReasons.Add((
                                Path.GetFileName(path),
                                passNumber,
                                "Suggested name sanitized to empty",
                                BuildRawSnippet(result.RawResponse)));
                            continue;
                        }

                        var ext = Path.GetExtension(path);
                        var targetPath = Path.Combine(renamedDir, sanitized + ext);
                        var collisionCounter = 2;
                        while (File.Exists(targetPath))
                        {
                            if (collisionCounter > 100)
                                throw new InvalidOperationException("Could not find available filename.");

                            targetPath = Path.Combine(renamedDir, $"{sanitized}_{collisionCounter}{ext}");
                            collisionCounter++;
                        }

                        File.Move(path, targetPath);
                        totalRenamed++;
                    }
                    catch (Exception ex)
                    {
                        failureReasons.Add((
                            Path.GetFileName(path),
                            passNumber,
                            $"Disk error: {ex.Message}",
                            "(disk operation)"));
                    }
                }

                if (cancelled)
                    break;

                var stillPendingCount = GetRenameableImageFiles(_renameFolderPath).Count;
                if (stillPendingCount == 0)
                    break;

                _renameProgressLabel.Text = $"Pass {passNumber} done. {stillPendingCount} unnamed. Pausing before next pass...";
                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    break;
                }
            }
        }
        finally
        {
            _renameCts?.Dispose();
            _renameCts = null;
            _renameRunning = false;
            _renamePickFolderButton.IsEnabled = true;
            _renameRunButton.IsEnabled = Directory.Exists(_renameFolderPath);
            _renameProgressLabel.IsVisible = false;
            _renameCancelButton.IsVisible = false;
        }

        var actualCost = totalApiCalls * VisionRenameEstimatedCostUsd;
        var stillUnnamed = GetRenameableImageFiles(_renameFolderPath).Count;
        var status = cancelled ? "Cancelled" : "Complete";
        var summary =
            $"{status} after {passNumber} pass(es).\n\n" +
            $"Renamed: {totalRenamed} -> 'renamed' subfolder\n" +
            $"Still unnamed in source: {stillUnnamed}\n" +
            $"API calls: {totalApiCalls} (~${actualCost:0.000})";

        await DisplayAlert("Bulk rename finished", summary, "OK");

        if (failureReasons.Count > 0)
            await DisplayAlert("Failure details", BuildFailureDetails(failureReasons), "OK");

        UpdateCostEstimate();
    }

    private static string BuildRawSnippet(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return "(no response)";

        var cleaned = rawResponse.Replace("\r", " ").Replace("\n", " ").Trim();
        return cleaned.Length > 200 ? cleaned.Substring(0, 200) + "..." : cleaned;
    }

    private static string BuildFailureDetails(List<(string fileName, int passNumber, string reason, string rawSnippet)> failureReasons)
    {
        var grouped = failureReasons
            .GroupBy(f => f.fileName)
            .Select(g => new
            {
                FileName = g.Key,
                AttemptCount = g.Count(),
                Reasons = g.Select(f => (f.reason, f.rawSnippet)).ToList()
            })
            .OrderByDescending(g => g.AttemptCount)
            .ThenBy(g => g.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{grouped.Count} file(s) failed at least once.");
        sb.AppendLine();

        foreach (var g in grouped.Take(15))
        {
            sb.AppendLine($"- {g.FileName} ({g.AttemptCount} failed attempt(s))");
            var uniqueReasons = g.Reasons
                .GroupBy(r => r.reason)
                .Select(rg => new { Reason = rg.Key, Count = rg.Count(), Sample = rg.First().rawSnippet })
                .OrderByDescending(x => x.Count)
                .Take(3);

            foreach (var ur in uniqueReasons)
            {
                sb.AppendLine($"    - {ur.Reason} (x{ur.Count})");
                if (!string.IsNullOrWhiteSpace(ur.Sample) &&
                    ur.Sample != "(no response)" &&
                    ur.Sample != "(disk operation)")
                {
                    sb.AppendLine($"      \"{ur.Sample}\"");
                }
            }

            sb.AppendLine();
        }

        if (grouped.Count > 15)
            sb.AppendLine($"...and {grouped.Count - 15} more files with failures.");

        return sb.ToString();
    }

    private void RefreshQueueDisplay()
    {
        if (_queueFrame == null || _batchPasteFrame == null || _queueItemsStack == null)
            return;

        var hasQueue = _queue.Count > 0;
        _batchPasteFrame.IsVisible = !hasQueue;
        _queueFrame.IsVisible = hasQueue;
        _queueHeaderLabel.Text = $"{_queue.Count} prompt{(_queue.Count == 1 ? "" : "s")} queued";
        var queuedCount = _queue.Count(q => q.Status == QueueStatus.Queued);
        var totalCost = queuedCount * CostPerImageUsd;
        _queueCostLabel.Text = $"Estimated cost: ${totalCost:0.000} USD for {queuedCount} queued (at ${CostPerImageUsd:0.000} per image, {CostPerImageNote})";
        _queueCostLabel.IsVisible = queuedCount > 0;
        _viewPricingButton.IsVisible = queuedCount > 0;
        _startQueueButton.IsEnabled = hasQueue && !_isQueueRunning && queuedCount > 0;
        _cancelQueueButton.IsVisible = _isQueueRunning;
        _clearQueueButton.IsEnabled = !_isQueueRunning;
        _parseBatchButton.IsEnabled = !_isGenerating && !_isQueueRunning;

        _queueItemsStack.Children.Clear();
        foreach (var item in _queue)
            _queueItemsStack.Children.Add(CreateQueueItemView(item));
    }

    private View CreateQueueItemView(QueuedPrompt item)
    {
        var statusLabel = new Label
        {
            Text = GetStatusText(item.Status),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetStatusColor(item.Status),
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = 92
        };

        var promptLabel = new Label
        {
            Text = TruncatePrompt(item.Text),
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };

        var indexLabel = new Label
        {
            Text = $"{item.Index} / {_queue.Count}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.End,
            WidthRequest = 54
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 6)
        };
        grid.Add(statusLabel, 0, 0);
        grid.Add(promptLabel, 1, 0);
        grid.Add(indexLabel, 2, 0);

        if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await DisplayAlert("Generation failed", item.ErrorMessage, "OK");
            grid.GestureRecognizers.Add(tap);
        }

        return grid;
    }

    private void AppendGalleryImage(QueuedPrompt item)
    {
        if (item.ImageBytes == null)
            return;

        _batchGalleryStack.IsVisible = true;
        _batchGalleryStack.Children.Add(new Label
        {
            Text = $"{item.Index:000}: {TruncatePrompt(item.Text)}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        _batchGalleryStack.Children.Add(new Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(item.ImageBytes)),
            HeightRequest = 200,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.White
        });
        if (!string.IsNullOrWhiteSpace(item.SavedPath))
        {
            _batchGalleryStack.Children.Add(new Label
            {
                Text = $"Saved to: {item.SavedPath}",
                FontSize = 11,
                TextColor = Color.FromArgb("#666"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
        }
    }

    private static string GetStatusText(QueueStatus status) => status switch
    {
        QueueStatus.Queued => "Queued",
        QueueStatus.Generating => "Generating",
        QueueStatus.Done => "Done",
        QueueStatus.Failed => "Failed",
        QueueStatus.Cancelled => "Cancelled",
        _ => "Queued"
    };

    private static Color GetStatusColor(QueueStatus status) => status switch
    {
        QueueStatus.Queued => Color.FromArgb("#666666"),
        QueueStatus.Generating => Color.FromArgb("#1565C0"),
        QueueStatus.Done => Color.FromArgb("#2E7D32"),
        QueueStatus.Failed => Color.FromArgb("#C62828"),
        QueueStatus.Cancelled => Color.FromArgb("#666666"),
        _ => Color.FromArgb("#666666")
    };

    private static string TruncatePrompt(string prompt)
    {
        var trimmed = prompt.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..77] + "...";
    }

    private static List<string> ParsePromptsFromCSharp(string pastedText)
    {
        if (string.IsNullOrWhiteSpace(pastedText))
            return new List<string>();

        var block = ExtractFirstBraceBlock(pastedText);
        if (string.IsNullOrWhiteSpace(block))
            return new List<string>();

        return Regex.Matches(block, "\"((?:\\\\.|[^\"\\\\])*)\"")
            .Select(match => DecodeCString(match.Groups[1].Value).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string? ExtractFirstBraceBlock(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return null;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch != '}')
                continue;

            depth--;
            if (depth == 0)
                return text.Substring(start, i - start + 1);
        }

        return null;
    }

    private static string DecodeCString(string value)
    {
        var output = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i == value.Length - 1)
            {
                output.Append(value[i]);
                continue;
            }

            var escaped = value[++i];
            output.Append(escaped switch
            {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => escaped
            });
        }

        return output.ToString();
    }

    private static int CountRenameableImages(string folder)
    {
        try
        {
            return GetRenameableImageFiles(folder).Count;
        }
        catch
        {
            return 0;
        }
    }

    private static List<string> GetRenameableImageFiles(string folder)
    {
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(p => exts.Contains(Path.GetExtension(p)))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string SanitizeForFilename(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var s = raw.Trim().ToLowerInvariant();
        s = s.Trim('"', '\'', '`');
        s = Regex.Replace(s, @"\s+", "_");
        s = Regex.Replace(s, @"[^a-z0-9_\-]", "");
        s = Regex.Replace(s, @"_+", "_");
        s = s.Trim('_', '-');
        if (s.Length > 80)
            s = s.Substring(0, 80).TrimEnd('_', '-');

        return s;
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

