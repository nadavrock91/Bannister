using Bannister.Services;

namespace Bannister.Views;

/// <summary>Per-user palette editor; editing a palette never changes the app display mode.</summary>
internal sealed class ThemeColorsView : ContentView
{
    private readonly AuthService _auth;
    private readonly AppSettingsService _settings;
    private readonly PrivacyModeService _privacy;
    private readonly Dictionary<ActivityDisplayMode, Button> _modeButtons = new();
    private readonly Label _modeNote = new() { FontSize = 12 };
    private readonly Entry _styleRequest = new() { Placeholder = "Optional style request (e.g. calm dark teal, less saturated)", MinimumWidthRequest = 180, HorizontalOptions = LayoutOptions.Fill };
    private readonly Button _export = new() { Text = "Export for LLM" };
    private readonly Button _import = new() { Text = "Import from LLM" };
    private ActivityDisplayMode _selectedMode;
    private bool _activated;
    private bool _subscribed;
    private readonly VerticalStackLayout _rows = new() { Spacing = 10 };
    private readonly Frame _preview = new() { Padding = 12, HasShadow = false };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly Dictionary<string, Color> _colors = new();
    private int _loadVersion;
    private readonly HashSet<string> _collapsed = new(new[] { "Cards", "Inputs", "Rows", "Status", "Buttons", "Highlights", "Brand" });

    private ActivityDisplayMode SelectedMode => _selectedMode;

    public ThemeColorsView(AuthService auth, AppSettingsService settings, PrivacyModeService privacy)
    {
        _auth = auth; _settings = settings; _privacy = privacy;
        var modeToggle = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 4
        };
        foreach (var mode in new[] { ActivityDisplayMode.PrivateOnly, ActivityDisplayMode.PublicOnly, ActivityDisplayMode.All })
        {
            var button = new Button { Text = ThemeImportExport.ModeName(mode), AutomationId = "EditPalette_" + mode };
            _modeButtons[mode] = button;
            button.Clicked += async (_, _) =>
            {
                if (SelectedMode == mode) return;
                _selectedMode = mode;
                await LoadAsync();
            };
            modeToggle.Add(button, mode == ActivityDisplayMode.PrivateOnly ? 0 : mode == ActivityDisplayMode.PublicOnly ? 1 : 2);
        }
        _styleRequest.ThemeColor(BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Entry.TextColorProperty, ThemeKey.InputText);
        _export.Clicked += async (_, _) => await ExportAsync();
        _import.Clicked += async (_, _) => await ImportAsync();
        var exchange = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        foreach (var control in new View[] { _styleRequest, _export, _import }) { control.Margin = 3; exchange.Children.Add(control); }
        FlexLayout.SetGrow(_styleRequest, 1);
        var resetMode = new Button { Text = "Reset this mode to defaults", FontSize = 12 };
        resetMode.Clicked += async (_, _) => await ResetAsync(false);
        var resetAll = new Button { Text = "Reset all modes", FontSize = 12 };
        resetAll.Clicked += async (_, _) => await ResetAsync(true);
        ModeThemedPage.SetPreserveColors(_preview, true);
        BuildPreview();
        Content = (new Frame
        {
            Padding = 20, CornerRadius = 12,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "UI Colors", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Choose a mode, then enter #RRGGBB (or #AARRGGBB for opacity) or tap a preset. Changes save immediately.", FontSize = 12 },
                    modeToggle, _modeNote, exchange, _status, _preview, _rows,
                    new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { resetMode, resetAll } }
                }
            }
        }).ThemeColor(BackgroundColorProperty, ThemeKey.CardBackground);
    }

    public async Task ActivateAsync()
    {
        if (!_activated)
        {
            _selectedMode = _privacy.GetDisplayMode(_auth.CurrentUsername);
            _activated = true;
        }
        if (!_subscribed) { PrivacyModeService.ModeChanged += OnModeChanged; _subscribed = true; }
        await LoadAsync();
    }

    public void Deactivate()
    {
        if (_subscribed) PrivacyModeService.ModeChanged -= OnModeChanged;
        _subscribed = false;
    }

    private void OnModeChanged(object? sender, DisplayModeChangedEventArgs args)
    {
        if (string.Equals(args.Username, _auth.CurrentUsername, StringComparison.OrdinalIgnoreCase))
            Dispatcher.Dispatch(UpdateModeNote);
    }

    private void UpdateModeNote()
    {
        var actual = _privacy.GetDisplayMode(_auth.CurrentUsername);
        _modeNote.IsVisible = actual != SelectedMode;
        _modeNote.Text = $"Editing {ThemeImportExport.ModeName(SelectedMode)} colors (app is in {ThemeImportExport.ModeName(actual)} mode)";
        foreach (var (mode, button) in _modeButtons)
            button.ThemeColor(BackgroundColorProperty, mode == SelectedMode ? ThemeKey.ButtonPrimaryBackground : ThemeKey.NeutralButtonBackground)
                .ThemeColor(Button.TextColorProperty, mode == SelectedMode ? ThemeKey.ButtonPrimaryText : ThemeKey.NeutralButtonText);
    }

    private async Task<ModeThemeColors> ReadAsync(ActivityDisplayMode mode)
    {
        await _writes.WaitAsync();
        try { return await ModeTheme.GetAsync(mode, _auth.CurrentUsername, _settings); }
        finally { _writes.Release(); }
    }

    private async Task LoadAsync()
    {
        int version = ++_loadVersion;
        var mode = SelectedMode;
        UpdateModeNote();
        _export.IsEnabled = _import.IsEnabled = false;
        try
        {
            ModeThemeColors colors = await ReadAsync(mode);
            if (version != _loadVersion) return;
            _colors.Clear();
            foreach (var token in colors.Tokens) _colors[token.Key] = token.Value;
            UpdatePreview();
            _rows.Children.Clear();
            foreach (var group in ModeTheme.Catalog.GroupBy(x => x.Group))
            {
                var body = new VerticalStackLayout { Spacing = 10, IsVisible = !_collapsed.Contains(group.Key) };
                foreach (var definition in group) body.Children.Add(BuildRow(definition, colors.Tokens[definition.Name], mode));
                var toggle = new Button { Text = $"{group.Key} ({group.Count()}) {(_collapsed.Contains(group.Key) ? "▸" : "▾")}", HorizontalOptions = LayoutOptions.Fill };
                string groupName = group.Key;
                int count = group.Count();
                toggle.Clicked += (_, _) =>
                {
                    if (!_collapsed.Add(groupName)) _collapsed.Remove(groupName);
                    body.IsVisible = !_collapsed.Contains(groupName);
                    toggle.Text = $"{groupName} ({count}) {(body.IsVisible ? "▾" : "▸")}";
                };
                _rows.Children.Add(toggle); _rows.Children.Add(body);
            }
            _status.Text = _settings.IsReadOnly ? "Colors can be changed on the master device." : "";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally
        {
            if (version == _loadVersion) { _export.IsEnabled = true; _import.IsEnabled = !_settings.IsReadOnly; }
        }
    }

    private View BuildRow(ThemeColorDefinition definition, Color color, ActivityDisplayMode mode)
    {
        string token = definition.Name;
        var swatch = new BoxView { Color = color, WidthRequest = 26, HeightRequest = 26, VerticalOptions = LayoutOptions.Center };
        ModeThemedPage.SetPreserveColors(swatch, true);
        var entry = (new Entry
        {
            Text = ModeTheme.ToHex(color), MaxLength = 9, WidthRequest = 130,
            IsEnabled = !_settings.IsReadOnly && !definition.Fixed,
            AutomationId = $"Theme_{token}"
        }).ThemeColor(BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Entry.TextColorProperty, ThemeKey.InputText);
        var validation = new Label { FontSize = 11, IsVisible = false }.ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText);
        entry.TextChanged += async (_, args) =>
        {
            if (!ModeTheme.TryNormalizeHex(args.NewTextValue, out string hex))
            {
                validation.Text = "Use #RRGGBB or #AARRGGBB."; validation.IsVisible = true;
                return;
            }
            validation.IsVisible = false;
            try
            {
                await SaveColorsAsync(mode, new Dictionary<string, string> { [token] = hex });
                if (mode == SelectedMode)
                {
                    swatch.Color = _colors[token];
                }
                _status.Text = "Saved";
            }
            catch (Exception ex) { _status.Text = $"Could not save: {ex.Message}"; }
        };
        var palette = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        palette.IsVisible = !definition.Fixed;
        foreach (string hex in ModeTheme.PresetDefaults().Select(ModeTheme.ToHex))
        {
            string choice = hex;
            var preset = (new Button
            {
                Text = "", BackgroundColor = ModeTheme.ParseStoredHex(choice), WidthRequest = 24, HeightRequest = 24,
                MinimumHeightRequest = 24, MinimumWidthRequest = 24, Margin = 2, Padding = 0,
                CornerRadius = 4, BorderWidth = 1, IsEnabled = !_settings.IsReadOnly
            }).ThemeColor(Button.BorderColorProperty, ThemeKey.Separator);
            ModeThemedPage.SetPreserveColors(preset, true);
            SemanticProperties.SetDescription(preset, $"{token}: {choice}");
            preset.Clicked += (_, _) => entry.Text = choice;
            palette.Children.Add(preset);
        }
        return new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = definition.Label + (definition.Fixed ? " (fixed)" : ""), FontSize = 14, FontAttributes = FontAttributes.Bold },
                new HorizontalStackLayout { Spacing = 8, Children = { swatch, entry } }, palette, validation
            }
        };
    }

    private async Task<int> SaveColorsAsync(ActivityDisplayMode mode, IReadOnlyDictionary<string, string> values)
    {
        var normalized = new Dictionary<string, string>();
        foreach (var (name, value) in values)
        {
            var definition = ThemeImportExport.FindColor(name);
            if (definition == null || definition.Fixed || definition.Group == "Brand" || !ModeTheme.TryNormalizeHex(value, out string hex))
                throw new InvalidOperationException($"Cannot save color '{name}'.");
            normalized[definition.Name] = hex;
        }
        int saved = 0;
        string user = _auth.CurrentUsername;
        await _writes.WaitAsync();
        try
        {
            foreach (var (name, hex) in normalized)
            {
                if (_settings.IsReadOnly) throw new InvalidOperationException("Colors can be changed on the master device.");
                await _settings.SetAsync(user, ModeTheme.StoragePrefix(mode) + name, hex);
                saved++;
                if (mode == SelectedMode) _colors[name] = ModeTheme.ParseStoredHex(hex);
            }
            if (mode == SelectedMode) UpdatePreview();
        }
        finally
        {
            _writes.Release();
            if (saved > 0) _privacy.NotifyThemeChanged(user);
        }
        return saved;
    }

    private Page HostPage
    {
        get
        {
            for (Element? node = this; node != null; node = node.Parent) if (node is Page page) return page;
            throw new InvalidOperationException("UI Colors is not attached to a page.");
        }
    }

    private async Task ExportAsync()
    {
        var mode = SelectedMode;
        try
        {
            var palette = await ReadAsync(mode);
            await Clipboard.SetTextAsync(ThemeImportExport.BuildPrompt(mode, palette.Tokens, _styleRequest.Text));
            _status.Text = $"Copied {ThemeImportExport.ModeName(mode)} palette prompt.";
        }
        catch (Exception ex) { await ShowFailureAsync("Could not export colors", ex); }
    }

    private async Task ImportAsync()
    {
        var mode = SelectedMode;
        try
        {
            var paste = new ThemePastePage();
            await HostPage.Navigation.PushModalAsync(paste);
            string? text = await paste.Result;
            if (text == null) return;
            var current = (await ReadAsync(mode)).Tokens;
            var parsed = ThemeImportExport.Parse(text, current);
            var review = new ThemeReviewPage(mode, current, parsed);
            await HostPage.Navigation.PushModalAsync(review);
            var selected = await review.Result;
            if (selected == null || selected.Count == 0) return;
            int count = await SaveColorsAsync(mode, selected.ToDictionary(c => c.Definition.Name, c => c.NewHex));
            await LoadAsync();
            _status.Text = $"Applied {count} {ThemeImportExport.ModeName(mode)} colors.";
        }
        catch (Exception ex)
        {
            await LoadAsync();
            await ShowFailureAsync("Could not import colors", ex);
        }
    }

    private async Task ShowFailureAsync(string title, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);
        _status.Text = exception.Message;
        await HostPage.DisplayAlert(title, exception.Message, "OK");
    }

    private async Task ResetAsync(bool allModes)
    {
        var mode = SelectedMode;
        await _writes.WaitAsync();
        try
        {
            await _settings.DeleteByPrefixAsync(_auth.CurrentUsername, allModes ? "theme_" : ModeTheme.StoragePrefix(mode));
            _privacy.NotifyThemeChanged(_auth.CurrentUsername);
        }
        catch (Exception ex) { _status.Text = ex.Message; return; }
        finally { _writes.Release(); }
        await LoadAsync();
        _status.Text = allModes ? "All modes reset to defaults." : "Mode reset to defaults.";
    }

    private void UpdatePreview()
    {
        foreach (var token in _colors) _preview.Resources["Preview" + token.Key] = token.Value;
    }

    private void BuildPreview()
    {
        Label Text(string value, string token, int size)
        {
            var label = new Label { Text = value, FontSize = size };
            label.SetDynamicResource(Label.TextColorProperty, "Preview" + token);
            return label;
        }
        _preview.SetDynamicResource(BackgroundColorProperty, "PreviewPageBackground");
        var card = new Frame
        {
            Padding = 10, HasShadow = false,
            Content = new VerticalStackLayout { Children = { Text("Card primary text", "CardText1", 15), Text("Card secondary text", "CardText2", 13) } }
        };
        card.SetDynamicResource(BackgroundColorProperty, "PreviewCardBackground");
        var input = new Entry { Text = "Example input", IsReadOnly = true };
        input.SetDynamicResource(BackgroundColorProperty, "PreviewInputBackground");
        input.SetDynamicResource(Entry.TextColorProperty, "PreviewInputText");
        var button = new Button { Text = "Primary button" };
        button.SetDynamicResource(BackgroundColorProperty, "PreviewButtonPrimaryBackground");
        button.SetDynamicResource(Button.TextColorProperty, "PreviewButtonPrimaryText");
        _preview.Content = new VerticalStackLayout
        {
            Spacing = 6,
            Children = { Text("Primary text", "Text1", 18), Text("Secondary text", "Text2", 14), Text("Hint text", "Text3", 11), Text("Accent", "Accent", 14), card, input, button }
        };
    }
}
