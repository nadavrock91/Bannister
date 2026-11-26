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
