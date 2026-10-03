using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class TargetedHooksPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomPromptService _customPrompts;
    private readonly CropPresetService _cropPresets;
    private readonly IPanelSaver _panelSaver;
    private readonly HookPrefixService _hookPrefixes;
    private readonly AppSettingsService _appSettings;
    private Editor _promptEntry = null!;
    private Picker _favoritesPicker = null!;
    private Label _outputLabel = null!;
    private Button _copyOutputButton = null!;
    private Editor _suffixEditor = null!;
    private Entry _croppedCountEntry = null!;
    private bool _isLoadingSuffix;
    private List<CustomPromptItem> _favorites = new();
    private static readonly Random PrefixModeRandom = new();
    private const string SuffixStorageKeyPrefix = "targeted_hooks_suffix_custom_";
    private const string FavoritesArea = "TargetedHooks";
    private const string PrefixIdeasPrompt =
        "Generate 10 creative and varied starting-frame prompt prefixes for AI video generation. Each prefix should describe a visual scenario, character type, environment, or situation that would make a compelling 10-second hook video. Return them as a numbered list, one per line, no explanations.";
    private const string DefaultSuffix =
        "Create the result as a single 9:16 vertical concept sheet containing 20 numbered " +
        "variations arranged in a 4x5 grid. Each panel must show a completely different idea, " +
        "composition, story moment, camera angle, environment, mood, and visual hook. " +
        "Prioritize variety of ideas over small visual changes. Large visible numbers 1-20. " +
        "Cinematic realistic, high detail, easy side-by-side comparison, no text except numbers.";

    public TargetedHooksPage(
        AuthService auth,
        CustomPromptService customPrompts,
        CropPresetService cropPresets,
        IPanelSaver panelSaver,
        HookPrefixService hookPrefixes,
        AppSettingsService appSettings)
    {
        _auth = auth;
        _customPrompts = customPrompts;
        _cropPresets = cropPresets;
        _panelSaver = panelSaver;
        _hookPrefixes = hookPrefixes;
        _appSettings = appSettings;
        Title = "Targeted Hooks";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSuffixAsync();
        await LoadFavoritesAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 20 };
        stack.Children.Add(new Label { Text = "Targeted Hooks", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        stack.Children.Add(new Label { Text = "Enter a starting prompt. A prefix is prepended and the suffix below is appended automatically before copying.", FontSize = 14, TextColor = Color.FromArgb("#666") });
        stack.Children.Add(BuildStage1Section());
        stack.Children.Add(BuildSuffixSection());
        stack.Children.Add(BuildOutputSection());

        var cropperBtn = new Button
        {
            Text = "Open Grid Cropper",
            BackgroundColor = Color.FromArgb("#37474F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            Margin = new Thickness(0, 4, 0, 0)
        };
        cropperBtn.Clicked += async (_, _) =>
            await Navigation.PushAsync(
                new GridCropperPage(_auth, _cropPresets, _panelSaver, _hookPrefixes));
        stack.Children.Add(cropperBtn);

        Content = new ScrollView { Content = stack };
    }

    private Frame BuildStage1Section()
    {
        var sectionStack = new VerticalStackLayout { Spacing = 10 };
        sectionStack.Children.Add(new Label { Text = "Stage 1 - Starting Prompt", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        sectionStack.Children.Add(new Label { Text = "Type your prompt or choose a favourite. Build Full Prompt randomly asks for a new, existing, or combined prefix.", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _favoritesPicker = new Picker { Title = "Choose from favourites...", BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222"), TitleColor = Color.FromArgb("#999") };
        _favoritesPicker.SelectedIndexChanged += OnFavouriteSelected;
        sectionStack.Children.Add(_favoritesPicker);
        _promptEntry = new Editor
        {
            Placeholder = "e.g. A lone astronaut discovering an alien forest at dawn",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            FontSize = 14,
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges
        };
        sectionStack.Children.Add(_promptEntry);

        var actionRow = new HorizontalStackLayout { Spacing = 10 };
        var addFavBtn = new Button { Text = "Add to Favourites", BackgroundColor = Color.FromArgb("#FFF8E1"), TextColor = Color.FromArgb("#F57F17"), CornerRadius = 8, FontSize = 13, HeightRequest = 40, Padding = new Thickness(14, 0) };
        addFavBtn.Clicked += async (_, _) => await AddToFavouritesAsync();
        actionRow.Children.Add(addFavBtn);
        var deleteFavBtn = new Button { Text = "Remove Favourite", BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828"), CornerRadius = 8, FontSize = 13, HeightRequest = 40, Padding = new Thickness(14, 0) };
        deleteFavBtn.Clicked += async (_, _) => await DeleteSelectedFavouriteAsync();
        actionRow.Children.Add(deleteFavBtn);
        sectionStack.Children.Add(actionRow);

        var ideasBtn = new Button { Text = "Get Prefix Ideas", BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F"), CornerRadius = 8, FontSize = 13, HeightRequest = 40, Padding = new Thickness(14, 0) };
        ideasBtn.Clicked += async (_, _) => await CopyPrefixIdeasPromptAsync();
        sectionStack.Children.Add(ideasBtn);

        var buildBtn = new Button { Text = "Build Full Prompt", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold };
        buildBtn.Clicked += async (_, _) => await BuildPromptAsync();
        sectionStack.Children.Add(buildBtn);
        sectionStack.Children.Add(BuildRecordCropResultSection());

        return new Frame { BackgroundColor = Colors.White, Padding = 16, CornerRadius = 12, HasShadow = true, Content = sectionStack };
    }

    private View BuildRecordCropResultSection()
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stack.Children.Add(new Label { Text = "Record Crop Result", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        stack.Children.Add(new Label { Text = "Save how many panels from the active prefix session were cropped.", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _croppedCountEntry = new Entry
        {
            Placeholder = "How many of the 20 were cropped?",
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999")
        };
        stack.Children.Add(_croppedCountEntry);

        var saveButton = new Button
        {
            Text = "Save Crop Result",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40
        };
        saveButton.Clicked += async (_, _) => await SaveCropResultAsync();
        stack.Children.Add(saveButton);
        return stack;
    }

    private Frame BuildSuffixSection()
    {
        var sectionStack = new VerticalStackLayout { Spacing = 10 };
        sectionStack.Children.Add(new Label { Text = "Appended Suffix (editable, saved automatically)", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        sectionStack.Children.Add(new Label { Text = "This text is appended to every prompt. Edit it here to change the output format for all future uses.", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _suffixEditor = new Editor { HeightRequest = 160, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Color.FromArgb("#FAFAFA"), TextColor = Color.FromArgb("#222"), FontSize = 12, Placeholder = "Appended suffix will load here...", PlaceholderColor = Color.FromArgb("#999") };
        _suffixEditor.TextChanged += async (_, e) => { if (!_isLoadingSuffix) await SaveSuffixAsync(e.NewTextValue ?? ""); };
        sectionStack.Children.Add(_suffixEditor);
        var resetBtn = new Button { Text = "Reset to Default", BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F"), CornerRadius = 8, FontSize = 12, HeightRequest = 36, HorizontalOptions = LayoutOptions.Start, Padding = new Thickness(12, 0) };
        resetBtn.Clicked += async (_, _) => { _suffixEditor.Text = DefaultSuffix; await SaveSuffixAsync(DefaultSuffix); };
        sectionStack.Children.Add(resetBtn);
        return new Frame { BackgroundColor = Colors.White, Padding = 16, CornerRadius = 12, HasShadow = true, Content = sectionStack };
    }

    private Frame BuildOutputSection()
    {
        var sectionStack = new VerticalStackLayout { Spacing = 10 };
        sectionStack.Children.Add(new Label { Text = "Full Prompt Output", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        _outputLabel = new Label { Text = "Tap 'Build Full Prompt' above to preview and copy.", FontSize = 13, TextColor = Color.FromArgb("#444"), LineBreakMode = LineBreakMode.WordWrap };
        sectionStack.Children.Add(_outputLabel);
        _copyOutputButton = new Button { Text = "Copy Full Prompt", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold, IsVisible = false };
        _copyOutputButton.Clicked += async (_, _) => await CopyFullPromptAsync();
        sectionStack.Children.Add(_copyOutputButton);
        return new Frame { BackgroundColor = Colors.White, Padding = 16, CornerRadius = 12, HasShadow = true, Content = sectionStack };
    }

    private async Task BuildPromptAsync()
    {
        var basePrompt = (_promptEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(basePrompt))
        {
            _outputLabel.Text = "Please enter a starting prompt first.";
            _outputLabel.TextColor = Color.FromArgb("#C62828");
            _copyOutputButton.IsVisible = false;
            return;
        }

        var prefixChoice = await ChoosePrefixAsync();
        if (prefixChoice == null)
            return;

        var suffix = (_suffixEditor.Text ?? DefaultSuffix).Trim();
        var prefixedPrompt = string.IsNullOrWhiteSpace(prefixChoice.PrefixText)
            ? basePrompt
            : $"{prefixChoice.PrefixText}\n\n{basePrompt}";
        _outputLabel.Text = string.IsNullOrWhiteSpace(suffix)
            ? prefixedPrompt
            : $"{prefixedPrompt}\n\n{suffix}";
        _outputLabel.TextColor = Color.FromArgb("#222");
        _copyOutputButton.IsVisible = true;

        await _hookPrefixes.SaveSessionAsync(new HookPrefixSession
        {
            Username = _auth.CurrentUsername,
            PrefixText = prefixChoice.PrefixText,
            PrefixId1 = prefixChoice.PrefixId1,
            PrefixId2 = prefixChoice.PrefixId2,
            Mode = prefixChoice.Mode,
            CreatedDate = DateTime.UtcNow
        });

        var settings = await _hookPrefixes.GetSettingsAsync(_auth.CurrentUsername);
        settings.LastPrefixText = prefixChoice.PrefixText;
        await _hookPrefixes.SaveSettingsAsync(settings);
    }

    private async Task CopyFullPromptAsync()
    {
        var text = _outputLabel.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        await Clipboard.SetTextAsync(text);
        var original = _copyOutputButton.Text;
        _copyOutputButton.Text = "Copied!";
        await Task.Delay(1500);
        _copyOutputButton.Text = original;
    }

    private async Task CopyPrefixIdeasPromptAsync()
    {
        await Clipboard.SetTextAsync(PrefixIdeasPrompt);
        await DisplayAlert("Copied", "Prefix ideas prompt copied to clipboard.", "OK");
    }

    private async Task SaveCropResultAsync()
    {
        if (!int.TryParse(_croppedCountEntry.Text, out var cropped) ||
            cropped < 0)
        {
            await DisplayAlert("Invalid Count", "Enter a valid cropped count.", "OK");
            return;
        }

        var session = await _hookPrefixes.GetActiveSessionAsync(_auth.CurrentUsername);
        if (session == null)
        {
            await DisplayAlert("No Active Session", "Build a full prompt first, then record the crop result.", "OK");
            return;
        }

        await _hookPrefixes.UpdateSessionCropResultAsync(
            session.Id,
            cropped,
            session.TotalExtraordinary ?? 0);
        _croppedCountEntry.Text = "";
        await DisplayAlert("Saved", "Crop result recorded.", "OK");
    }

    private async Task<PrefixChoice?> ChoosePrefixAsync()
    {
        var prefixes = await _hookPrefixes.GetPrefixesAsync(_auth.CurrentUsername);
        var mode = PrefixModeRandom.Next(3) switch
        {
            0 => "new",
            1 => "existing",
            _ => "combined"
        };

        var page = new PrefixChoicePage(mode, prefixes);
        await Navigation.PushModalAsync(page);
        var choice = await page.Completion;
        if (choice == null || string.IsNullOrWhiteSpace(choice.PrefixText))
            return null;

        if (choice.AddNewToLibrary)
        {
            var prefix = new HookPrefix
            {
                Username = _auth.CurrentUsername,
                PrefixText = choice.PrefixText,
                CreatedDate = DateTime.UtcNow
            };
            await _hookPrefixes.SavePrefixAsync(prefix);
            choice = choice with { PrefixId1 = prefix.Id };
        }

        return choice;
    }

    private void OnFavouriteSelected(object? sender, EventArgs e)
    {
        if (_favoritesPicker.SelectedIndex >= 0 && _favoritesPicker.SelectedIndex < _favorites.Count)
            _promptEntry.Text = _favorites[_favoritesPicker.SelectedIndex].Text;
    }

    private async Task AddToFavouritesAsync()
    {
        var text = (_promptEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) { await DisplayAlert("Empty prompt", "Enter a prompt before saving to favourites.", "OK"); return; }
        string? title = await DisplayPromptAsync("Save to Favourites", "Give this prompt a short name:", "Save", "Cancel", placeholder: "e.g. Astronaut forest dawn");
        if (string.IsNullOrWhiteSpace(title)) return;
        await _customPrompts.AddCustomPromptAsync(_auth.CurrentUsername, FavoritesArea, title.Trim(), text);
        await LoadFavoritesAsync();
        await DisplayAlert("Saved", $"'{title.Trim()}' added to favourites.", "OK");
    }

    private async Task DeleteSelectedFavouriteAsync()
    {
        if (_favoritesPicker.SelectedIndex < 0 || _favoritesPicker.SelectedIndex >= _favorites.Count) { await DisplayAlert("No favourite selected", "Choose a favourite from the picker first.", "OK"); return; }
        var item = _favorites[_favoritesPicker.SelectedIndex];
        if (!await DisplayAlert("Remove Favourite", $"Remove '{item.Title}' from favourites?", "Remove", "Cancel")) return;
        await _customPrompts.DeleteCustomPromptAsync(item.Id);
        _promptEntry.Text = "";
        await LoadFavoritesAsync();
    }

    private async Task LoadFavoritesAsync()
    {
        _favorites = (await _customPrompts.GetCustomPromptsAsync(_auth.CurrentUsername, FavoritesArea)).OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase).ToList();
        _favoritesPicker.Items.Clear();
        foreach (var fav in _favorites) _favoritesPicker.Items.Add(fav.Title);
        _favoritesPicker.SelectedIndex = -1;
        _favoritesPicker.Title = _favorites.Count == 0 ? "No favourites yet" : "Choose from favourites...";
    }

    private string SuffixKey => $"{SuffixStorageKeyPrefix}{_auth.CurrentUsername}";

    private async Task LoadSuffixAsync()
    {
        _isLoadingSuffix = true;
        try
        {
            var stored = await _appSettings.GetAsync(_auth.CurrentUsername, SuffixKey);
            _suffixEditor.Text = string.IsNullOrWhiteSpace(stored) ? DefaultSuffix : stored;
        }
        catch { _suffixEditor.Text = DefaultSuffix; }
        finally { _isLoadingSuffix = false; }
    }

    private async Task SaveSuffixAsync(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                await _appSettings.DeleteAsync(_auth.CurrentUsername, SuffixKey);
            else
                await _appSettings.SetAsync(_auth.CurrentUsername, SuffixKey, value);
        }
        catch { }
    }

    private sealed record PrefixChoice(
        string Mode,
        string PrefixText,
        int? PrefixId1,
        int? PrefixId2,
        bool AddNewToLibrary);

    private sealed class PrefixChoicePage : ContentPage
    {
        private readonly TaskCompletionSource<PrefixChoice?> _completion = new();
        private readonly string _mode;
        private readonly List<HookPrefix> _prefixes;
        private readonly Entry _newPrefixEntry;
        private readonly CheckBox _saveNewCheckBox;
        private readonly Picker _prefixPicker1;
        private readonly Picker _prefixPicker2;
        private bool _isClosing;

        public Task<PrefixChoice?> Completion => _completion.Task;

        public PrefixChoicePage(string mode, List<HookPrefix> prefixes)
        {
            _mode = mode;
            _prefixes = prefixes;
            Title = "Choose Prefix";
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);

            _newPrefixEntry = new Entry
            {
                Placeholder = "Type a new prefix",
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#222"),
                PlaceholderColor = Color.FromArgb("#999")
            };
            _saveNewCheckBox = new CheckBox { IsChecked = true };
            _prefixPicker1 = CreatePrefixPicker();
            _prefixPicker2 = CreatePrefixPicker();

            Content = new Grid
            {
                Padding = 24,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 12,
                        Padding = 20,
                        HasShadow = true,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Fill,
                        Content = BuildContent()
                    }
                }
            };
        }

        private View BuildContent()
        {
            var stack = new VerticalStackLayout { Spacing = 12 };
            stack.Children.Add(new Label
            {
                Text = _mode switch
                {
                    "existing" => "Pick a saved prefix",
                    "combined" => "Combine two saved prefixes",
                    _ => "Create a new prefix"
                },
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222")
            });

            if (_mode == "existing")
            {
                stack.Children.Add(_prefixPicker1);
            }
            else if (_mode == "combined")
            {
                stack.Children.Add(_prefixPicker1);
                stack.Children.Add(_prefixPicker2);
            }
            else
            {
                stack.Children.Add(_newPrefixEntry);
                stack.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        _saveNewCheckBox,
                        new Label
                        {
                            Text = "Add to saved prefixes",
                            FontSize = 13,
                            TextColor = Color.FromArgb("#333"),
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                });
            }

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 8,
                HeightRequest = 42
            };
            cancelButton.Clicked += async (_, _) => await CloseAsync(null);

            var confirmButton = new Button
            {
                Text = "Use Prefix",
                BackgroundColor = Color.FromArgb("#1565C0"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 42
            };
            confirmButton.Clicked += async (_, _) => await ConfirmAsync();

            var buttonGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 10
            };
            buttonGrid.Add(cancelButton, 0, 0);
            buttonGrid.Add(confirmButton, 1, 0);
            stack.Children.Add(buttonGrid);

            return stack;
        }

        private Picker CreatePrefixPicker()
        {
            var picker = new Picker
            {
                Title = _prefixes.Count == 0 ? "No saved prefixes" : "Choose prefix",
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#222")
            };
            foreach (var prefix in _prefixes)
                picker.Items.Add(prefix.PrefixText);
            return picker;
        }

        private async Task ConfirmAsync()
        {
            if (_mode == "new")
            {
                var text = (_newPrefixEntry.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                await CloseAsync(new PrefixChoice(
                    "new",
                    text,
                    null,
                    null,
                    _saveNewCheckBox.IsChecked));
                return;
            }

            if (_mode == "existing")
            {
                if (_prefixPicker1.SelectedIndex < 0 ||
                    _prefixPicker1.SelectedIndex >= _prefixes.Count)
                    return;

                var prefix = _prefixes[_prefixPicker1.SelectedIndex];
                await CloseAsync(new PrefixChoice(
                    "existing",
                    prefix.PrefixText,
                    prefix.Id,
                    null,
                    false));
                return;
            }

            if (_prefixPicker1.SelectedIndex < 0 ||
                _prefixPicker2.SelectedIndex < 0 ||
                _prefixPicker1.SelectedIndex >= _prefixes.Count ||
                _prefixPicker2.SelectedIndex >= _prefixes.Count)
                return;

            var first = _prefixes[_prefixPicker1.SelectedIndex];
            var second = _prefixes[_prefixPicker2.SelectedIndex];
            await CloseAsync(new PrefixChoice(
                "combined",
                $"{first.PrefixText}\n\n{second.PrefixText}",
                first.Id,
                second.Id,
                false));
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(PrefixChoice? result)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            await Navigation.PopModalAsync();
            _completion.TrySetResult(result);
        }
    }
}
