using System.Text.RegularExpressions;

namespace Bannister.Services;

public sealed record ModeThemeColors(
    Color PageBackground, Color Text1, Color Text2, Color Text3, Color Accent,
    Color CardBackground, Color CardText1, Color CardText2,
    Color InputBackground, Color InputText,
    Color ButtonPrimaryBackground, Color ButtonPrimaryText)
{
    public IReadOnlyDictionary<string, Color> ExtraTokens { get; init; } = new Dictionary<string, Color>();
    public Color Background => PageBackground;
    public Color Foreground => Text1;
    public Color MutedForeground => Text2;

    public IReadOnlyDictionary<string, Color> Tokens
    {
        get
        {
            var tokens = new Dictionary<string, Color>
            {
                [nameof(PageBackground)] = PageBackground, [nameof(ThemeKey.PageText1)] = Text1,
                [nameof(ThemeKey.PageText2)] = Text2, [nameof(ThemeKey.PageText3)] = Text3, [nameof(Accent)] = Accent,
                [nameof(CardBackground)] = CardBackground, [nameof(CardText1)] = CardText1,
                [nameof(CardText2)] = CardText2, [nameof(InputBackground)] = InputBackground,
                [nameof(InputText)] = InputText, [nameof(ButtonPrimaryBackground)] = ButtonPrimaryBackground,
                [nameof(ButtonPrimaryText)] = ButtonPrimaryText
            };
            foreach (var token in ExtraTokens) tokens[token.Key] = token.Value;
            return tokens;
        }
    }
}

public static class ModeTheme
{
    // UI: new Label { Text = "..." }.ThemeColor(Label.TextColorProperty, ThemeKey.CardText2).
    // The optional pageKey handles builders shared by page canvases and cards. Bindings update live.
    // Drawing/computed/API colors: AppColors.Get(ThemeKey.X); string-only APIs: AppColors.Hex(ThemeKey.X).
    // Color literals belong only in this catalog; Transparent remains a structural non-color.
    public static string KeyName(ThemeKey key) => key.ToString();

    public static IReadOnlyList<ThemeColorDefinition> Catalog { get; } = new ThemeColorDefinition[]
    {
        new(ThemeKey.PageBackground, "Page", "#6B73FF", false, "The canvas behind cards and controls."),
        new(ThemeKey.PageText1, "Page", "#FFFFFF", false, "Headings and main text directly on the page background."),
        new(ThemeKey.PageText2, "Page", "#101026", false, "Secondary text directly on the page background."),
        new(ThemeKey.PageText3, "Page", "#101026", false, "Small hints directly on the page background."),
        new(ThemeKey.Accent, "Page", "#FFFFFF", false, "Navigation accents and page emphasis."),
        new(ThemeKey.CardBackground, "Cards", "#FFFFFF", false, "Plain cards and normal table rows."),
        new(ThemeKey.CardText1, "Cards", "#333333", false, "Main text inside cards and table cells."),
        new(ThemeKey.CardText2, "Cards", "#666666", false, "Secondary text inside cards."),
        new(ThemeKey.CardText3, "Cards", "#999999", false, "Hints, disabled text, and muted details."),
        new(ThemeKey.SecondarySurface, "Cards", "#F5F5F5", false, "Subtle neutral panels and inset surfaces."),
        new(ThemeKey.Separator, "Cards", "#E0E0E0", false, "Dividers, table grid lines, and light borders."),
        new(ThemeKey.Overlay, "Cards", "#80000000", false, "Translucent scrims and shadows; keep its alpha channel."),
        new(ThemeKey.InputBackground, "Inputs", "#FAFAFA", false, "Background of text boxes, pickers, and other inputs."),
        new(ThemeKey.InputText, "Inputs", "#222222", false, "Text inside inputs."),
        new(ThemeKey.RowAltBackground, "Rows", "#F5F5F5", false, "Alternating table rows."),
        new(ThemeKey.RowSelected, "Rows", "#E3F2FD", false, "Selected rows and cells."),
        new(ThemeKey.ButtonPrimaryBackground, "Buttons", "#1565C0", false, "Main action buttons and selected primary controls."),
        new(ThemeKey.ButtonPrimaryText, "Buttons", "#FFFFFF", false, "Text on primary and other solid-color buttons."),
        new(ThemeKey.SecondaryButtonBackground, "Buttons", "#5B63EE", false, "Secondary action buttons."),
        new(ThemeKey.NeutralButtonBackground, "Buttons", "#ECEFF1", false, "Quiet neutral buttons and unselected controls."),
        new(ThemeKey.NeutralButtonText, "Buttons", "#37474F", false, "Text on light neutral buttons."),
        new(ThemeKey.DisabledButtonBackground, "Buttons", "#9E9E9E", false, "Unavailable buttons and controls."),
        new(ThemeKey.NeutralDarkButton, "Buttons", "#455A64", false, "Dark neutral actions, editor panels, and strong borders."),
        new(ThemeKey.SuccessText, "Status", "#2E7D32", false, "Success messages, green emphasis, and dark success surfaces."),
        new(ThemeKey.SuccessBackground, "Status", "#E8F5E9", false, "Pale success badges and panels."),
        new(ThemeKey.SuccessButtonBackground, "Status", "#4CAF50", false, "Success actions and green indicators."),
        new(ThemeKey.WarningText, "Status", "#E65100", false, "Warning messages and amber emphasis."),
        new(ThemeKey.WarningBackground, "Status", "#FFF3E0", false, "Pale warning badges and panels."),
        new(ThemeKey.WarningButtonBackground, "Status", "#FF9800", false, "Warning actions and amber indicators."),
        new(ThemeKey.ErrorText, "Status", "#C62828", false, "Error messages, red emphasis, and dark error surfaces."),
        new(ThemeKey.ErrorBackground, "Status", "#FFEBEE", false, "Pale error badges and panels."),
        new(ThemeKey.ErrorButtonBackground, "Status", "#F44336", false, "Destructive actions and red indicators."),
        new(ThemeKey.InfoText, "Status", "#1565C0", false, "Information messages and blue links."),
        new(ThemeKey.InfoBackground, "Status", "#E3F2FD", false, "Pale information badges and panels."),
        new(ThemeKey.InfoButtonBackground, "Status", "#2196F3", false, "Information actions and blue indicators."),
        new(ThemeKey.HighlightYellow, "Highlights", "#FFF8E1", false, "Warm yellow highlights and warm neutral panels."),
        new(ThemeKey.HighlightYellowText, "Highlights", "#F57F17", false, "Text and emphasis on yellow highlights."),
        new(ThemeKey.HighlightIndigo, "Highlights", "#E8EAF6", false, "Indigo highlights, chart fills, and selected indigo controls."),
        new(ThemeKey.HighlightIndigoText, "Highlights", "#283593", false, "Dark text and solid actions in the indigo family."),
        new(ThemeKey.HighlightTeal, "Highlights", "#E0F7FA", false, "Teal and mint highlights."),
        new(ThemeKey.HighlightTealText, "Highlights", "#00838F", false, "Dark text and solid actions in the teal family."),
        new(ThemeKey.HighlightPink, "Highlights", "#FCE4EC", false, "Pink highlights."),
        new(ThemeKey.HighlightPinkText, "Highlights", "#C2185B", false, "Dark text and solid actions in the pink family."),
        new(ThemeKey.HighlightViolet, "Highlights", "#EDE7F6", false, "Violet and lavender highlights."),
        new(ThemeKey.HighlightVioletText, "Highlights", "#4A148C", false, "Dark text and solid actions in the violet family."),
        new(ThemeKey.TimelinePrimary, "Timeline", "#D2A8FF", false, "Primary lavender timeline indicators."),
        new(ThemeKey.TimelineSecondary, "Timeline", "#63E6BE", false, "Secondary mint timeline indicators."),
        new(ThemeKey.TimelineAccent, "Timeline", "#F78166", false, "Salmon timeline indicators."),
        new(ThemeKey.MediaBackground, "Media", "#1E1E1E", false, "Dark editor and timeline canvases."),
        new(ThemeKey.MediaText, "Media", "#D4D4D4", false, "Main text on dark editor and timeline canvases."),
        new(ThemeKey.BrandFacebook, "Brand", "#1877F2", true, "Facebook branding (fixed)."),
        new(ThemeKey.BrandTikTok, "Brand", "#FE2C55", true, "TikTok branding (fixed)."),
        new(ThemeKey.BrandYouTube, "Brand", "#FF0000", true, "YouTube branding (fixed)."),
    };
    public static ModeThemeColors Get(ActivityDisplayMode mode)
    {
        var (background, secondary, accent) = mode switch
        {
            ActivityDisplayMode.PublicOnly => ("#1A1A2E", "#D7D9E8", "#00E5FF"),
            ActivityDisplayMode.PrivateOnly => ("#2D0A4E", "#E6D9F2", "#FFD700"),
            _ => ("#6B73FF", "#101026", "#FFFFFF")
        };
        return new ModeThemeColors(Color.FromArgb(background), Colors.White, Color.FromArgb(secondary), Color.FromArgb(secondary),
            Color.FromArgb(accent), Colors.White, Color.FromArgb("#333333"), Color.FromArgb("#666666"),
            Color.FromArgb("#FAFAFA"), Color.FromArgb("#222222"), Color.FromArgb("#1565C0"), Colors.White)
        {
            ExtraTokens = Catalog.Where(x => !LegacyKeys.Contains(x.Name)).ToDictionary(x => x.Name, x => Color.FromArgb(x.Hex))
        };
    }

    private static readonly HashSet<string> LegacyKeys = new(new[] { "PageBackground", "PageText1", "PageText2", "PageText3", "Accent", "CardBackground", "CardText1", "CardText2", "InputBackground", "InputText", "ButtonPrimaryBackground", "ButtonPrimaryText" });

    public static string StoragePrefix(ActivityDisplayMode mode) => "theme_" + (mode switch
    {
        ActivityDisplayMode.PrivateOnly => "private",
        ActivityDisplayMode.PublicOnly => "public",
        _ => "all"
    }) + "_";

    public static bool TryNormalizeHex(string? text, out string hex)
    {
        hex = text?.Trim().ToUpperInvariant() ?? "";
        return Regex.IsMatch(hex, "^#(?:[0-9A-F]{6}|[0-9A-F]{8})$");
    }

    public static string ToHex(Color color) => "#" + (color.Alpha < 1 ? $"{(int)Math.Round(color.Alpha * 255):X2}" : "") +
        $"{(int)Math.Round(color.Red * 255):X2}{(int)Math.Round(color.Green * 255):X2}{(int)Math.Round(color.Blue * 255):X2}";

    // User-entered or persisted colors are data, rather than app color defaults.
    internal static Color ParseStoredHex(string hex) => Color.FromArgb(hex); // theme-exempt: runtime hex data parsing.

    public static IEnumerable<Color> PresetDefaults()
    {
        var all = Get(ActivityDisplayMode.All);
        var publicMode = Get(ActivityDisplayMode.PublicOnly);
        var privateMode = Get(ActivityDisplayMode.PrivateOnly);
        return new[] { all.CardBackground, all.InputBackground, all.InputText, all.CardText2,
            publicMode.Background, privateMode.Background, all.Background,
            all.ButtonPrimaryBackground, publicMode.Accent, privateMode.Accent };
    }

    public static async Task<ModeThemeColors> GetAsync(ActivityDisplayMode mode, string username, AppSettingsService settings)
    {
        var defaults = Get(mode);
        var prefix = StoragePrefix(mode);
        await settings.MigrateThemeOverridesAsync(username);
        var stored = ThemePaletteMigration.Plan(await settings.GetByPrefixAsync(username, prefix)).Values;
        Color Read(string token) => stored.TryGetValue(prefix + token, out var value) && TryNormalizeHex(value, out var hex)
            ? ParseStoredHex(hex) : defaults.Tokens[token];
        var colors = new ModeThemeColors(Read("PageBackground"), Read("PageText1"), Read("PageText2"), Read("PageText3"), Read("Accent"),
            Read("CardBackground"), Read("CardText1"), Read("CardText2"), Read("InputBackground"), Read("InputText"),
            Read("ButtonPrimaryBackground"), Read("ButtonPrimaryText"))
        {
            ExtraTokens = Catalog.Where(x => !LegacyKeys.Contains(x.Name))
                .ToDictionary(x => x.Name, x => x.Fixed ? defaults.Tokens[x.Name] : Read(x.Name))
        };
        AppColors.Remember(username, mode, colors);
        return colors;
    }

    public static Color GetBackground(ActivityDisplayMode mode) => Get(mode).Background;
}
