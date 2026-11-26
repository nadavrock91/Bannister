using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace Bannister.Services;

public static class AppColors
{
    private static readonly ConcurrentDictionary<(string User, ActivityDisplayMode Mode), ModeThemeColors> Palettes = new();

    internal static void Remember(string username, ActivityDisplayMode mode, ModeThemeColors colors) =>
        Palettes[(username, mode)] = colors;

    public static Color Get(ThemeKey key)
    {
        string name = ModeTheme.KeyName(key);
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var auth = services?.GetService<AuthService>();
        var privacy = services?.GetService<PrivacyModeService>();
        if (auth?.IsLoggedIn == true && privacy != null)
        {
            var mode = privacy.GetDisplayMode(auth.CurrentUsername);
            return (Palettes.TryGetValue((auth.CurrentUsername, mode), out var colors) ? colors : ModeTheme.Get(mode)).Tokens[name];
        }
        if (Application.Current?.Resources.TryGetValue("Mode" + name, out var value) == true && value is Color color)
            return color;
        return ModeTheme.Get(ActivityDisplayMode.All).Tokens[name];
    }

    public static string Hex(ThemeKey key) => ModeTheme.ToHex(Get(key));
}

public static class ThemeColorExtensions
{
    private sealed record Binding(ThemeKey Key, ThemeKey? PageKey);
    private static readonly ConditionalWeakTable<Element, Dictionary<BindableProperty, Binding>> Bindings = new();

    public static T ThemeColor<T>(this T element, BindableProperty property, ThemeKey key, ThemeKey? pageKey = null) where T : Element
    {
        if (!Bindings.TryGetValue(element, out var bindings))
        {
            bindings = Bindings.GetOrCreateValue(element);
            element.PropertyChanged += (_, args) => { if (args.PropertyName == "Parent") RefreshThemeColors(element); };
            if (element is VisualElement visual) visual.Loaded += (_, _) => RefreshThemeColors(element);
        }
        bindings[property] = new(key, pageKey);
        Bind(element, property, bindings[property]);
        return element;
    }

    public static bool HasThemeColor(this Element element, BindableProperty? property = null) =>
        Bindings.TryGetValue(element, out var bindings) && (property == null || bindings.ContainsKey(property));

    // Cloned navigation controls must retain their source's live color bindings.
    public static T CopyThemeColorsFrom<T>(this T target, Element source, params BindableProperty[] properties) where T : Element
    {
        foreach (var property in properties)
        {
            if (Bindings.TryGetValue(source, out var sourceBindings) && sourceBindings.TryGetValue(property, out var binding))
                target.ThemeColor(property, binding.Key, binding.PageKey);
            else
            {
                if (Bindings.TryGetValue(target, out var targetBindings)) targetBindings.Remove(property);
                target.RemoveDynamicResource(property);
                target.ClearValue(property);
                target.SetValue(property, source.GetValue(property));
            }
        }
        return target;
    }

    internal static bool UsesThemeColor(this Element element, BindableProperty property, ThemeKey key) =>
        Bindings.TryGetValue(element, out var bindings) && bindings.TryGetValue(property, out var binding) && binding.Key == key;

    public static void RefreshThemeColors(Element element)
    {
        if (Bindings.TryGetValue(element, out var bindings))
            foreach (var binding in bindings) Bind(element, binding.Key, binding.Value);
    }

    private static void Bind(Element element, BindableProperty property, Binding binding)
    {
        var key = binding.Key;
        if (binding.PageKey.HasValue)
        {
            for (Element? ancestor = element; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor is Bannister.Views.ModeThemedPage) { key = binding.PageKey.Value; break; }
                if (ancestor is Page || ancestor is Button || ancestor is VisualElement visual &&
                    (visual.BackgroundColor?.Alpha > 0 || HasBackground(visual.Background))) break;
            }
        }
        element.ClearValue(property);
        element.SetDynamicResource(property, "Mode" + ModeTheme.KeyName(key));
    }

    private static bool HasBackground(Brush? brush) => brush switch
    {
        null => false,
        SolidColorBrush solid => solid.Color?.Alpha > 0,
        GradientBrush gradient => gradient.GradientStops.Any(stop => stop.Color?.Alpha > 0),
        _ => !brush.IsEmpty
    };
}
