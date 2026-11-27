namespace Bannister.Services;

public sealed record RetiredThemeColor(string OldName, ThemeKey Core);
public sealed record ThemeOverrideDecision(string Mode, string OldName, string CoreName, string Value, string? KeptValue, string Action, string Reason);
public sealed record ThemeMigrationReport(IReadOnlyList<ThemeOverrideDecision> Decisions, bool Deferred = false);
public sealed record ThemeMigrationPlan(Dictionary<string, string> Values, Dictionary<string, string> Updates, IReadOnlyList<string> RetiredKeys, ThemeMigrationReport Report);

/// <summary>Compatibility data and pure planning for the one-time per-user catalog migration.</summary>
public static class ThemePaletteMigration
{
    public const string ReportKey = "ui_color_consolidation_v1";
    // Ordered by closest original default to the core default, then original catalog order.
    // Existing core overrides always win, regardless of this priority.
    public static IReadOnlyList<RetiredThemeColor> Retired { get; } = new RetiredThemeColor[]
    {
        new("BrandMediaSelection", ThemeKey.ButtonPrimaryBackground),
        new("SecondaryButtonText", ThemeKey.ButtonPrimaryText),
        new("OnColorText", ThemeKey.ButtonPrimaryText),
        new("RowBackground", ThemeKey.CardBackground),
        new("TranslucentWhiteSurface", ThemeKey.CardBackground),
        new("BrownStrongText", ThemeKey.CardText1),
        new("CardTextStrong", ThemeKey.CardText1),
        new("BrownText", ThemeKey.CardText1),
        new("BrownMutedText", ThemeKey.CardText2),
        new("SlateText", ThemeKey.CardText2),
        new("DisabledText", ThemeKey.CardText3),
        new("BrandMediaMutedText", ThemeKey.CardText3),
        new("BrandMediaHintText", ThemeKey.CardText3),
        new("BrandMediaDetailText", ThemeKey.CardText3),
        new("DisabledBlueGrey", ThemeKey.DisabledButtonBackground),
        new("ErrorSelectedBackground", ThemeKey.ErrorBackground),
        new("TimelineError", ThemeKey.ErrorButtonBackground),
        new("ErrorStrongButtonBackground", ThemeKey.ErrorButtonBackground),
        new("ErrorDarkButtonBackground", ThemeKey.ErrorButtonBackground),
        new("ErrorStrongText", ThemeKey.ErrorText),
        new("ErrorDeepText", ThemeKey.ErrorText),
        new("ErrorBrightText", ThemeKey.ErrorText),
        new("ErrorPureText", ThemeKey.ErrorText),
        new("DarkErrorBackground", ThemeKey.ErrorText),
        new("HighlightSky", ThemeKey.HighlightIndigo),
        new("HighlightIndigoSelected", ThemeKey.HighlightIndigo),
        new("HighlightBlueStrong", ThemeKey.HighlightIndigo),
        new("HighlightIndigoStrong", ThemeKey.HighlightIndigo),
        new("HighlightSkyStrong", ThemeKey.HighlightIndigo),
        new("ChartAreaFill", ThemeKey.HighlightIndigo),
        new("HighlightIndigoDeepText", ThemeKey.HighlightIndigoText),
        new("HighlightIndigoButton", ThemeKey.HighlightIndigoText),
        new("HighlightIndigoStrongText", ThemeKey.HighlightIndigoText),
        new("SecondaryAccentText", ThemeKey.HighlightIndigoText),
        new("HighlightPinkStrong", ThemeKey.HighlightPink),
        new("PinkButtonBackground", ThemeKey.HighlightPinkText),
        new("HighlightMint", ThemeKey.HighlightTeal),
        new("HighlightLime", ThemeKey.HighlightTeal),
        new("HighlightEmerald", ThemeKey.HighlightTeal),
        new("HighlightMintStrong", ThemeKey.HighlightTeal),
        new("TealButtonBackground", ThemeKey.HighlightTealText),
        new("HighlightTealStrongText", ThemeKey.HighlightTealText),
        new("HighlightMintText", ThemeKey.HighlightTealText),
        new("HighlightEmeraldText", ThemeKey.HighlightTealText),
        new("HighlightCyanButton", ThemeKey.HighlightTealText),
        new("HighlightAquaButton", ThemeKey.HighlightTealText),
        new("AccentPurpleBackground", ThemeKey.HighlightViolet),
        new("SecondaryLavenderSurface", ThemeKey.HighlightViolet),
        new("AccentPurpleSelected", ThemeKey.HighlightViolet),
        new("AccentVioletStrongText", ThemeKey.HighlightVioletText),
        new("AccentPurpleStrongText", ThemeKey.HighlightVioletText),
        new("AccentPurpleDarkButtonBackground", ThemeKey.HighlightVioletText),
        new("AccentVioletDeep", ThemeKey.HighlightVioletText),
        new("DeepVioletText", ThemeKey.HighlightVioletText),
        new("AccentVioletButton", ThemeKey.HighlightVioletText),
        new("AccentPurpleText", ThemeKey.HighlightVioletText),
        new("AccentPurpleSurface", ThemeKey.HighlightVioletText),
        new("AccentPurpleButtonBackground", ThemeKey.HighlightVioletText),
        new("AccentPurpleLightButtonBackground", ThemeKey.HighlightVioletText),
        new("AccentPurpleBorder", ThemeKey.HighlightVioletText),
        new("HighlightYellowSoft", ThemeKey.HighlightYellow),
        new("HighlightYellowStrong", ThemeKey.HighlightYellow),
        new("WarmSurface", ThemeKey.HighlightYellow),
        new("HighlightOrange", ThemeKey.HighlightYellow),
        new("WarmSelectedSurface", ThemeKey.HighlightYellow),
        new("HighlightAmberStrong", ThemeKey.HighlightYellow),
        new("TimelineInfo", ThemeKey.InfoButtonBackground),
        new("InfoSkyButton", ThemeKey.InfoButtonBackground),
        new("InfoHoverButtonBackground", ThemeKey.InfoButtonBackground),
        new("HighlightSkyButton", ThemeKey.InfoButtonBackground),
        new("InfoLightText", ThemeKey.InfoButtonBackground),
        new("Link", ThemeKey.InfoText),
        new("InfoStrongText", ThemeKey.InfoText),
        new("HighlightSkyAccentText", ThemeKey.InfoText),
        new("InfoDeepText", ThemeKey.InfoText),
        new("HighlightSkyText", ThemeKey.InfoText),
        new("InfoLinkText", ThemeKey.InfoText),
        new("InfoBrightText", ThemeKey.InfoText),
        new("BrandMediaBackground", ThemeKey.MediaBackground),
        new("PublicCardBackground", ThemeKey.MediaBackground),
        new("PrivateCardBackground", ThemeKey.MediaBackground),
        new("BrandMediaText", ThemeKey.MediaText),
        new("InfoPaleText", ThemeKey.MediaText),
        new("BrandMediaButton", ThemeKey.NeutralDarkButton),
        new("BrandMediaBorder", ThemeKey.NeutralDarkButton),
        new("BrownButtonBackground", ThemeKey.NeutralDarkButton),
        new("NeutralHoverButton", ThemeKey.NeutralDarkButton),
        new("NeutralMutedButton", ThemeKey.NeutralDarkButton),
        new("BrownLightButtonBackground", ThemeKey.NeutralDarkButton),
        new("NeutralStrongButtonBackground", ThemeKey.NeutralDarkButton),
        new("BrandMediaConsoleBorder", ThemeKey.NeutralDarkButton),
        new("BrandMediaPanel", ThemeKey.NeutralDarkButton),
        new("BrownMutedButton", ThemeKey.NeutralDarkButton),
        new("OverlayLight", ThemeKey.Overlay),
        new("OverlayMedium", ThemeKey.Overlay),
        new("OverlayDark", ThemeKey.Overlay),
        new("OverlayFaint", ThemeKey.Overlay),
        new("OverlayStrong", ThemeKey.Overlay),
        new("Shadow", ThemeKey.Overlay),
        new("Text1", ThemeKey.PageText1),
        new("Text2", ThemeKey.PageText2),
        new("Text3", ThemeKey.PageText3),
        new("IndigoMutedButton", ThemeKey.SecondaryButtonBackground),
        new("PeriwinkleButtonBackground", ThemeKey.SecondaryButtonBackground),
        new("SuccessSelectedBackground", ThemeKey.SuccessBackground),
        new("SuccessLightText", ThemeKey.SuccessButtonBackground),
        new("TimelineSuccess", ThemeKey.SuccessButtonBackground),
        new("TimelineBrightGreen", ThemeKey.SuccessButtonBackground),
        new("SuccessHoverButtonBackground", ThemeKey.SuccessButtonBackground),
        new("SuccessDarkButtonBackground", ThemeKey.SuccessButtonBackground),
        new("LimeButtonBackground", ThemeKey.SuccessButtonBackground),
        new("SuccessBrightText", ThemeKey.SuccessText),
        new("HighlightLimeText", ThemeKey.SuccessText),
        new("SuccessStrongText", ThemeKey.SuccessText),
        new("DarkSuccessBackground", ThemeKey.SuccessText),
        new("TimelineSalmon", ThemeKey.TimelineAccent),
        new("TimelineLavender", ThemeKey.TimelinePrimary),
        new("TimelineViolet", ThemeKey.TimelinePrimary),
        new("TimelinePurple", ThemeKey.TimelinePrimary),
        new("TimelineMint", ThemeKey.TimelineSecondary),
        new("WarningAmberButtonBackground", ThemeKey.WarningButtonBackground),
        new("TimelineWarning", ThemeKey.WarningButtonBackground),
        new("HighlightYellowButton", ThemeKey.WarningButtonBackground),
        new("WarningHoverButtonBackground", ThemeKey.WarningButtonBackground),
        new("HighlightYellowDarkButton", ThemeKey.WarningButtonBackground),
        new("WarningGoldenButton", ThemeKey.WarningButtonBackground),
        new("HighlightAmberButton", ThemeKey.WarningButtonBackground),
        new("HighlightGoldButton", ThemeKey.WarningButtonBackground),
        new("DeepOrangeButtonBackground", ThemeKey.WarningButtonBackground),
        new("WarningDarkButtonBackground", ThemeKey.WarningButtonBackground),
        new("WarningLightButtonBackground", ThemeKey.WarningButtonBackground),
        new("WarningAmber", ThemeKey.WarningText),
        new("WarningStrongText", ThemeKey.WarningText),
        new("WarningBurntText", ThemeKey.WarningText),
        new("WarningAccentText", ThemeKey.WarningText),
        new("WarningSoftText", ThemeKey.WarningText),
    };

    public static ThemeMigrationPlan Plan(IReadOnlyDictionary<string, string> stored, bool deferred = false)
    {
        var values = stored.ToDictionary(x => x.Key, x => x.Value);
        var updates = new Dictionary<string, string>();
        var keys = new List<string>();
        var decisions = new List<ThemeOverrideDecision>();
        foreach (var mode in new[] { ActivityDisplayMode.PrivateOnly, ActivityDisplayMode.PublicOnly, ActivityDisplayMode.All })
        {
            string prefix = ModeTheme.StoragePrefix(mode);
            var originalCore = ModeTheme.Catalog.Select(c => prefix + c.Name).Where(stored.ContainsKey).ToHashSet();
            foreach (var retired in Retired)
            {
                string source = prefix + retired.OldName, targetName = ModeTheme.KeyName(retired.Core), target = prefix + targetName;
                if (!values.TryGetValue(source, out string? value)) continue;
                keys.Add(source);
                if (!ModeTheme.TryNormalizeHex(value, out string hex))
                    decisions.Add(new(ModeName(mode), retired.OldName, targetName, value, values.GetValueOrDefault(target), "Dropped", "Invalid saved hex; it was already ignored by the old palette reader."));
                else if (values.TryGetValue(target, out string? kept))
                    decisions.Add(new(ModeName(mode), retired.OldName, targetName, value, kept, "Dropped",
                        originalCore.Contains(target) ? "An override already exists for the core color." : "A higher-priority retired override already filled the core color."));
                else
                {
                    values[target] = hex;
                    updates[target] = hex;
                    decisions.Add(new(ModeName(mode), retired.OldName, targetName, value, hex, "Moved", "Core color had no override."));
                }
                values.Remove(source);
            }
        }
        return new(values, updates, keys, new(decisions, deferred));
    }

    private static string ModeName(ActivityDisplayMode mode) => mode switch
    {
        ActivityDisplayMode.PrivateOnly => "Private",
        ActivityDisplayMode.PublicOnly => "Public",
        _ => "Both"
    };
}
