namespace Bannister.Services;

public enum ThemeKey
{
    PageBackground, Text1, Text2, Text3, Accent,
    CardBackground, CardText1, CardText2, CardText3, CardTextStrong, DisabledText,
    SecondarySurface, InputBackground, InputText, Overlay, OverlayStrong, OverlayMedium, Shadow,
    RowBackground, RowAltBackground, RowSelected, Separator,
    SuccessText, SuccessBackground, SuccessButtonBackground, SuccessDarkButtonBackground, SuccessBrightText, SuccessStrongText,
    WarningText, WarningBackground, WarningButtonBackground, WarningDarkButtonBackground, WarningStrongText,
    ErrorText, ErrorBackground, ErrorButtonBackground, ErrorDarkButtonBackground, ErrorBrightText, ErrorStrongText,
    InfoText, InfoBackground, InfoButtonBackground, InfoStrongText, InfoBrightText,
    ButtonPrimaryBackground, ButtonPrimaryText, SecondaryButtonBackground, SecondaryButtonText,
    NeutralButtonBackground, NeutralButtonText, DisabledButtonBackground, OnColorText,
    AccentPurpleText, AccentPurpleStrongText, AccentPurpleBackground, AccentPurpleButtonBackground,
    AccentPurpleLightButtonBackground, AccentPurpleBorder, Link,
    HighlightYellow, HighlightYellowText, HighlightYellowStrong, HighlightYellowSoft,
    HighlightIndigo, HighlightIndigoText, HighlightIndigoStrongText, HighlightIndigoButton,
    HighlightTeal, HighlightTealText, HighlightPink, HighlightPinkText,
    HighlightOrange, HighlightLime, HighlightLimeText, HighlightViolet, HighlightVioletText,
    HighlightSky, HighlightSkyText,
    TimelineSuccess, TimelineError, TimelineInfo, TimelineWarning,
    NeutralDarkButton, NeutralMutedButton, WarningGoldenButton, HighlightYellowButton,
    HighlightSkyButton, InfoLightText, ErrorSelectedBackground, InfoDeepText, NeutralHoverButton, WarningSoftText,
    SecondaryAccentText, SuccessLightText, InfoLinkText, SlateText, HighlightSkyAccentText,
    AccentPurpleSurface, AccentPurpleDarkButtonBackground, ErrorStrongButtonBackground, HighlightYellowDarkButton,
    SuccessHoverButtonBackground, WarningHoverButtonBackground, InfoHoverButtonBackground,
    WarningAmber, HighlightBlueStrong, HighlightMint, HighlightMintText,
    TealButtonBackground, BrownButtonBackground, BrownLightButtonBackground, WarningAmberButtonBackground,
    SecondaryLavenderSurface, WarningLightButtonBackground, InfoPaleText, DisabledBlueGrey,
    AccentPurpleSelected, AccentVioletButton, HighlightGoldButton, InfoSkyButton,
    HighlightPinkStrong, HighlightTealStrongText, AccentVioletDeep, WarningBurntText,
    HighlightEmeraldText, SuccessSelectedBackground, HighlightIndigoSelected, AccentVioletStrongText,
    HighlightSkyStrong, HighlightEmerald, TranslucentWhiteSurface, OverlayLight,
    OverlayDark, HighlightIndigoDeepText, PinkButtonBackground, TimelinePurple,
    DeepOrangeButtonBackground, LimeButtonBackground, HighlightAmberButton, ErrorPureText,
    ErrorDeepText, HighlightIndigoStrong,
    BrownText, WarningAccentText,
    HighlightMintStrong, BrownStrongText, BrownMutedText, BrownMutedButton,
    WarmSurface, WarmSelectedSurface, TimelineSalmon, TimelineLavender,
    TimelineViolet, TimelineBrightGreen, TimelineMint, DarkSuccessBackground,
    IndigoMutedButton, HighlightAmberStrong,
    NeutralStrongButtonBackground,
    PeriwinkleButtonBackground, PublicCardBackground, PrivateCardBackground, DeepVioletText,
    BrandFacebook, BrandTikTok, BrandYouTube, BrandMediaBackground, BrandMediaPanel,
    BrandMediaBorder, BrandMediaConsoleBorder, BrandMediaButton, BrandMediaSelection, BrandMediaText,
    BrandMediaMutedText, BrandMediaHintText, BrandMediaDetailText,
    PageText1 = Text1, PageText2 = Text2, PageText3 = Text3
}

public sealed record ThemeColorDefinition(ThemeKey Key, string Group, string Hex, bool Fixed = false)
{
    public string Name => ModeTheme.KeyName(Key);
    public string Label => Key switch { ThemeKey.Text1 => "PageText1", ThemeKey.Text2 => "PageText2", ThemeKey.Text3 => "PageText3", _ => Name };
}
