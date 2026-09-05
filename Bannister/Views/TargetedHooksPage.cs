using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class TargetedHooksPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomPromptService _customPrompts;
    private readonly CropPresetService _cropPresets;
    private readonly IPanelSaver _panelSaver;
    private Editor _promptEntry = null!;
    private Picker _favoritesPicker = null!;
    private Label _outputLabel = null!;
    private Button _copyOutputButton = null!;
    private Editor _suffixEditor = null!;
    private bool _isLoadingSuffix;
    private List<CustomPromptItem> _favorites = new();
    private const string SuffixStorageKey_Prefix = "targeted_hooks_suffix_custom_";
    private const string FavoritesArea = "TargetedHooks";
    private const string DefaultSuffix =
        "Create the result as a single 9:16 vertical concept sheet containing 20 numbered " +
        "variations arranged in a 4x5 grid. Each panel must show a completely different idea, " +
        "composition, story moment, camera angle, environment, mood, and visual hook. " +
        "Prioritize variety of ideas over small visual changes. Large visible numbers 1–20. " +
        "Cinematic realistic, high detail, easy side-by-side comparison, no text except numbers.";

    public TargetedHooksPage(
        AuthService auth,
        CustomPromptService customPrompts,
        CropPresetService cropPresets,
        IPanelSaver panelSaver)
    {
        _auth = auth;
        _customPrompts = customPrompts;
        _cropPresets = cropPresets;
        _panelSaver = panelSaver;
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
        stack.Children.Add(new Label { Text = " Targeted Hooks", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        stack.Children.Add(new Label { Text = "Enter a starting prompt. The suffix below is appended automatically before copying.", FontSize = 14, TextColor = Color.FromArgb("#666") });
        stack.Children.Add(BuildStage1Section());
        stack.Children.Add(BuildSuffixSection());
        stack.Children.Add(BuildOutputSection());
        // Grid Cropper tool
        var cropperBtn = new Button
        {
            Text = " Open Grid Cropper",
            BackgroundColor = Color.FromArgb("#37474F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            Margin = new Thickness(0, 4, 0, 0)
        };
        cropperBtn.Clicked += async (_, _) =>
            await Navigation.PushAsync(
                new GridCropperPage(_auth, _cropPresets, _panelSaver));
        stack.Children.Add(cropperBtn);
        Content = new ScrollView { Content = stack };
    }

    private Frame BuildStage1Section()
    {
        var sectionStack = new VerticalStackLayout { Spacing = 10 };
        sectionStack.Children.Add(new Label { Text = "Stage 1 — Starting Prompt", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        sectionStack.Children.Add(new Label { Text = "Type your prompt or choose a favourite. Tap 'Add to Favourites' to save the current text.", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _favoritesPicker = new Picker { Title = "Choose from favourites…", BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222"), TitleColor = Color.FromArgb("#999") };
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
        var addFavBtn = new Button { Text = "★ Add to Favourites", BackgroundColor = Color.FromArgb("#FFF8E1"), TextColor = Color.FromArgb("#F57F17"), CornerRadius = 8, FontSize = 13, HeightRequest = 40, Padding = new Thickness(14, 0) };
        addFavBtn.Clicked += async (_, _) => await AddToFavouritesAsync();
        actionRow.Children.Add(addFavBtn);
        var deleteFavBtn = new Button { Text = "✕ Remove Favourite", BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828"), CornerRadius = 8, FontSize = 13, HeightRequest = 40, Padding = new Thickness(14, 0) };
        deleteFavBtn.Clicked += async (_, _) => await DeleteSelectedFavouriteAsync();
        actionRow.Children.Add(deleteFavBtn);
        sectionStack.Children.Add(actionRow);
        var buildBtn = new Button { Text = "▶ Build Full Prompt", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold };
        buildBtn.Clicked += OnBuildPromptClicked;
        sectionStack.Children.Add(buildBtn);
        return new Frame { BackgroundColor = Colors.White, Padding = 16, CornerRadius = 12, HasShadow = true, Content = sectionStack };
    }

    private Frame BuildSuffixSection()
    {
        var sectionStack = new VerticalStackLayout { Spacing = 10 };
        sectionStack.Children.Add(new Label { Text = "Appended Suffix (editable, saved automatically)", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        sectionStack.Children.Add(new Label { Text = "This text is appended to every prompt. Edit it here to change the output format for all future uses.", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _suffixEditor = new Editor { HeightRequest = 160, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Color.FromArgb("#FAFAFA"), TextColor = Color.FromArgb("#222"), FontSize = 12, Placeholder = "Appended suffix will load here…", PlaceholderColor = Color.FromArgb("#999") };
        _suffixEditor.TextChanged += async (_, e) => { if (!_isLoadingSuffix) await SaveSuffixAsync(e.NewTextValue ?? ""); };
        sectionStack.Children.Add(_suffixEditor);
        var resetBtn = new Button { Text = "↺ Reset to Default", BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F"), CornerRadius = 8, FontSize = 12, HeightRequest = 36, HorizontalOptions = LayoutOptions.Start, Padding = new Thickness(12, 0) };
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
        _copyOutputButton = new Button { Text = " Copy Full Prompt", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold, IsVisible = false };
        _copyOutputButton.Clicked += async (_, _) => await CopyFullPromptAsync();
        sectionStack.Children.Add(_copyOutputButton);
        return new Frame { BackgroundColor = Colors.White, Padding = 16, CornerRadius = 12, HasShadow = true, Content = sectionStack };
    }

    private void OnBuildPromptClicked(object? sender, EventArgs e)
    {
        var basePrompt = (_promptEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(basePrompt)) { _outputLabel.Text = "Please enter a starting prompt first."; _outputLabel.TextColor = Color.FromArgb("#C62828"); _copyOutputButton.IsVisible = false; return; }
        var suffix = (_suffixEditor.Text ?? DefaultSuffix).Trim();
        _outputLabel.Text = string.IsNullOrWhiteSpace(suffix) ? basePrompt : $"{basePrompt}\n\n{suffix}";
        _outputLabel.TextColor = Color.FromArgb("#222");
        _copyOutputButton.IsVisible = true;
    }

    private async Task CopyFullPromptAsync()
    {
        var text = _outputLabel.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        await Clipboard.SetTextAsync(text);
        var original = _copyOutputButton.Text;
        _copyOutputButton.Text = "✓ Copied!";
        await Task.Delay(1500);
        _copyOutputButton.Text = original;
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
        _favoritesPicker.Title = _favorites.Count == 0 ? "No favourites yet" : "Choose from favourites…";
    }

    private string SuffixKey => $"{SuffixStorageKey_Prefix}{_auth.CurrentUsername}";

    private async Task LoadSuffixAsync()
    {
        _isLoadingSuffix = true;
        try { var stored = await SecureStorage.GetAsync(SuffixKey); _suffixEditor.Text = string.IsNullOrWhiteSpace(stored) ? DefaultSuffix : stored; }
        catch { _suffixEditor.Text = DefaultSuffix; }
        finally { _isLoadingSuffix = false; }
    }

    private async Task SaveSuffixAsync(string value)
    {
        try { if (string.IsNullOrWhiteSpace(value)) SecureStorage.Remove(SuffixKey); else await SecureStorage.SetAsync(SuffixKey, value); }
        catch { }
    }
}
