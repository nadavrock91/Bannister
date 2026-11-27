using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

internal static class ThemePaletteUI
{
    private static readonly ThemeKey[] StripColors = { ThemeKey.PageBackground, ThemeKey.PageText1, ThemeKey.CardBackground,
        ThemeKey.ButtonPrimaryBackground, ThemeKey.Accent, ThemeKey.SuccessButtonBackground, ThemeKey.ErrorButtonBackground };

    public static Label Text(string text, double size = 14, bool muted = false) =>
        new Label { Text = text, FontSize = size }.ThemeColor(Label.TextColorProperty, muted ? ThemeKey.CardText2 : ThemeKey.CardText1);

    public static View Swatch(string name, string hex, double width = 30)
    {
        var swatch = new BoxView { Color = ModeTheme.ParseStoredHex(hex), WidthRequest = width, HeightRequest = 26 };
        ModeThemedPage.SetPreserveColors(swatch, true);
        SemanticProperties.SetDescription(swatch, name + ": " + hex);
        return swatch;
    }

    public static View Strip(IReadOnlyDictionary<string, string> values)
    {
        var strip = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var key in StripColors)
        {
            string name = ModeTheme.KeyName(key);
            var swatch = Swatch(name, values[name]); swatch.Margin = 2; strip.Children.Add(swatch);
        }
        return strip;
    }

    public static Frame Card(params View[] contents) => new Frame
    {
        Padding = 14, CornerRadius = 10, HasShadow = false,
        Content = new VerticalStackLayout { Spacing = 8, Children = { } }
    }.WithContent(contents);

    private static Frame WithContent(this Frame card, IEnumerable<View> contents)
    {
        foreach (var item in contents) ((VerticalStackLayout)card.Content).Children.Add(item);
        return card.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
    }

    public static string MissingNote(IReadOnlyList<string> missing) => missing.Count == 0 ? "" :
        $"Using current values for {missing.Count} missing or invalid colors: {string.Join(", ", missing)}.";

    public static Button Button(string text) => new Button { Text = text, Margin = 3 };
}

internal abstract class ThemeLibraryPage : ModeThemedPage
{
    protected readonly string Username;
    protected readonly AppSettingsService Settings;
    protected readonly PrivacyModeService Privacy;
    protected ThemeLibraryPage(AuthService auth, AppSettingsService settings, PrivacyModeService privacy)
    { Username = auth.CurrentUsername; Settings = settings; Privacy = privacy; }

    protected async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("UI Colors", ex.Message, "OK");
        }
    }

    protected Task<ModeThemeColors> PaletteAsync(ActivityDisplayMode mode) => ModeTheme.GetAsync(mode, Username, Settings);
}

internal sealed class ThemePaletteHistoryPage : ThemeLibraryPage
{
    private readonly ActivityDisplayMode _mode;
    private readonly VerticalStackLayout _rows = new() { Padding = 20, Spacing = 12 };
    public ThemePaletteHistoryPage(AuthService auth, AppSettingsService settings, PrivacyModeService privacy, ActivityDisplayMode mode)
        : base(auth, settings, privacy)
    {
        _mode = mode; Title = ThemeImportExport.ModeName(mode) + " Color History";
        Content = new ScrollView { Content = _rows };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(async () =>
        {
            var current = ModeTheme.EditableHex(await PaletteAsync(_mode));
            var entries = await Settings.GetThemeHistoryAsync(Username, _mode);
            _rows.Children.Clear();
            _rows.Children.Add(new Label { Text = "Tap an entry to review its differences and restore it.", FontSize = 13 });
            if (entries.Count == 0) _rows.Children.Add(new Label { Text = Settings.IsReadOnly
                ? "No history yet. Sync from the master device to receive palette history." : "No palette history yet." });
            foreach (var entry in entries)
            {
                try
                {
                    var resolved = ModeTheme.ResolvePalette(entry.PaletteJson, current);
                    int different = resolved.Values.Count(c => c.Value != current[c.Key]);
                    var card = ThemePaletteUI.Card(ThemePaletteUI.Text(entry.CreatedAt.ToLocalTime().ToString("g")),
                        ThemePaletteUI.Text(entry.Source), ThemePaletteUI.Strip(resolved.Values),
                        ThemePaletteUI.Text($"{different} colors differ from the current palette", 12, true));
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += async (_, _) => await RunAsync(async () => await Navigation.PushAsync(new ThemePaletteReviewPage(
                        Username, Settings, Privacy, "Restore " + ThemeImportExport.ModeName(_mode) + " colors", $"{entry.CreatedAt.ToLocalTime():g} · {entry.Source}",
                        new[] { new ThemePaletteReview(_mode, current, resolved) }, "Restore",
                        async () => new[] { await Settings.RestoreThemeHistoryAsync(Username, entry.Id, entry.PaletteJson) }, entry.Note)));
                    card.GestureRecognizers.Add(tap); _rows.Children.Add(card);
                }
                catch (Exception ex) { _rows.Children.Add(ThemePaletteUI.Card(ThemePaletteUI.Text($"{entry.CreatedAt.ToLocalTime():g} · {entry.Source}"), ThemePaletteUI.Text(ex.Message))); }
            }
        });
    }
}

internal sealed record ThemePaletteReview(ActivityDisplayMode Mode, IReadOnlyDictionary<string, string> Current, ThemePaletteResolution Saved);

internal sealed class ThemePaletteReviewPage : ModeThemedPage
{
    public ThemePaletteReviewPage(string username, AppSettingsService settings, PrivacyModeService privacy, string title, string source,
        IEnumerable<ThemePaletteReview> palettes, string action, Func<Task<IReadOnlyList<ThemePaletteSaveResult>>> save, string? note = null)
    {
        Title = title;
        var rows = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        rows.Children.Add(new Label { Text = source, FontSize = 20, FontAttributes = FontAttributes.Bold });
        if (!string.IsNullOrWhiteSpace(note)) rows.Children.Add(new Label { Text = note, FontSize = 13 });
        foreach (var palette in palettes)
        {
            rows.Children.Add(new Label { Text = ThemeImportExport.ModeName(palette.Mode), FontSize = 18, FontAttributes = FontAttributes.Bold });
            if (palette.Saved.MissingColors.Count > 0) rows.Children.Add(new Label { Text = ThemePaletteUI.MissingNote(palette.Saved.MissingColors), FontSize = 13 });
            var changes = palette.Saved.Values.Where(c => palette.Current[c.Key] != c.Value).ToList();
            if (changes.Count == 0) rows.Children.Add(new Label { Text = "No colors differ from the current palette." });
            foreach (var (name, hex) in changes)
            {
                string before = palette.Current[name];
                var values = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
                foreach (var item in new[] { ("Current", before), ("Saved", hex) })
                    values.Children.Add(new VerticalStackLayout { Margin = 5, Spacing = 3,
                        Children = { ThemePaletteUI.Text(item.Item1, 12, true), ThemePaletteUI.Swatch(name, item.Item2, 50), ThemePaletteUI.Text(item.Item2) } });
                rows.Children.Add(ThemePaletteUI.Card(ThemePaletteUI.Text(name), values));
            }
        }
        var apply = ThemePaletteUI.Button(action); apply.IsEnabled = !settings.IsReadOnly;
        bool saving = false;
        apply.Clicked += async (_, _) =>
        {
            if (saving) return;
            saving = true; apply.IsEnabled = false;
            try
            {
                var results = await save();
                privacy.NotifyThemeChanged(username);
                await DisplayAlert("UI Colors", string.Join("\n", results.Select(r =>
                    $"{ThemeImportExport.ModeName(r.Mode)}: {r.ChangedCount} colors changed." +
                    (r.MissingColors.Count > 0 ? " " + ThemePaletteUI.MissingNote(r.MissingColors) : ""))), "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); await DisplayAlert("Could not apply palette", ex.Message, "OK"); }
            finally { saving = false; apply.IsEnabled = !settings.IsReadOnly; }
        };
        var cancel = ThemePaletteUI.Button("Cancel"); cancel.Clicked += async (_, _) => { if (!saving) await Navigation.PopAsync(); };
        if (settings.IsReadOnly) rows.Children.Add(new Label { Text = "Palettes can be changed on the master device." });
        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        layout.Add(new ScrollView { Content = rows });
        layout.Add(new FlexLayout { Margin = 12, Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { apply, cancel } }, 0, 1);
        Content = layout;
    }
}

internal sealed class ThemeTargetModesPage : ThemeResultPage<IReadOnlyList<ActivityDisplayMode>?>
{
    public ThemeTargetModesPage(ActivityDisplayMode selected) : base(null)
    {
        Title = "Apply to palettes";
        var modes = new Dictionary<ActivityDisplayMode, CheckBox>();
        var rows = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        rows.Children.Add(ThemePaletteUI.Text("Choose one or several palettes. This does not change the app display mode."));
        var review = ThemePaletteUI.Button("Review selected palettes");
        foreach (var mode in new[] { ActivityDisplayMode.PrivateOnly, ActivityDisplayMode.PublicOnly, ActivityDisplayMode.All })
        {
            var check = new CheckBox { IsChecked = mode == selected }; modes[mode] = check;
            check.CheckedChanged += (_, _) => review.IsEnabled = modes.Values.Any(c => c.IsChecked);
            rows.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { check, ThemePaletteUI.Text(ThemeImportExport.ModeName(mode)) } });
        }
        review.Clicked += async (_, _) => await CloseAsync(modes.Where(m => m.Value.IsChecked).Select(m => m.Key).ToArray());
        var cancel = ThemePaletteUI.Button("Cancel"); cancel.Clicked += async (_, _) => await CloseAsync(null);
        rows.Children.Add(review); rows.Children.Add(cancel); Content = rows;
    }
}

internal sealed class ThemeTemplatesPage : ThemeLibraryPage
{
    private readonly ActivityDisplayMode _mode;
    private readonly string? _styleRequest;
    private readonly VerticalStackLayout _rows = new() { Padding = 20, Spacing = 12 };
    public ThemeTemplatesPage(AuthService auth, AppSettingsService settings, PrivacyModeService privacy, ActivityDisplayMode mode, string? styleRequest)
        : base(auth, settings, privacy)
    {
        _mode = mode; _styleRequest = styleRequest; Title = "Color Templates";
        Content = new ScrollView { Content = _rows };
    }

    protected override async void OnAppearing()
    { base.OnAppearing(); await RunAsync(LoadAsync); }

    private async Task LoadAsync()
    {
        var current = ModeTheme.EditableHex(await PaletteAsync(_mode));
        var templates = await Settings.GetThemeTemplatesAsync(Username);
        _rows.Children.Clear();
        foreach (var template in templates)
        {
            try
            {
                var values = ModeTheme.ResolvePalette(template.PaletteJson, current).Values;
                var apply = ThemePaletteUI.Button("Apply to…"); apply.IsEnabled = !Settings.IsReadOnly;
                apply.Clicked += async (_, _) => await RunAsync(async () =>
                {
                    var targets = new ThemeTargetModesPage(_mode);
                    await Navigation.PushModalAsync(targets);
                    var modes = await targets.Result;
                    if (modes == null || modes.Count == 0) return;
                    var previews = new List<ThemePaletteReview>();
                    foreach (var mode in modes)
                    {
                        var palette = ModeTheme.EditableHex(await PaletteAsync(mode));
                        previews.Add(new(mode, palette, ModeTheme.ResolvePalette(template.PaletteJson, palette)));
                    }
                    await Navigation.PushAsync(new ThemePaletteReviewPage(Username, Settings, Privacy, "Apply color template", template.DisplayName,
                        previews, "Apply Selected", () => Settings.ApplyThemeTemplateAsync(Username, template.Id, modes, template.PaletteJson), template.Note));
                });
                var export = ThemePaletteUI.Button("Export for LLM");
                export.Clicked += async (_, _) => await RunAsync(async () =>
                {
                    var exportMode = template.Id switch { -1 => ActivityDisplayMode.PrivateOnly, -2 => ActivityDisplayMode.PublicOnly, -3 => ActivityDisplayMode.All, _ => _mode };
                    var palette = await PaletteAsync(exportMode);
                    var resolved = ModeTheme.ResolvePalette(template.PaletteJson, ModeTheme.EditableHex(palette));
                    if (resolved.MissingColors.Count > 0) await DisplayAlert("Missing colors", ThemePaletteUI.MissingNote(resolved.MissingColors), "OK");
                    var tokens = palette.Tokens.ToDictionary(c => c.Key, c => c.Value);
                    foreach (var (name, hex) in resolved.Values) tokens[name] = ModeTheme.ParseStoredHex(hex);
                    await Clipboard.SetTextAsync(ThemeImportExport.BuildPrompt(exportMode, tokens, _styleRequest));
                    await DisplayAlert("Copied", $"Copied the palette prompt for '{template.DisplayName}'.", "OK");
                });
                var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { apply, export } };
                if (!template.IsBuiltIn)
                {
                    var rename = ThemePaletteUI.Button("Rename"); rename.IsEnabled = !Settings.IsReadOnly;
                    rename.Clicked += async (_, _) => await RunAsync(async () =>
                    {
                        string? name = await DisplayPromptAsync("Rename template", "Template name", "Save", "Cancel", initialValue: template.Name);
                        if (string.IsNullOrWhiteSpace(name)) return;
                        await Settings.RenameThemeTemplateAsync(Username, template.Id, name); await LoadAsync();
                    });
                    var delete = ThemePaletteUI.Button("Delete"); delete.IsEnabled = !Settings.IsReadOnly;
                    delete.Clicked += async (_, _) => await RunAsync(async () =>
                    {
                        if (!await DisplayAlert("Delete template?", $"Delete '{template.Name}'?", "Delete", "Cancel")) return;
                        await Settings.DeleteThemeTemplateAsync(Username, template.Id); await LoadAsync();
                    });
                    actions.Children.Add(rename); actions.Children.Add(delete);
                }
                _rows.Children.Add(ThemePaletteUI.Card(ThemePaletteUI.Text(template.DisplayName, 18),
                    ThemePaletteUI.Text(template.IsBuiltIn ? "Built-in defaults" : $"Updated {template.UpdatedAt.ToLocalTime():g}", 12, true),
                    ThemePaletteUI.Strip(values), actions));
            }
            catch (Exception ex) { _rows.Children.Add(ThemePaletteUI.Card(ThemePaletteUI.Text(template.DisplayName), ThemePaletteUI.Text(ex.Message))); }
        }
    }
}
