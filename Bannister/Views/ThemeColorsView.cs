using Bannister.Services;

namespace Bannister.Views;

/// <summary>Per-user mode palette editor hosted by Settings.</summary>
internal sealed class ThemeColorsView : ContentView
{
    private readonly AuthService _auth;
    private readonly AppSettingsService _settings;
    private readonly PrivacyModeService _privacy;
    private readonly Picker _mode = new() { ItemsSource = new[] { "Private", "Public", "All" } };
    private readonly VerticalStackLayout _rows = new() { Spacing = 10 };
    private readonly Frame _preview = new() { Padding = 12, HasShadow = false };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly Dictionary<string, Color> _colors = new();
    private int _loadVersion;
    private readonly HashSet<string> _collapsed = new(new[] { "Cards", "Inputs", "Rows", "Status", "Buttons", "Highlights", "Brand" });

    private ActivityDisplayMode SelectedMode => _mode.SelectedIndex switch
    {
        0 => ActivityDisplayMode.PrivateOnly,
        1 => ActivityDisplayMode.PublicOnly,
        _ => ActivityDisplayMode.All
    };

    public ThemeColorsView(AuthService auth, AppSettingsService settings, PrivacyModeService privacy)
    {
        _auth = auth; _settings = settings; _privacy = privacy;
        _mode.SelectedIndexChanged += async (_, _) => await LoadAsync();
        var resetMode = new Button { Text = "Reset this mode to defaults", FontSize = 12 };
        resetMode.Clicked += async (_, _) => await ResetAsync(false);
        var resetAll = new Button { Text = "Reset all modes", FontSize = 12 };
        resetAll.Clicked += async (_, _) => await ResetAsync(true);
        ModeThemedPage.SetPreserveColors(_preview, true);
        BuildPreview();
        Content = new Frame
        {
            BackgroundColor = Colors.White, Padding = 20, CornerRadius = 12,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Colors", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Choose a mode, then enter #RRGGBB (or #AARRGGBB for opacity) or tap a preset. Changes save immediately.", FontSize = 12 },
                    _mode, _status, _preview, _rows,
                    new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { resetMode, resetAll } }
                }
            }
        };
    }

    public async Task ActivateAsync()
    {
        int index = _privacy.GetDisplayMode(_auth.CurrentUsername) switch
        {
            ActivityDisplayMode.PrivateOnly => 0,
            ActivityDisplayMode.PublicOnly => 1,
            _ => 2
        };
        if (_mode.SelectedIndex == index) await LoadAsync(); else _mode.SelectedIndex = index;
    }

    private async Task LoadAsync()
    {
        int version = ++_loadVersion;
        var mode = SelectedMode;
        try
        {
            await _writes.WaitAsync();
            ModeThemeColors colors;
            try { colors = await ModeTheme.GetAsync(mode, _auth.CurrentUsername, _settings); }
            finally { _writes.Release(); }
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
    }

    private View BuildRow(ThemeColorDefinition definition, Color color, ActivityDisplayMode mode)
    {
        string token = definition.Name;
        var swatch = new BoxView { Color = color, WidthRequest = 26, HeightRequest = 26, VerticalOptions = LayoutOptions.Center };
        ModeThemedPage.SetPreserveColors(swatch, true);
        var entry = new Entry
        {
            Text = ModeTheme.ToHex(color), MaxLength = 9, WidthRequest = 130,
            BackgroundColor = Colors.White, TextColor = Colors.Black,
            IsEnabled = !_settings.IsReadOnly && !definition.Fixed,
            AutomationId = $"Theme_{token}"
        };
        var validation = new Label { FontSize = 11, TextColor = Color.FromArgb("#C62828"), IsVisible = false };
        entry.TextChanged += async (_, args) =>
        {
            if (!ModeTheme.TryNormalizeHex(args.NewTextValue, out string hex))
            {
                validation.Text = "Use #RRGGBB or #AARRGGBB."; validation.IsVisible = true;
                return;
            }
            validation.IsVisible = false;
            await _writes.WaitAsync();
            try
            {
                if (_settings.IsReadOnly) throw new InvalidOperationException("Colors can be changed on the master device.");
                await _settings.SetAsync(_auth.CurrentUsername, ModeTheme.StoragePrefix(mode) + token, hex);
                _privacy.NotifyThemeChanged(_auth.CurrentUsername);
                if (mode == SelectedMode)
                {
                    _colors[token] = Color.FromArgb(hex); swatch.Color = _colors[token]; UpdatePreview();
                }
                _status.Text = "Saved";
            }
            catch (Exception ex) { _status.Text = $"Could not save: {ex.Message}"; }
            finally { _writes.Release(); }
        };
        var palette = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        palette.IsVisible = !definition.Fixed;
        foreach (string hex in new[] { "#FFFFFF", "#FAFAFA", "#222222", "#666666", "#1A1A2E", "#2D0A4E", "#6B73FF", "#1565C0", "#00E5FF", "#FFD700" })
        {
            string choice = hex;
            var preset = new Button
            {
                Text = "", BackgroundColor = Color.FromArgb(choice), WidthRequest = 24, HeightRequest = 24,
                MinimumHeightRequest = 24, MinimumWidthRequest = 24, Margin = 2, Padding = 0,
                CornerRadius = 4, BorderColor = Colors.Gray, BorderWidth = 1, IsEnabled = !_settings.IsReadOnly
            };
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
