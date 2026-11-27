using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bannister.Services;

public sealed record ThemeColorChange(ThemeColorDefinition Definition, string OldHex, string NewHex);
public sealed record ThemeImportResult(IReadOnlyList<ThemeColorChange> Changes, IReadOnlyList<string> Warnings);

/// <summary>Palette exchange uses Settings labels externally and the existing storage keys internally.</summary>
public static class ThemeImportExport
{
    private static readonly Regex Assignment = new("""\btheme\s*\[\s*"(?<name>[^"\r\n]*)"\s*\]\s*=\s*"(?<hex>[^"\r\n]*)"\s*;?""", RegexOptions.Compiled);

    public static string ModeName(ActivityDisplayMode mode) => mode switch
    {
        ActivityDisplayMode.PrivateOnly => "Private",
        ActivityDisplayMode.PublicOnly => "Public",
        _ => "Both"
    };

    public static ThemeColorDefinition? FindColor(string name) => ModeTheme.Catalog.FirstOrDefault(d => d.Label == name || d.Name == name);

    public static string Role(ThemeColorDefinition color)
    {
        string name = color.Name;
        string Words(string text) => Regex.Replace(text, "([a-z0-9])([A-Z])", "$1 $2").ToLowerInvariant();
        string? specific = name switch
        {
            "PageBackground" => "page background behind cards and controls",
            "Text1" => "primary text on the page background",
            "Text2" => "secondary text on the page background",
            "Text3" => "hint text on the page background",
            "Accent" => "page accent and navigation emphasis",
            "DisabledText" => "disabled text",
            "CardBackground" => "plain card background",
            "CardText1" => "primary text inside cards",
            "CardText2" => "secondary text inside cards",
            "CardText3" => "hint text inside cards",
            "CardTextStrong" => "emphasized text inside cards",
            "SecondarySurface" => "secondary neutral surface",
            "InputBackground" => "input background",
            "InputText" => "text inside inputs",
            "RowBackground" => "normal table row background",
            "RowAltBackground" => "alternate table row background",
            "RowSelected" => "selected row background",
            "Separator" => "borders and separator lines",
            "Shadow" => "shadow color",
            "OnColorText" => "text on colored buttons and surfaces",
            "ButtonPrimaryBackground" => "primary button background",
            "ButtonPrimaryText" => "text on primary buttons",
            "DisabledBlueGrey" => "disabled blue-grey button background",
            "SecondaryLavenderSurface" => "secondary lavender card surface",
            "WarmSurface" => "warm neutral card surface",
            "WarmSelectedSurface" => "selected warm card surface",
            "AccentVioletDeep" => "deep violet button background",
            "Link" => "link text",
            "AccentPurpleSurface" => "purple button surface",
            "AccentPurpleSelected" => "selected purple control surface",
            "ChartAreaFill" => "translucent filled chart area",
            "TranslucentWhiteSurface" => "translucent light surface over content",
            _ => null
        };
        if (specific != null) return specific;
        if (name.StartsWith("Overlay", StringComparison.Ordinal)) return Words(name) + " scrim over content (preserve alpha)";
        if (name.StartsWith("Timeline", StringComparison.Ordinal)) return Words(name[8..]) + " timeline indicator";
        if (name.EndsWith("ButtonBackground", StringComparison.Ordinal)) return Words(name[..^16]) + " button background";
        if (name.EndsWith("ButtonText", StringComparison.Ordinal)) return Words(name[..^10]) + " button text";
        if (name.EndsWith("Button", StringComparison.Ordinal)) return Words(name[..^6]) + " button background";
        if (name.EndsWith("Background", StringComparison.Ordinal)) return Words(name[..^10]) + " surface background";
        if (name.EndsWith("Text", StringComparison.Ordinal)) return Words(name[..^4]) + " text on matching surfaces";
        if (name.EndsWith("Border", StringComparison.Ordinal)) return Words(name[..^6]) + " border";
        if (name.EndsWith("Surface", StringComparison.Ordinal)) return Words(name[..^7]) + " surface";
        if (name.StartsWith("Highlight", StringComparison.Ordinal)) return Words(name[9..]) + " highlighted surface";
        return Words(name) + " surface";
    }

    public static string BuildPrompt(ActivityDisplayMode mode, IReadOnlyDictionary<string, Color> palette, string? styleRequest = null)
    {
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(styleRequest)) text.AppendLine("Style request: " + styleRequest.Trim()).AppendLine();
        text.AppendLine($"Propose a cohesive Bannister UI color palette for {ModeName(mode)} mode{(mode == ActivityDisplayMode.All ? " (the All palette)" : "")}.");
        text.AppendLine("The page background sits behind cards (white by default), light inputs, and colored status, highlight, and button surfaces.");
        text.AppendLine("Edit only this palette; do not change the app's display mode. Current named colors follow, one per line: [Settings group] Name = role; current hex.");
        text.AppendLine("Fixed Brand colors are excluded and must not be changed.").AppendLine();
        foreach (var color in ModeTheme.Catalog.Where(d => !d.Fixed && d.Group != "Brand"))
            text.AppendLine($"[{color.Group}] {color.Label} = {Role(color)}; {ModeTheme.ToHex(palette[color.Name])}");
        text.AppendLine().AppendLine("Keep readable contrast of at least 4.5:1 for text against its actual surface: page text on PageBackground, card text on CardBackground, input text on InputBackground, button text on its button background, and status text on its status background.");
        text.AppendLine("Keep status meanings recognizable: success green-ish, warning amber-ish, and error red-ish. Keep highlight and button colors cohesive with the palette.");
        text.AppendLine("Preserve the existing alpha channel on Overlay colors and other translucent surfaces such as ChartAreaFill and TranslucentWhiteSurface; use #AARRGGBB for these colors.");
        text.AppendLine("Return a single C# code block. Use exactly the existing names listed above, with one assignment per color in the format below. Colors that should not change may be omitted. Put no prose, comments, headings, or other text inside the code block.");
        text.AppendLine("Format (the example values are current values; replace them with your proposals):");
        text.AppendLine("```csharp");
        text.AppendLine($"theme[\"PageBackground\"] = \"{ModeTheme.ToHex(palette[ModeTheme.KeyName(ThemeKey.PageBackground)])}\";");
        text.AppendLine($"theme[\"PageText1\"] = \"{ModeTheme.ToHex(palette[ModeTheme.KeyName(ThemeKey.Text1)])}\";");
        text.Append("```");
        return text.ToString().Replace("\r\n", "\n");
    }

    public static ThemeImportResult Parse(string response, IReadOnlyDictionary<string, Color> current)
    {
        var values = new Dictionary<string, ThemeColorChange>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int lineNumber = 0;
        foreach (string line in response.Split('\n'))
        {
            lineNumber++;
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            var matches = Assignment.Matches(line);
            if (matches.Count == 0 && Regex.IsMatch(line, @"\btheme\s*\["))
                warnings.Add($"Line {lineNumber}: invalid theme assignment.");
            foreach (Match match in matches)
            {
                string name = match.Groups["name"].Value;
                var definition = FindColor(name);
                if (definition == null) { warnings.Add($"Line {lineNumber}: unknown color '{name}' ignored."); continue; }
                if (definition.Fixed || definition.Group == "Brand") { warnings.Add($"Line {lineNumber}: Brand color '{name}' ignored."); continue; }
                if (!ModeTheme.TryNormalizeHex(match.Groups["hex"].Value, out string hex))
                { warnings.Add($"Line {lineNumber}: invalid hex for {definition.Label}; use #RRGGBB or #AARRGGBB."); continue; }
                hex = ModeTheme.ToHex(ModeTheme.ParseStoredHex(hex));
                if (!seen.Add(definition.Name)) warnings.Add($"Line {lineNumber}: duplicate {definition.Label}; last valid value used.");
                string oldHex = ModeTheme.ToHex(current[definition.Name]);
                if (oldHex == hex) values.Remove(definition.Name);
                else
                {
                    values[definition.Name] = new(definition, oldHex, hex);
                    if (definition.Name.StartsWith("Overlay", StringComparison.Ordinal) &&
                        Math.Abs(ModeTheme.ParseStoredHex(hex).Alpha - current[definition.Name].Alpha) > 0.001)
                        warnings.Add($"{definition.Label}: overlay alpha changed; review the opacity.");
                }
            }
        }
        return new(values.Values.ToList(), warnings);
    }

    public static IReadOnlyList<string> ContrastWarnings(ActivityDisplayMode mode, IReadOnlyDictionary<string, Color> current, IEnumerable<ThemeColorChange> selected)
    {
        var palette = current.ToDictionary(x => x.Key, x => x.Value);
        foreach (var change in selected) palette[change.Definition.Name] = ModeTheme.ParseStoredHex(change.NewHex);
        var warnings = new List<string>();
        var checkedPairs = new HashSet<(string, string, string)>();
        var defaultPage = ModeTheme.Get(mode).PageBackground;
        var page = Composite(palette["PageBackground"], defaultPage);
        var card = Composite(palette["CardBackground"], page);
        void Check(string foreground, string background, Color behind)
        {
            Color surface = Composite(palette[background], behind);
            if (!checkedPairs.Add((foreground, background, ModeTheme.ToHex(surface)))) return;
            double ratio = ContrastRatio(palette[foreground], surface);
            if (ratio < 4.5)
                warnings.Add($"{FindColor(foreground)?.Label ?? foreground} on {FindColor(background)?.Label ?? background}: {ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1 (below 4.5:1).");
        }
        foreach (string key in new[] { "Text1", "Text2", "Text3" }) Check(key, "PageBackground", defaultPage);
        foreach (string key in new[] { "CardText1", "CardText2", "CardText3", "CardTextStrong" }) Check(key, "CardBackground", page);
        foreach (string background in new[] { "RowBackground", "RowAltBackground", "RowSelected", "SecondarySurface" })
            foreach (string foreground in new[] { "CardText1", "CardText2" }) Check(foreground, background, card);
        Check("InputText", "InputBackground", card);
        Check("InputText", "InputBackground", page);
        Check("ButtonPrimaryText", "ButtonPrimaryBackground", card);
        Check("SecondaryButtonText", "SecondaryButtonBackground", card);
        Check("OnColorText", "ButtonPrimaryBackground", card);
        Check("OnColorText", "SecondaryButtonBackground", card);
        Check("NeutralButtonText", "NeutralButtonBackground", card);
        foreach (var definition in ModeTheme.Catalog.Where(d => !d.Fixed))
        {
            string name = definition.Name;
            if ((name.EndsWith("ButtonBackground", StringComparison.Ordinal) || name.EndsWith("Button", StringComparison.Ordinal) || name == "AccentPurpleSurface") &&
                name is not "ButtonPrimaryBackground" and not "SecondaryButtonBackground" and not "NeutralButtonBackground")
                Check("OnColorText", name, card);
            var status = Regex.Match(name, @"^(Success|Warning|Error|Info)(?:Bright|Strong|Deep|Light|Pale|Pure|Link)?Text$");
            if (status.Success) Check(name, status.Groups[1].Value + "Background", card);
            if (name.StartsWith("Highlight", StringComparison.Ordinal) && name.EndsWith("Text", StringComparison.Ordinal))
            {
                string background = Regex.Replace(name[..^4], "(?:Strong|Dark|Accent|Deep)$", "");
                if (palette.ContainsKey(background)) Check(name, background, card);
            }
        }
        foreach (string key in new[] { "BrownText", "BrownStrongText", "BrownMutedText" })
        {
            Check(key, "WarmSurface", card);
            Check(key, "WarmSelectedSurface", card);
        }
        Check("AccentPurpleText", "AccentPurpleBackground", card);
        Check("AccentPurpleStrongText", "AccentPurpleBackground", card);
        Check("SlateText", "SecondarySurface", card);
        Check("SecondaryAccentText", "HighlightIndigoSelected", card);
        Check("Link", "CardBackground", page);
        Check("OnColorText", "DarkSuccessBackground", card);
        Check("OnColorText", "DarkErrorBackground", card);
        Check("OnColorText", "PublicCardBackground", page);
        Check("OnColorText", "PrivateCardBackground", page);
        return warnings;
    }

    public static double ContrastRatio(Color foreground, Color background)
    {
        var visible = Composite(foreground, background);
        static double Luminance(Color c)
        {
            static double Linear(double channel) => channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(c.Red) + 0.7152 * Linear(c.Green) + 0.0722 * Linear(c.Blue);
        }
        double a = Luminance(visible), b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static Color Composite(Color front, Color back) => new(
        front.Red * front.Alpha + back.Red * (1 - front.Alpha),
        front.Green * front.Alpha + back.Green * (1 - front.Alpha),
        front.Blue * front.Alpha + back.Blue * (1 - front.Alpha), 1f);
}
