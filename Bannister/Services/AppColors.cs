using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace Bannister.Services;

public interface IThemeContext
{
    (string Username, ActivityDisplayMode Mode)? Current { get; }
}

public interface IModeThemePage { }

public static class AppColors
{
    private static readonly ConcurrentDictionary<(string User, ActivityDisplayMode Mode), ModeThemeColors> Palettes = new();

    internal static void Remember(string username, ActivityDisplayMode mode, ModeThemeColors colors) =>
        Palettes[(username, mode)] = colors;

    public static Color Get(ThemeKey key)
    {
        string name = ModeTheme.KeyName(key);
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var context = services?.GetService<IThemeContext>()?.Current;
        if (context is { } current)
        {
            return (Palettes.TryGetValue((current.Username, current.Mode), out var colors) ? colors : ModeTheme.Get(current.Mode)).Tokens[name];
        }
        if (Application.Current?.Resources.TryGetValue("Mode" + name, out var value) == true && value is Color color)
            return color;
        return ModeTheme.Get(ActivityDisplayMode.All).Tokens[name];
    }

    public static string Hex(ThemeKey key) => ModeTheme.ToHex(Get(key));

    // Saved per-item colors are user data; an empty override follows the named default.
    public static Color Get(ThemeKey key, string? storedHex) => string.IsNullOrWhiteSpace(storedHex)
        ? Get(key) : ModeTheme.ParseStoredHex(storedHex);
}

public static class ThemeColorExtensions
{
    private sealed record Binding(ThemeKey? Key, ThemeKey? PageKey, bool ClassifyPageText);
    private static readonly ConditionalWeakTable<Element, Dictionary<BindableProperty, Binding>> Bindings = new();
    private static readonly Lazy<IReadOnlyDictionary<string, Color>> TextDefaults = new(() => ModeTheme.Get(ActivityDisplayMode.All).Tokens);

    public static T ThemeColor<T>(this T element, BindableProperty property, ThemeKey key, ThemeKey? pageKey = null, bool classifyPageText = false) where T : Element
        => ThemeColor(element, property, (ThemeKey?)key, pageKey, classifyPageText);

    // A null key is structural transparency, including the inactive branch of a highlight.
    public static T ThemeColor<T>(this T element, BindableProperty property, ThemeKey? key, ThemeKey? pageKey = null, bool classifyPageText = false) where T : Element
    {
        if (!Bindings.TryGetValue(element, out var bindings))
        {
            bindings = Bindings.GetOrCreateValue(element);
            element.PropertyChanged += (_, args) => { if (args.PropertyName == "Parent") RefreshThemeColors(element); };
            if (element is VisualElement visual) visual.Loaded += (_, _) => RefreshThemeColors(element);
        }
        bindings[property] = new(key, pageKey, classifyPageText);
        Bind(element, property, bindings[property]);
        return element;
    }

    public static readonly BindableProperty NamedColorPropertiesProperty = BindableProperty.CreateAttached(
        "NamedColorProperties", typeof(string), typeof(ThemeColorExtensions), "");
    public static string GetNamedColorProperties(BindableObject element) => (string)element.GetValue(NamedColorPropertiesProperty);
    public static void SetNamedColorProperties(BindableObject element, string value) => element.SetValue(NamedColorPropertiesProperty, value);

    public static bool HasThemeColor(this Element element, BindableProperty? property = null) =>
        Bindings.TryGetValue(element, out var bindings) && (property == null || bindings.ContainsKey(property)) ||
        GetNamedColorProperties(element).Split(',', StringSplitOptions.RemoveEmptyEntries).Any(name => property == null || name.Split('=')[0] == property.PropertyName);

    // Cloned navigation controls must retain their source's live color bindings.
    public static T CopyThemeColorsFrom<T>(this T target, Element source, params BindableProperty[] properties) where T : Element
    {
        foreach (var property in properties)
        {
            if (Bindings.TryGetValue(source, out var sourceBindings) && sourceBindings.TryGetValue(property, out var binding))
                target.ThemeColor(property, binding.Key, binding.PageKey, binding.ClassifyPageText);
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
        Bindings.TryGetValue(element, out var bindings) && bindings.TryGetValue(property, out var binding) && binding.Key == key ||
        GetNamedColorProperties(element).Split(',').Contains(property.PropertyName + "=" + ModeTheme.KeyName(key));

    // Temporary feedback must restore the binding, so it also survives the next theme refresh.
    public static Action CaptureThemeColor(this Element element, BindableProperty property)
    {
        Bindings.TryGetValue(element, out var bindings);
        Binding? saved = bindings != null && bindings.TryGetValue(property, out var binding) ? binding : null;
        var original = element.GetValue(property);
        return () =>
        {
            if (saved != null) element.ThemeColor(property, saved.Key, saved.PageKey, saved.ClassifyPageText);
            else
            {
                if (Bindings.TryGetValue(element, out var current)) current.Remove(property);
                element.RemoveDynamicResource(property);
                element.ClearValue(property);
                element.SetValue(property, original);
            }
        };
    }

    public static void RefreshThemeColors(Element element)
    {
        if (Bindings.TryGetValue(element, out var bindings))
            foreach (var binding in bindings) Bind(element, binding.Key, binding.Value);
    }

    private static void Bind(Element element, BindableProperty property, Binding binding)
    {
        var key = binding.Key;
        if (key == null)
        {
            element.RemoveDynamicResource(property);
            element.ClearValue(property);
            element.SetValue(property, Colors.Transparent);
            return;
        }
        if (binding.PageKey.HasValue)
        {
            for (Element? ancestor = element; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor is IModeThemePage)
                {
                    key = binding.ClassifyPageText && element is Label label ? PageTextKey(label, binding.Key.GetValueOrDefault()) : binding.PageKey.Value;
                    break;
                }
                if (ancestor is Page || ancestor is Button || ancestor is VisualElement visual &&
                    (visual.BackgroundColor?.Alpha > 0 || HasBackground(visual.Background))) break;
            }
        }
        element.ClearValue(property);
        element.SetDynamicResource(property, "Mode" + ModeTheme.KeyName(key.Value));
    }

    private static ThemeKey PageTextKey(Label label, ThemeKey key)
    {
        double size = label.FontSize > 0 ? label.FontSize : 14;
        bool heading = size >= 18 || size >= 13 && label.FontAttributes.HasFlag(FontAttributes.Bold);
        var color = TextDefaults.Value[ModeTheme.KeyName(key)];
        bool grey = Math.Max(color.Red, Math.Max(color.Green, color.Blue)) - Math.Min(color.Red, Math.Min(color.Green, color.Blue)) <= 0.10 && color.Red >= 0.25 && color.Red <= 0.85;
        return size < 13 && !heading ? ThemeKey.Text3 : !heading && grey ? ThemeKey.Text2 : ThemeKey.Text1;
    }

    private static bool HasBackground(Brush? brush) => brush switch
    {
        null => false,
        SolidColorBrush solid => solid.Color?.Alpha > 0,
        GradientBrush gradient => gradient.GradientStops.Any(stop => stop.Color?.Alpha > 0),
        _ => !brush.IsEmpty
    };
}
