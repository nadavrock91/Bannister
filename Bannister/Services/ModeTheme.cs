using System.Text.RegularExpressions;

namespace Bannister.Services;

public sealed record ModeThemeColors(
    Color PageBackground, Color Text1, Color Text2, Color Text3, Color Accent,
    Color CardBackground, Color CardText1, Color CardText2,
    Color InputBackground, Color InputText,
    Color ButtonPrimaryBackground, Color ButtonPrimaryText)
{
    public Color Background => PageBackground;
    public Color Foreground => Text1;
    public Color MutedForeground => Text2;

    public IReadOnlyDictionary<string, Color> Tokens => new Dictionary<string, Color>
    {
        [nameof(PageBackground)] = PageBackground, [nameof(Text1)] = Text1,
        [nameof(Text2)] = Text2, [nameof(Text3)] = Text3, [nameof(Accent)] = Accent,
        [nameof(CardBackground)] = CardBackground, [nameof(CardText1)] = CardText1,
        [nameof(CardText2)] = CardText2, [nameof(InputBackground)] = InputBackground,
        [nameof(InputText)] = InputText, [nameof(ButtonPrimaryBackground)] = ButtonPrimaryBackground,
        [nameof(ButtonPrimaryText)] = ButtonPrimaryText
    };
}

public static class ModeTheme
{
    public static ModeThemeColors Get(ActivityDisplayMode mode)
    {
        var (background, secondary, accent) = mode switch
        {
            ActivityDisplayMode.PublicOnly => ("#1A1A2E", "#D7D9E8", "#00E5FF"),
            ActivityDisplayMode.PrivateOnly => ("#2D0A4E", "#E6D9F2", "#FFD700"),
            _ => ("#6B73FF", "#101026", "#FFFFFF")
        };
        return new(Color.FromArgb(background), Colors.White, Color.FromArgb(secondary), Color.FromArgb(secondary),
            Color.FromArgb(accent), Colors.White, Color.FromArgb("#333333"), Color.FromArgb("#666666"),
            Color.FromArgb("#FAFAFA"), Color.FromArgb("#222222"), Color.FromArgb("#1565C0"), Colors.White);
    }

    public static string StoragePrefix(ActivityDisplayMode mode) => "theme_" + (mode switch
    {
        ActivityDisplayMode.PrivateOnly => "private",
        ActivityDisplayMode.PublicOnly => "public",
        _ => "all"
    }) + "_";

    public static bool TryNormalizeHex(string? text, out string hex)
    {
        hex = text?.Trim().ToUpperInvariant() ?? "";
        return Regex.IsMatch(hex, "^#[0-9A-F]{6}$");
    }

    public static string ToHex(Color color) => $"#{(int)Math.Round(color.Red * 255):X2}{(int)Math.Round(color.Green * 255):X2}{(int)Math.Round(color.Blue * 255):X2}";

    public static async Task<ModeThemeColors> GetAsync(ActivityDisplayMode mode, string username, AppSettingsService settings)
    {
        var defaults = Get(mode);
        var prefix = StoragePrefix(mode);
        var stored = await settings.GetByPrefixAsync(username, prefix);
        Color Read(string token) => stored.TryGetValue(prefix + token, out var value) && TryNormalizeHex(value, out var hex)
            ? Color.FromArgb(hex) : defaults.Tokens[token];
        return new(Read("PageBackground"), Read("Text1"), Read("Text2"), Read("Text3"), Read("Accent"),
            Read("CardBackground"), Read("CardText1"), Read("CardText2"), Read("InputBackground"), Read("InputText"),
            Read("ButtonPrimaryBackground"), Read("ButtonPrimaryText"));
    }

    public static Color GetBackground(ActivityDisplayMode mode) => Get(mode).Background;
}
