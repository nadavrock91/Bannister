namespace Bannister.Services;

internal static class ImageFitCycle
{
    public const int DefaultMode = 0;
    private const int ModeCount = 3;
    private const string CycleIcon = "\u27F3";

    public static int Normalize(int mode) =>
        mode is >= 0 and < ModeCount ? mode : DefaultMode;

    public static int Next(int mode) => (Normalize(mode) + 1) % ModeCount;

    public static Aspect ToAspect(int mode) => Normalize(mode) switch
    {
        1 => Aspect.AspectFill,
        2 => Aspect.Fill,
        _ => Aspect.AspectFit
    };

    public static Button CreateButton(bool compact = false, bool includeLabel = true)
    {
        var button = new Button
        {
            Text = includeLabel ? $"{CycleIcon} Fit" : CycleIcon,
            CornerRadius = compact ? 4 : 8,
            FontSize = compact ? 10 : 12,
            HeightRequest = compact ? 22 : 36,
            Padding = compact ? 0 : new Thickness(10, 0)
        };
        if (compact) button.WidthRequest = 70;
        return button
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
    }
}
