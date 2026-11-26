namespace Bannister.Services;

public sealed record ModeThemeColors(
    Color Background,
    Color Foreground,
    Color MutedForeground,
    Color Accent);

public static class ModeTheme
{
    public static ModeThemeColors Get(ActivityDisplayMode mode) => mode switch
    {
        ActivityDisplayMode.PublicOnly => new(
            Color.FromArgb("#1A1A2E"),
            Colors.White,
            Color.FromArgb("#D7D9E8"),
            Color.FromArgb("#00E5FF")),
        ActivityDisplayMode.PrivateOnly => new(
            Color.FromArgb("#2D0A4E"),
            Colors.White,
            Color.FromArgb("#E6D9F2"),
            Color.FromArgb("#FFD700")),
        _ => new(
            Color.FromArgb("#6B73FF"),
            Colors.White,
            Color.FromArgb("#101026"),
            Colors.White)
    };

    public static Color GetBackground(ActivityDisplayMode mode) => Get(mode).Background;
}
