using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Main page for managing audio items (quotes, anecdotes, lessons).
/// Supports adding items by category, attaching audio files,
/// or generating audio from text.
/// </summary>
public class AudioLibraryPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly AudioLibraryService _audioLib;

    private Picker _categoryPicker;
    private VerticalStackLayout _toolbarContainer;
    private VerticalStackLayout _gridContainer;
    private Label _statsLabel;
    private Button _actionsButton;
    private List<string> _categories = new();
    private List<AudioItem> _currentItems = new();
    private AudioItem? _selectedItem;
    private string _selectedCategory = "All";

    public AudioLibraryPage(AuthService auth, AudioLibraryService audioLib)
    {
        _auth = auth;
        _audioLib = audioLib;

        Title = "Audio Library";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },  // Header + filters
                new RowDefinition { Height = GridLength.Star },  // Items list
                new RowDefinition { Height = GridLength.Auto }   // Bottom bar
            }
        };

        // ===== Header =====
        var headerStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        headerStack.Children.Add(new Label
        {
            Text = "🔊 Audio Library",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _statsLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#888")
        };
        headerStack.Children.Add(_statsLabel);

        // Category filter
        var filterRow = new HorizontalStackLayout { Spacing = 10 };

        filterRow.Children.Add(new Label
        {
            Text = "Category:",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        _categoryPicker = new Picker
        {
            Title = "All",
            FontSize = 14,
            BackgroundColor = Colors.White,
            WidthRequest = 200
        };
        _categoryPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_categoryPicker.SelectedIndex >= 0)
            {
                _selectedCategory = _categoryPicker.SelectedIndex == 0
                    ? "All"
                    : _categories[_categoryPicker.SelectedIndex - 1];
                await LoadItemsAsync();
            }
        };
        filterRow.Children.Add(_categoryPicker);

        _actionsButton = new Button
        {
            Text = "⋮ Actions",
            FontSize = 13,
            BackgroundColor = Color.FromArgb("#4527A0"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 36,
            IsEnabled = false
        };
        _actionsButton.Clicked += async (s, e) =>
        {
            if (_selectedItem != null)
                await ShowItemActionsAsync(_selectedItem);
        };
        filterRow.Children.Add(_actionsButton);

        headerStack.Children.Add(filterRow);

        Grid.SetRow(headerStack, 0);
        mainGrid.Children.Add(headerStack);

        // ===== Data grid: fixed toolbar + scrollable grid, same pattern as DatabasesPage =====
        var gridArea = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Padding = new Thickness(16, 4)
        };

        _toolbarContainer = new VerticalStackLayout
        {
            Padding = new Thickness(0, 0, 0, 4)
        };
        gridArea.Add(_toolbarContainer, 0, 0);

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Spacing = 4 };
        scrollView.Content = _gridContainer;
        gridArea.Add(scrollView, 0, 1);

        Grid.SetRow(gridArea, 1);
        mainGrid.Children.Add(gridArea);

        // ===== Bottom bar =====
        var bottomBar = new Grid
        {
            Padding = 12,
            BackgroundColor = Colors.White,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };

        var btnAddText = new Button
        {
            Text = "✍️ Add from Text",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        btnAddText.Clicked += OnAddFromTextClicked;
        Grid.SetColumn(btnAddText, 0);
        bottomBar.Children.Add(btnAddText);

        var btnAddAudio = new Button
        {
            Text = "🔊 Add from Audio File",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        btnAddAudio.Clicked += OnAddFromAudioClicked;
        Grid.SetColumn(btnAddAudio, 1);
        bottomBar.Children.Add(btnAddAudio);

        Grid.SetRow(bottomBar, 2);
        mainGrid.Children.Add(bottomBar);

        Content = mainGrid;
    }

    private async Task LoadAsync()
    {
        // Load categories
        _categories = await _audioLib.GetCategoriesAsync(_auth.CurrentUsername);

        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("All");
        foreach (var cat in _categories)
            _categoryPicker.Items.Add(cat);

        if (_selectedCategory == "All")
            _categoryPicker.SelectedIndex = 0;
        else
        {
            int idx = _categories.IndexOf(_selectedCategory);
            _categoryPicker.SelectedIndex = idx >= 0 ? idx + 1 : 0;
        }

        // Stats
        var (total, withAudio, favorites, catCount) = await _audioLib.GetStatsAsync(_auth.CurrentUsername);
        _statsLabel.Text = $"{total} items • {withAudio} with audio • {favorites} favorites • {catCount} categories";

        await LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();
        _selectedItem = null;
        _actionsButton.IsEnabled = false;

        List<AudioItem> items;
        if (_selectedCategory == "All")
            items = await _audioLib.GetAllAsync(_auth.CurrentUsername);
        else
            items = await _audioLib.GetByCategoryAsync(_auth.CurrentUsername, _selectedCategory);

        _currentItems = items;

        if (items.Count == 0)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "No audio items yet.\n\nAdd quotes, anecdotes, or lessons\nusing the buttons below.",
                FontSize = 16,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 60, 0, 0)
            });
            return;
        }

        // Build columns and rows for DataGridView
        var columns = new List<string> { "Id", "Text", "Category", "Source", "Notes", "Audio", "Played", "Fav", "CreatedAt", "ModifiedAt" };
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var item in items)
        {
            var fullRow = BuildAudioGridRow(item);
            var displayRow = fullRow.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList();
            fullRows.Add(fullRow);
            displayRows.Add(displayRow);
        }

        var dataGrid = DataGridView.Create(columns, displayRows)
            .WithHeaderStyle(Color.FromArgb("#4527A0"), Colors.White)
            .WithAlternateRowColor(Color.FromArgb("#F5F0FF"))
            .WithColumnWidths(60, 400)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentItems.Count)
                {
                    _selectedItem = _currentItems[e.RowIndex];
                    _actionsButton.IsEnabled = true;
                }
            })
            .WithUpdateCallback(UpdateAudioGridCellAsync)
            .Build();

        _toolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    private static List<string> BuildAudioGridRow(AudioItem item)
    {
        return new List<string>
        {
            item.Id.ToString(),
            item.Text,
            item.Category,
            item.Source ?? "",
            item.Notes ?? "",
            item.AudioStatusDisplay,
            item.TimesPlayed.ToString(),
            item.IsFavorite ? "true" : "false",
            item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            item.ModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? ""
        };
    }

    private async Task<bool> UpdateAudioGridCellAsync(string idValue, string columnName, string newValue)
    {
        if (!int.TryParse(idValue, out int id))
            return false;

        var item = _currentItems.FirstOrDefault(i => i.Id == id)
            ?? (await _audioLib.GetAllAsync(_auth.CurrentUsername)).FirstOrDefault(i => i.Id == id);
        if (item == null)
            return false;

        switch (columnName)
        {
            case "Text":
                if (string.IsNullOrWhiteSpace(newValue)) return false;
                item.Text = newValue.Trim();
                break;
            case "Category":
                item.Category = string.IsNullOrWhiteSpace(newValue) || newValue.Equals("NULL", StringComparison.OrdinalIgnoreCase)
                    ? "General"
                    : newValue.Trim();
                break;
            case "Source":
                item.Source = newValue == "NULL" ? "" : newValue.Trim();
                break;
            case "Notes":
                item.Notes = newValue == "NULL" ? "" : newValue;
                break;
            case "Played":
                if (!int.TryParse(newValue, out int timesPlayed) || timesPlayed < 0) return false;
                item.TimesPlayed = timesPlayed;
                break;
            case "Fav":
                if (!TryParseFavorite(newValue, out bool favorite)) return false;
                item.IsFavorite = favorite;
                break;
            case "Id":
            case "Audio":
            case "CreatedAt":
            case "ModifiedAt":
            default:
                return false;
        }

        await _audioLib.UpdateAsync(item);

        if (columnName == "Category")
            await LoadAsync();
        else
            await LoadItemsAsync();

        return true;
    }

    private static bool TryParseFavorite(string value, out bool favorite)
    {
        favorite = false;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "true" or "yes" or "y" or "1" or "favorite" or "fav" or "⭐")
        {
            favorite = true;
            return true;
        }

        if (normalized is "false" or "no" or "n" or "0" or "unfavorite" or "not favorite" or "☆")
        {
            favorite = false;
            return true;
        }

        return false;
    }

    private async Task ShowItemActionsAsync(AudioItem item)
    {
        var options = new List<string>();

        if (item.HasAudio)
        {
            options.Add("▶️ Play");
            options.Add("🔄 Replace Audio");
            options.Add("🔇 Remove Audio");
        }
        else
        {
            options.Add("📂 Add Audio File");
            options.Add("🎙️ Generate Audio");
        }

        options.Add(item.IsFavorite ? "☆ Unfavorite" : "⭐ Favorite");
        options.Add("✏️ Edit Text");
        options.Add("📁 Change Category");
        options.Add("📝 Edit Source");
        options.Add("💭 Edit Notes");
        options.Add("🗑️ Delete");

        string? action = await DisplayActionSheet(
            item.TextPreview,
            "Cancel",
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action == "▶️ Play")
            await PlayAudioAsync(item);
        else if (action == "📂 Add Audio File" || action == "🔄 Replace Audio")
            await BrowseAudioForItemAsync(item);
        else if (action == "🎙️ Generate Audio")
            await GenerateAudioForItemAsync(item);
        else if (action == "🔇 Remove Audio")
        {
            bool confirm = await DisplayAlert("Remove Audio", "Remove the audio file?", "Remove", "Cancel");
            if (confirm) { await _audioLib.RemoveAudioAsync(item); await LoadItemsAsync(); }
        }
        else if (action.Contains("Favorite") || action.Contains("Unfavorite"))
        {
            await _audioLib.ToggleFavoriteAsync(item.Id);
            await LoadItemsAsync();
        }
        else if (action == "✏️ Edit Text")
        {
            string? newText = await GetMultiLineInputAsync("Edit Text", "Update the text:", "Save", "Cancel", item.Text);
            if (newText != null) { item.Text = newText.Trim(); await _audioLib.UpdateAsync(item); await LoadItemsAsync(); }
        }
        else if (action == "📁 Change Category")
        {
            string? newCat = await PickCategoryAsync();
            if (newCat != null) { item.Category = newCat; await _audioLib.UpdateAsync(item); await LoadAsync(); }
        }
        else if (action == "📝 Edit Source")
        {
            string? newSource = await DisplayPromptAsync("Edit Source", "Who said this?", "Save", "Cancel", initialValue: item.Source, maxLength: 200);
            if (newSource != null) { item.Source = newSource.Trim(); await _audioLib.UpdateAsync(item); await LoadItemsAsync(); }
        }
        else if (action == "💭 Edit Notes")
        {
            string? newNotes = await DisplayPromptAsync("Edit Notes", "Your personal notes:", "Save", "Cancel", initialValue: item.Notes, maxLength: 1000);
            if (newNotes != null) { item.Notes = newNotes.Trim(); await _audioLib.UpdateAsync(item); await LoadItemsAsync(); }
        }
        else if (action == "🗑️ Delete")
        {
            bool confirm = await DisplayAlert("Delete", $"Delete this item?\n\n\"{item.TextPreview}\"", "Delete", "Cancel");
            if (confirm) { await _audioLib.DeleteAsync(item.Id); await LoadAsync(); }
        }
    }

    // ===== ADD FROM TEXT =====

    private async void OnAddFromTextClicked(object? sender, EventArgs e)
    {
        // Pick or create category
        string category = await PickCategoryAsync();
        if (category == null) return;

        // Enter text (multi-line)
        var textInput = new TextInputPage(
            "Add Audio Item",
            "Enter the quote, anecdote, or lesson:",
            "Type or paste text...",
            "Next",
            "Cancel");
        await Navigation.PushModalAsync(textInput);
        string? text = await textInput.WaitForResultAsync();

        if (string.IsNullOrWhiteSpace(text)) return;

        // Optional source
        string? source = await DisplayPromptAsync(
            "Source (optional)",
            "Who said this? Which book/video?",
            "Create",
            "Skip",
            placeholder: "e.g., Marcus Aurelius, The 48 Laws of Power");

        // Create the item
        var item = await _audioLib.CreateAsync(
            _auth.CurrentUsername,
            text,
            category,
            source ?? "");

        // Ask about audio
        string audioChoice = await DisplayActionSheet(
            "Add audio?",
            "Skip (add later)",
            null,
            "📂 Browse audio file",
            "🎙️ Generate from text");

        if (audioChoice == "📂 Browse audio file")
        {
            await BrowseAudioForItemAsync(item);
        }
        else if (audioChoice == "🎙️ Generate from text")
        {
            await GenerateAudioForItemAsync(item);
        }

        await LoadAsync();
    }

    // ===== ADD FROM AUDIO FILE =====

    private async void OnAddFromAudioClicked(object? sender, EventArgs e)
    {
        // Pick audio file first
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select audio file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac" } },
                    { DevicePlatform.Android, new[] { "audio/*" } },
                    { DevicePlatform.iOS, new[] { "public.audio" } }
                })
            });

            if (result == null) return;

            // Pick category
            string category = await PickCategoryAsync();
            if (category == null) return;

            // Enter text description (multi-line)
            var textInput = new TextInputPage(
                "What is this audio?",
                "Enter the text/quote this audio contains:",
                "The quote or lesson in this audio...",
                "Create",
                "Cancel");
            await Navigation.PushModalAsync(textInput);
            string? text = await textInput.WaitForResultAsync();

            if (string.IsNullOrWhiteSpace(text)) return;

            // Optional source
            string? source = await DisplayPromptAsync(
                "Source (optional)",
                "Who said this? Which book/video?",
                "Save",
                "Skip",
                placeholder: "e.g., Jordan Peterson, 12 Rules");

            // Create item
            var item = await _audioLib.CreateAsync(
                _auth.CurrentUsername,
                text,
                category,
                source ?? "");

            // Attach audio
            using var stream = await result.OpenReadAsync();
            await _audioLib.AttachAudioFromStreamAsync(item, stream, result.FileName);

            await DisplayAlert("Added", $"Audio item created with audio.\n\nCategory: {category}", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add audio: {ex.Message}", "OK");
        }
    }

    // ===== AUDIO HELPERS =====

    private async Task BrowseAudioForItemAsync(AudioItem item)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select audio file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac" } },
                    { DevicePlatform.Android, new[] { "audio/*" } },
                    { DevicePlatform.iOS, new[] { "public.audio" } }
                })
            });

            if (result == null) return;

            using var stream = await result.OpenReadAsync();
            await _audioLib.AttachAudioFromStreamAsync(item, stream, result.FileName);

            await DisplayAlert("Audio Added", "Audio file attached successfully.", "OK");
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to attach audio: {ex.Message}", "OK");
        }
    }

    private async Task GenerateAudioForItemAsync(AudioItem item)
    {
#if WINDOWS
        try
        {
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();

            // Let user pick a voice
            var voices = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices;
            var voiceNames = voices.Select(v => $"{v.DisplayName} ({v.Language})").ToArray();

            string? selectedVoice = null;
            if (voiceNames.Length > 1)
            {
                selectedVoice = await DisplayActionSheet("Select Voice", "Cancel", null, voiceNames);
                if (selectedVoice == null || selectedVoice == "Cancel")
                {
                    // Use default voice
                }
                else
                {
                    int idx = Array.IndexOf(voiceNames, selectedVoice);
                    if (idx >= 0)
                        synth.Voice = voices[idx];
                }
            }

            // Generate speech
            var stream = await synth.SynthesizeTextToStreamAsync(item.Text);

            // Save to temp file first
            string tempPath = Path.Combine(Path.GetTempPath(), $"tts_temp_{DateTime.Now.Ticks}.wav");
            using (var fileStream = File.Create(tempPath))
            {
                var inputStream = stream.AsStreamForRead();
                await inputStream.CopyToAsync(fileStream);
            }

            // Attach to item via service
            await _audioLib.MarkAudioAsGeneratedAsync(item, tempPath);

            // Clean up temp
            try { File.Delete(tempPath); } catch { }

            await DisplayAlert("Audio Generated",
                $"TTS audio created and attached.\n\nVoice: {synth.Voice.DisplayName}",
                "OK");

            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("TTS Error", $"Failed to generate audio: {ex.Message}", "OK");
        }
#else
        // Non-Windows: clipboard fallback
        await Clipboard.SetTextAsync(item.Text);
        await DisplayAlert("Generate Audio",
            "Text copied to clipboard.\n\n" +
            "TTS generation is currently only available on Windows.\n" +
            "Use an external TTS service, then attach the audio file.",
            "OK");
#endif
    }

    private async Task PlayAudioAsync(AudioItem item)
    {
        try
        {
            var audioPath = _audioLib.GetAudioFullPath(item);
            if (audioPath == null)
            {
                await DisplayAlert("No Audio", "Audio file not found.", "OK");
                return;
            }

#if WINDOWS
            // Use Windows process to play audio
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = audioPath;
            process.StartInfo.UseShellExecute = true;
            process.Start();
#elif ANDROID
            // Use Android media player intent
            // For now just show path
            await DisplayAlert("Play", $"Playing: {Path.GetFileName(audioPath)}", "OK");
#endif

            // Update play count
            item.TimesPlayed++;
            item.LastPlayedAt = DateTime.Now;
            await _audioLib.UpdateAsync(item);
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to play audio: {ex.Message}", "OK");
        }
    }

    // ===== EDIT =====

    // ===== CATEGORY PICKER =====

    private async Task<string?> PickCategoryAsync()
    {
        var options = new List<string>(_categories);
        options.Add("➕ New Category");

        string? selected = await DisplayActionSheet(
            "Select Category",
            "Cancel",
            null,
            options.ToArray());

        if (selected == null || selected == "Cancel") return null;

        if (selected == "➕ New Category")
        {
            string? newCat = await DisplayPromptAsync(
                "New Category",
                "Enter category name:",
                "Create",
                "Cancel",
                placeholder: "e.g., Philosophy, Business, Motivation");

            if (string.IsNullOrWhiteSpace(newCat)) return null;
            return newCat.Trim();
        }

        return selected;
    }

    /// <summary>
    /// Show a multi-line text input modal and return the result.
    /// </summary>
    private async Task<string?> GetMultiLineInputAsync(string title, string subtitle,
        string confirmText = "OK", string cancelText = "Cancel", string initialValue = "")
    {
        var inputPage = new TextInputPage(title, subtitle, "Type or paste text...",
            confirmText, cancelText, initialValue);
        await Navigation.PushModalAsync(inputPage);
        return await inputPage.WaitForResultAsync();
    }
}
