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

    private sealed class RenamePreviewItem
    {
        public string OriginalPath { get; init; } = "";
        public string OriginalName { get; init; } = "";
        public string SuggestedName { get; set; } = "";
        public string Extension { get; init; } = "";
        public bool Selected { get; set; } = true;
        public string? Error { get; set; }
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
    private List<RenamePreviewItem>? _renamePreview;
    private bool _renameDryRunRunning;
    private bool _renameExecuteRunning;
    private CancellationTokenSource? _dryRunCts;
    private int _renameRetryCap = DefaultRetryCap;
    private bool _updatingRenameRetryCapEntry;
    private readonly Label _renameFolderLabel;
    private readonly Label _renameCountLabel;
    private readonly Label _renameProgressLabel;
    private readonly Entry _renameRetryCapEntry;
    private readonly Button _renamePickFolderButton;
    private readonly Button _renameDryRunButton;
    private readonly Button _renameExecuteButton;
    private readonly Button _renameCancelButton;
    private readonly VerticalStackLayout _renamePreviewStack;
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
        _renameDryRunButton = new Button
        {
            Text = "Run Dry-Run",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            IsEnabled = false
        };
        _renameDryRunButton.Clicked += OnRunDryRunClicked;
        _renameExecuteButton = new Button
        {
            Text = "Execute Renames",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            IsEnabled = false
        };
        _renameExecuteButton.Clicked += OnExecuteRenamesClicked;
        _renameCancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#B71C1C"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 36,
            IsVisible = false
        };
        _renameCancelButton.Clicked += (_, _) => _dryRunCts?.Cancel();
        _renamePreviewStack = new VerticalStackLayout
        {
            Spacing = 6
        };

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

        var actionsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        actionsGrid.Add(_renameDryRunButton, 0, 0);
        actionsGrid.Add(_renameExecuteButton, 1, 0);

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
                        Text = "Pick a folder. The vision API suggests a descriptive filename for each image at the top level. Review the preview, then execute the renames on disk.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    },
                    folderRow,
                    retryCapRow,
                    _renameCountLabel,
                    _renameProgressLabel,
                    _renameCancelButton,
                    actionsGrid,
                    new ScrollView
                    {
                        HeightRequest = 280,
                        Content = _renamePreviewStack
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
        if (_renameDryRunRunning || _renameExecuteRunning)
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

        _renameDryRunButton.IsEnabled = count > 0 && !_renameDryRunRunning && !_renameExecuteRunning;
        _renamePreview = null;
        _renamePreviewStack.Children.Clear();
        _renameExecuteButton.IsEnabled = false;
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

    private async void OnRunDryRunClicked(object? sender, EventArgs e)
    {
        if (_renameDryRunRunning || _renameExecuteRunning)
            return;

        if (string.IsNullOrWhiteSpace(_renameFolderPath) || !Directory.Exists(_renameFolderPath))
            return;

        if (!await _keyService.IsKeyConfiguredAsync())
        {
            await DisplayAlert("API key required", "Configure API key first.", "OK");
            return;
        }

        var files = GetRenameableImageFiles(_renameFolderPath);
        if (files.Count == 0)
        {
            await DisplayAlert("No images", "No images found at the top level of that folder.", "OK");
            return;
        }

        var cap = Math.Clamp(_renameRetryCap, MinRetryCap, MaxRetryCap);
        var q = 1.0 - AssumedSuccessRatePerPass;
        var expectedFactor = (1.0 - Math.Pow(q, cap)) / AssumedSuccessRatePerPass;
        var expectedCost = (decimal)(files.Count * expectedFactor) * VisionRenameEstimatedCostUsd;
        var maxCost = files.Count * cap * VisionRenameEstimatedCostUsd;

        var confirm = await DisplayAlert(
            "Confirm dry-run",
            $"Send {files.Count} image(s) to OpenAI Vision.\n\n" +
            $"Est. cost: ${expectedCost:0.000} (assumes 60% success per pass, {cap} max passes).\n" +
            $"Max cost if all passes hit cap: ${maxCost:0.000}.\n\n" +
            "Retries continue until all files are named or the cap is reached.",
            "Run",
            "Cancel");

        if (!confirm)
            return;

        _renameDryRunRunning = true;
        _renamePickFolderButton.IsEnabled = false;
        _renameDryRunButton.IsEnabled = false;
        _renameExecuteButton.IsEnabled = false;
        _renameProgressLabel.IsVisible = true;
        _renameCancelButton.IsVisible = true;
        _renamePreview = new List<RenamePreviewItem>();
        _renamePreviewStack.Children.Clear();

        foreach (var file in files)
        {
            var item = new RenamePreviewItem
            {
                OriginalPath = file,
                OriginalName = Path.GetFileName(file),
                Extension = Path.GetExtension(file),
                Selected = false,
                SuggestedName = "",
                Error = null
            };

            _renamePreview.Add(item);
            _renamePreviewStack.Children.Add(BuildPreviewRow(item));
        }

        _dryRunCts = new CancellationTokenSource();
        var ct = _dryRunCts.Token;
        var totalApiCalls = 0;
        var passNumber = 0;
        var cancelled = false;

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

                var pending = _renamePreview
                    .Where(p => string.IsNullOrWhiteSpace(p.SuggestedName))
                    .ToList();

                if (pending.Count == 0)
                    break;

                for (var i = 0; i < pending.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    var item = pending[i];
                    _renameProgressLabel.Text =
                        $"Pass {passNumber}/{cap} - {i + 1}/{pending.Count} " +
                        $"({_renamePreview.Count(p => !string.IsNullOrWhiteSpace(p.SuggestedName))}/{_renamePreview.Count} named)";
                    await Task.Yield();

                    var result = await _visionService.SuggestImageNameAsync(item.OriginalPath);
                    totalApiCalls++;

                    if (result.Success && !string.IsNullOrWhiteSpace(result.SuggestedName))
                    {
                        item.Selected = true;
                        item.SuggestedName = result.SuggestedName!;
                        item.Error = null;
                    }
                    else
                    {
                        item.Selected = false;
                        item.Error = result.ErrorMessage;
                    }

                    RefreshPreviewRow(item);
                }

                if (cancelled)
                    break;

                var stillPending = _renamePreview.Count(p => string.IsNullOrWhiteSpace(p.SuggestedName));
                if (stillPending == 0)
                    break;

                _renameProgressLabel.Text = $"Pass {passNumber} done. {stillPending} unnamed. Pausing before next pass...";
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
            _dryRunCts?.Dispose();
            _dryRunCts = null;
            _renameDryRunRunning = false;
            _renamePickFolderButton.IsEnabled = true;
            _renameDryRunButton.IsEnabled = true;
            _renameProgressLabel.IsVisible = false;
            _renameCancelButton.IsVisible = false;
            _renameExecuteButton.IsEnabled = _renamePreview?.Any(p => p.Selected) == true;
        }

        var namedCount = _renamePreview.Count(p => p.Selected);
        var unnamedCount = _renamePreview.Count - namedCount;
        var actualCost = totalApiCalls * VisionRenameEstimatedCostUsd;
        var summary = cancelled
            ? $"Cancelled after pass {passNumber}.\n\nNamed: {namedCount}\nUnnamed: {unnamedCount}\nAPI calls: {totalApiCalls} (~${actualCost:0.000})"
            : $"Complete after {passNumber} pass(es).\n\nNamed: {namedCount}\nUnnamed: {unnamedCount}\nAPI calls: {totalApiCalls} (~${actualCost:0.000})";

        await DisplayAlert(
            "Dry-run finished",
            summary,
            "OK");
    }

    private void RefreshPreviewRow(RenamePreviewItem item)
    {
        if (_renamePreview == null)
            return;

        var index = _renamePreview.IndexOf(item);
        if (index < 0 || index >= _renamePreviewStack.Children.Count)
            return;

        _renamePreviewStack.Children.RemoveAt(index);
        _renamePreviewStack.Children.Insert(index, BuildPreviewRow(item));
    }

    private View BuildPreviewRow(RenamePreviewItem item)
    {
        var checkbox = new CheckBox
        {
            IsChecked = item.Selected,
            VerticalOptions = LayoutOptions.Center
        };
        checkbox.CheckedChanged += (_, e) =>
        {
            item.Selected = e.Value;
            _renameExecuteButton.IsEnabled = _renamePreview?.Any(p => p.Selected) == true;
        };

        var original = new Label
        {
            Text = item.OriginalName,
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var arrow = new Label
        {
            Text = "→",
            FontSize = 14,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        };

        var entry = new Entry
        {
            Text = item.SuggestedName,
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Fill,
            IsEnabled = item.Error == null
        };
        entry.TextChanged += (_, e) => item.SuggestedName = e.NewTextValue ?? "";

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }
            },
            ColumnSpacing = 6,
            Padding = new Thickness(0, 4)
        };
        grid.Add(checkbox, 0, 0);
        grid.Add(original, 1, 0);
        grid.Add(arrow, 2, 0);
        grid.Add(entry, 3, 0);

        if (item.Error == null)
            return grid;

        return new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                grid,
                new Label
                {
                    Text = $"Error: {item.Error}",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#C62828"),
                    Margin = new Thickness(30, 0, 0, 4)
                }
            }
        };
    }

    private async void OnExecuteRenamesClicked(object? sender, EventArgs e)
    {
        if (_renameExecuteRunning || _renameDryRunRunning)
            return;

        if (_renamePreview == null || string.IsNullOrWhiteSpace(_renameFolderPath))
            return;

        var selected = _renamePreview
            .Where(p => p.Selected && !string.IsNullOrWhiteSpace(p.SuggestedName))
            .ToList();

        if (selected.Count == 0)
        {
            await DisplayAlert("Nothing to rename", "No files selected with a non-empty suggested name.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Rename files on disk?",
            $"Rename {selected.Count} file(s) on disk in:\n{_renameFolderPath}\n\nThis cannot be undone automatically.",
            "Rename",
            "Cancel");

        if (!confirm)
            return;

        _renameExecuteRunning = true;
        _renamePickFolderButton.IsEnabled = false;
        _renameDryRunButton.IsEnabled = false;
        _renameExecuteButton.IsEnabled = false;
        _renameProgressLabel.IsVisible = true;

        var folder = _renameFolderPath;
        var renamed = 0;
        var skipped = 0;
        var failed = 0;
        var failures = new List<string>();

        try
        {
            await Task.Run(() =>
            {
                for (var i = 0; i < selected.Count; i++)
                {
                    var item = selected[i];
                    var current = i + 1;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _renameProgressLabel.Text = $"Renaming {current}/{selected.Count}...";
                    });

                    try
                    {
                        var sanitized = SanitizeForFilename(item.SuggestedName);
                        if (string.IsNullOrWhiteSpace(sanitized))
                        {
                            skipped++;
                            continue;
                        }

                        var newPath = Path.Combine(folder, sanitized + item.Extension);
                        var collisionCounter = 2;
                        while (File.Exists(newPath) &&
                               !string.Equals(newPath, item.OriginalPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (collisionCounter > 100)
                                throw new InvalidOperationException("Could not find an available filename.");

                            newPath = Path.Combine(folder, $"{sanitized}_{collisionCounter}{item.Extension}");
                            collisionCounter++;
                        }

                        if (string.Equals(newPath, item.OriginalPath, StringComparison.OrdinalIgnoreCase))
                        {
                            skipped++;
                            continue;
                        }

                        File.Move(item.OriginalPath, newPath);
                        renamed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failures.Add($"{item.OriginalName}: {ex.Message}");
                    }
                }
            });
        }
        finally
        {
            _renameExecuteRunning = false;
            _renamePickFolderButton.IsEnabled = true;
            _renameDryRunButton.IsEnabled = true;
            _renameProgressLabel.IsVisible = false;
            _renamePreview = null;
            _renamePreviewStack.Children.Clear();
            _renameExecuteButton.IsEnabled = false;
        }

        var summary = $"Renamed: {renamed}\nSkipped: {skipped}\nFailed: {failed}";
        if (failures.Count > 0)
        {
            summary += "\n\nFailures:\n" + string.Join("\n", failures.Take(10));
            if (failures.Count > 10)
                summary += $"\n...and {failures.Count - 10} more.";
        }

        await DisplayAlert("Rename complete", summary, "OK");

        if (Directory.Exists(folder))
            SetRenameFolder(folder);
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
