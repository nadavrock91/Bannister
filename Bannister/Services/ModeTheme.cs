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
                [nameof(PageBackground)] = PageBackground, [nameof(Text1)] = Text1,
                [nameof(Text2)] = Text2, [nameof(Text3)] = Text3, [nameof(Accent)] = Accent,
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
    public static string KeyName(ThemeKey key) => key switch
    {
        ThemeKey.Text1 => "Text1", ThemeKey.Text2 => "Text2", ThemeKey.Text3 => "Text3", _ => key.ToString()
    };

    public static IReadOnlyList<ThemeColorDefinition> Catalog { get; } = new ThemeColorDefinition[]
    {
        new(ThemeKey.PageBackground,"Page","#6B73FF"), new(ThemeKey.Text1,"Page","#FFFFFF"),
        new(ThemeKey.Text2,"Page","#101026"), new(ThemeKey.Text3,"Page","#101026"), new(ThemeKey.Accent,"Page","#FFFFFF"),
        new(ThemeKey.DisabledText,"Page","#999999"),
        new(ThemeKey.CardBackground,"Cards","#FFFFFF"), new(ThemeKey.CardText1,"Cards","#333333"),
        new(ThemeKey.CardText2,"Cards","#666666"), new(ThemeKey.CardText3,"Cards","#999999"),
        new(ThemeKey.CardTextStrong,"Cards","#222222"), new(ThemeKey.SecondarySurface,"Cards","#F5F5F5"),
        new(ThemeKey.Overlay,"Cards","#80000000"), new(ThemeKey.OverlayStrong,"Cards","#CC000000"), new(ThemeKey.OverlayMedium,"Cards","#90000000"),
        new(ThemeKey.Shadow,"Cards","#000000"),
        new(ThemeKey.InputBackground,"Inputs","#FAFAFA"), new(ThemeKey.InputText,"Inputs","#222222"),
        new(ThemeKey.RowBackground,"Rows","#FFFFFF"), new(ThemeKey.RowAltBackground,"Rows","#F5F5F5"),
        new(ThemeKey.RowSelected,"Rows","#E3F2FD"), new(ThemeKey.Separator,"Rows","#E0E0E0"),
        new(ThemeKey.SuccessText,"Status","#2E7D32"), new(ThemeKey.SuccessBackground,"Status","#E8F5E9"),
        new(ThemeKey.SuccessButtonBackground,"Status","#4CAF50"), new(ThemeKey.SuccessBrightText,"Status","#388E3C"), new(ThemeKey.SuccessStrongText,"Status","#1B5E20"),
        new(ThemeKey.SuccessDarkButtonBackground,"Status","#2E7D32"),
        new(ThemeKey.WarningText,"Status","#E65100"), new(ThemeKey.WarningBackground,"Status","#FFF3E0"),
        new(ThemeKey.WarningButtonBackground,"Status","#FF9800"), new(ThemeKey.WarningStrongText,"Status","#F57C00"),
        new(ThemeKey.WarningDarkButtonBackground,"Status","#E65100"),
        new(ThemeKey.ErrorText,"Status","#C62828"), new(ThemeKey.ErrorBackground,"Status","#FFEBEE"),
        new(ThemeKey.ErrorButtonBackground,"Status","#F44336"), new(ThemeKey.ErrorBrightText,"Status","#EF5350"), new(ThemeKey.ErrorStrongText,"Status","#D32F2F"),
        new(ThemeKey.ErrorDarkButtonBackground,"Status","#C62828"),
        new(ThemeKey.InfoText,"Status","#1565C0"), new(ThemeKey.InfoBackground,"Status","#E3F2FD"),
        new(ThemeKey.InfoButtonBackground,"Status","#2196F3"), new(ThemeKey.InfoStrongText,"Status","#1976D2"), new(ThemeKey.InfoBrightText,"Status","#42A5F5"),
        new(ThemeKey.ButtonPrimaryBackground,"Buttons","#1565C0"), new(ThemeKey.ButtonPrimaryText,"Buttons","#FFFFFF"),
        new(ThemeKey.SecondaryButtonBackground,"Buttons","#5B63EE"), new(ThemeKey.SecondaryButtonText,"Buttons","#FFFFFF"),
        new(ThemeKey.NeutralButtonBackground,"Buttons","#ECEFF1"), new(ThemeKey.NeutralButtonText,"Buttons","#37474F"),
        new(ThemeKey.DisabledButtonBackground,"Buttons","#9E9E9E"), new(ThemeKey.OnColorText,"Buttons","#FFFFFF"),
        new(ThemeKey.AccentPurpleText,"Buttons","#7B1FA2"), new(ThemeKey.AccentPurpleStrongText,"Buttons","#6A1B9A"),
        new(ThemeKey.AccentPurpleBackground,"Buttons","#F3E5F5"), new(ThemeKey.AccentPurpleButtonBackground,"Buttons","#9C27B0"),
        new(ThemeKey.AccentPurpleLightButtonBackground,"Buttons","#AB47BC"), new(ThemeKey.AccentPurpleBorder,"Buttons","#CE93D8"), new(ThemeKey.Link,"Buttons","#1565C0"),
        new(ThemeKey.HighlightYellow,"Highlights","#FFF8E1"), new(ThemeKey.HighlightYellowText,"Highlights","#F57F17"),
        new(ThemeKey.HighlightYellowStrong,"Highlights","#FFF9C4"), new(ThemeKey.HighlightYellowSoft,"Highlights","#FFFDE7"),
        new(ThemeKey.HighlightIndigo,"Highlights","#E8EAF6"), new(ThemeKey.HighlightIndigoText,"Highlights","#283593"),
        new(ThemeKey.HighlightIndigoStrongText,"Highlights","#3F51B5"), new(ThemeKey.HighlightIndigoButton,"Highlights","#3949AB"),
        new(ThemeKey.HighlightTeal,"Highlights","#E0F7FA"), new(ThemeKey.HighlightTealText,"Highlights","#00838F"),
        new(ThemeKey.HighlightPink,"Highlights","#FCE4EC"), new(ThemeKey.HighlightPinkText,"Highlights","#C2185B"),
        new(ThemeKey.HighlightOrange,"Highlights","#FFE0B2"), new(ThemeKey.HighlightLime,"Highlights","#F1F8E9"), new(ThemeKey.HighlightLimeText,"Highlights","#33691E"),
        new(ThemeKey.HighlightViolet,"Highlights","#EDE7F6"), new(ThemeKey.HighlightVioletText,"Highlights","#4A148C"),
        new(ThemeKey.HighlightSky,"Highlights","#E1F5FE"), new(ThemeKey.HighlightSkyText,"Highlights","#01579B"),
        new(ThemeKey.TimelineSuccess,"Status","#27AE60"), new(ThemeKey.TimelineError,"Status","#E74C3C"),
        new(ThemeKey.TimelineInfo,"Status","#3498DB"), new(ThemeKey.TimelineWarning,"Status","#F39C12"),
        new(ThemeKey.NeutralDarkButton,"Buttons","#455A64"), new(ThemeKey.NeutralMutedButton,"Buttons","#607D8B"),
        new(ThemeKey.WarningGoldenButton,"Status","#F9A825"), new(ThemeKey.HighlightYellowButton,"Highlights","#FFB300"),
        new(ThemeKey.HighlightSkyButton,"Highlights","#0277BD"), new(ThemeKey.InfoLightText,"Status","#90CAF9"), new(ThemeKey.ErrorSelectedBackground,"Status","#FFCDD2"),
        new(ThemeKey.InfoDeepText,"Status","#0D47A1"), new(ThemeKey.NeutralHoverButton,"Buttons","#757575"),
        new(ThemeKey.WarningSoftText,"Status","#FFA726"),
        new(ThemeKey.SecondaryAccentText,"Buttons","#5B63EE"), new(ThemeKey.SuccessLightText,"Status","#4CAF50"),
        new(ThemeKey.InfoLinkText,"Status","#2196F3"), new(ThemeKey.SlateText,"Cards","#455A64"),
        new(ThemeKey.HighlightSkyAccentText,"Highlights","#0277BD"), new(ThemeKey.AccentPurpleSurface,"Buttons","#7B1FA2"),
        new(ThemeKey.AccentPurpleDarkButtonBackground,"Buttons","#6A1B9A"), new(ThemeKey.ErrorStrongButtonBackground,"Status","#D32F2F"),
        new(ThemeKey.HighlightYellowDarkButton,"Highlights","#F57F17"), new(ThemeKey.SuccessHoverButtonBackground,"Status","#388E3C"),
        new(ThemeKey.WarningHoverButtonBackground,"Status","#F57C00"), new(ThemeKey.InfoHoverButtonBackground,"Status","#1976D2"),
        new(ThemeKey.WarningAmber,"Status","#EF6C00"),
        new(ThemeKey.HighlightBlueStrong,"Highlights","#BBDEFB"),
        new(ThemeKey.HighlightMint,"Highlights","#E0F2F1"),
        new(ThemeKey.HighlightMintText,"Highlights","#00695C"),
        new(ThemeKey.TealButtonBackground,"Buttons","#00897B"),
        new(ThemeKey.BrownButtonBackground,"Buttons","#5D4037"),
        new(ThemeKey.BrownLightButtonBackground,"Buttons","#795548"),
        new(ThemeKey.WarningAmberButtonBackground,"Status","#FF8F00"),
        new(ThemeKey.SecondaryLavenderSurface,"Cards","#F8F9FF"),
        new(ThemeKey.WarningLightButtonBackground,"Status","#FFB74D"),
        new(ThemeKey.InfoPaleText,"Status","#64B5F6"),
        new(ThemeKey.DisabledBlueGrey,"Buttons","#90A4AE"),
        new(ThemeKey.AccentPurpleSelected,"Buttons","#E1BEE7"),
        new(ThemeKey.AccentVioletButton,"Buttons","#5E35B1"),
        new(ThemeKey.HighlightGoldButton,"Highlights","#FBC02D"),
        new(ThemeKey.InfoSkyButton,"Buttons","#29B6F6"),
        new(ThemeKey.HighlightPinkStrong,"Highlights","#F8BBD0"),
        new(ThemeKey.HighlightTealStrongText,"Highlights","#006064"),
        new(ThemeKey.AccentVioletDeep,"Buttons","#512DA8"),
        new(ThemeKey.WarningBurntText,"Status","#BF360C"),
        new(ThemeKey.HighlightEmeraldText,"Highlights","#065F46"),
        new(ThemeKey.SuccessSelectedBackground,"Status","#A5D6A7"),
        new(ThemeKey.HighlightIndigoSelected,"Highlights","#DDE1FF"),
        new(ThemeKey.AccentVioletStrongText,"Buttons","#4527A0"),
        new(ThemeKey.HighlightSkyStrong,"Highlights","#B3E5FC"),
        new(ThemeKey.HighlightEmerald,"Highlights","#D1FAE5"),
        new(ThemeKey.TranslucentWhiteSurface,"Cards","#D2FFFFFF"),
        new(ThemeKey.OverlayLight,"Cards","#73000000"),
        new(ThemeKey.OverlayDark,"Cards","#99000000"),
        new(ThemeKey.HighlightIndigoDeepText,"Highlights","#303F9F"),
        new(ThemeKey.PinkButtonBackground,"Buttons","#E91E63"),
        new(ThemeKey.TimelinePurple,"Status","#9B59B6"),
        new(ThemeKey.DeepOrangeButtonBackground,"Buttons","#FF5722"),
        new(ThemeKey.LimeButtonBackground,"Buttons","#8BC34A"),
        new(ThemeKey.HighlightAmberButton,"Highlights","#FFC107"),
        new(ThemeKey.ErrorPureText,"Status","#FF0000"),
        new(ThemeKey.ErrorDeepText,"Status","#B71C1C"),
        new(ThemeKey.HighlightIndigoStrong,"Highlights","#C5CAE9"),
        new(ThemeKey.BrownText,"Cards","#5D4037"), new(ThemeKey.WarningAccentText,"Status","#FF9800"),
        new(ThemeKey.BrandFacebook,"Brand","#1877F2",true), new(ThemeKey.BrandTikTok,"Brand","#FE2C55",true), new(ThemeKey.BrandYouTube,"Brand","#FF0000",true),
        new(ThemeKey.BrandMediaBackground,"Brand","#1E1E1E",true), new(ThemeKey.BrandMediaPanel,"Brand","#2D2D30",true),
        new(ThemeKey.BrandMediaBorder,"Brand","#3E3E42",true), new(ThemeKey.BrandMediaButton,"Brand","#4A4A4A",true),
        new(ThemeKey.BrandMediaConsoleBorder,"Brand","#333333",true),
        new(ThemeKey.BrandMediaSelection,"Brand","#0E639C",true), new(ThemeKey.BrandMediaText,"Brand","#D4D4D4",true),
        new(ThemeKey.BrandMediaMutedText,"Brand","#AAAAAA",true), new(ThemeKey.BrandMediaHintText,"Brand","#888888",true),
        new(ThemeKey.BrandMediaDetailText,"Brand","#CCCCCC",true)
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

    private static readonly HashSet<string> LegacyKeys = new(new[] { "PageBackground", "Text1", "Text2", "Text3", "Accent", "CardBackground", "CardText1", "CardText2", "InputBackground", "InputText", "ButtonPrimaryBackground", "ButtonPrimaryText" });

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

    public static async Task<ModeThemeColors> GetAsync(ActivityDisplayMode mode, string username, AppSettingsService settings)
    {
        var defaults = Get(mode);
        var prefix = StoragePrefix(mode);
        var stored = await settings.GetByPrefixAsync(username, prefix);
        Color Read(string token) => stored.TryGetValue(prefix + token, out var value) && TryNormalizeHex(value, out var hex)
            ? Color.FromArgb(hex) : defaults.Tokens[token];
        var colors = new ModeThemeColors(Read("PageBackground"), Read("Text1"), Read("Text2"), Read("Text3"), Read("Accent"),
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
