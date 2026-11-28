using Bannister.Services;
using System.Runtime.CompilerServices;

namespace Bannister.Views;

/// <summary>Shared page canvas and a one-time fallback for text without named colors.</summary>
public class ModeThemedPage : ContentPage, IModeThemePage
{
    protected virtual bool PreserveCanvasBackground => false;
    private bool _visible;
    private bool _scheduled;
    private bool _initialPass;
    private readonly HashSet<Element> _pendingSubtrees = new();
    private readonly ConditionalWeakTable<Element, TextAppearance> _textAppearance = new();

    // Previews own their colors, including descendants added after the initial pass.
    public static readonly BindableProperty PreserveColorsProperty = BindableProperty.CreateAttached(
        "PreserveColors", typeof(bool), typeof(ModeThemedPage), false);
    public static void SetPreserveColors(BindableObject element, bool value) => element.SetValue(PreserveColorsProperty, value);

    private sealed class TextAppearance(Color? color)
    {
        public Color? OriginalColor { get; } = color;
        public string? ResourceKey { get; set; }
    }

    public ModeThemedPage()
    {
        SetDynamicResource(BackgroundColorProperty, "ModePageBackground");
        DescendantAdded += OnDescendantAdded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        _initialPass = true;
        var services = Handler?.MauiContext?.Services ?? Application.Current?.Handler?.MauiContext?.Services;
        _ = services?.GetService<ModeThemePublisher>()?.ApplyCurrentAsync();
        ScheduleTextFallback();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        _initialPass = false;
        _pendingSubtrees.Clear();
        base.OnDisappearing();
    }

    private void OnDescendantAdded(object? sender, ElementEventArgs args)
    {
        if (!_visible) return;
        Element root = args.Element;
        // Formatted text needs its label's inherited font and surface, not a page-wide scan.
        if (root is Span || root is FormattedString)
            for (var ancestor = root.Parent; ancestor != null && ancestor is not Page; ancestor = ancestor.Parent)
                if (ancestor is Label label) { root = label; break; }
        _pendingSubtrees.Add(root);
        ScheduleTextFallback();
    }

    private void ScheduleTextFallback()
    {
        if (!_visible || _scheduled) return;
        _scheduled = true;
        Dispatcher.Dispatch(() =>
        {
            _scheduled = false;
            if (!_visible) return;
            bool initial = _initialPass;
            _initialPass = false;
            var roots = initial ? (Content == null ? Array.Empty<Element>() : new Element[] { Content }) : _pendingSubtrees.ToArray();
            _pendingSubtrees.Clear();
            if (initial || roots.Any(root => ReferenceEquals(root, Content))) NormalizeCanvas();
            var candidates = roots.ToHashSet();
            var visited = new HashSet<Element>();
            foreach (var root in roots)
            {
                // Adding a populated subtree also raises events for its children. Visit it once.
                bool covered = false;
                Element? ancestor = root;
                for (; ancestor != null && !ReferenceEquals(ancestor, this); ancestor = ancestor.Parent)
                {
                    if (ancestor is Page || (bool)ancestor.GetValue(PreserveColorsProperty)) { covered = true; break; }
                    if (!ReferenceEquals(ancestor, root) && candidates.Contains(ancestor)) { covered = true; break; }
                }
                if (!covered && ReferenceEquals(ancestor, this)) StyleTextTree(root, visited);
            }
        });
    }

    private void NormalizeCanvas()
    {
        // Keep the existing canvas rule; named card/media backgrounds remain intact.
        VisualElement? canvas = Content;
        while (!PreserveCanvasBackground && (canvas is Layout || canvas is ScrollView || canvas is ContentView))
        {
            if ((bool)canvas.GetValue(PreserveColorsProperty) || canvas.HasThemeColor(BackgroundColorProperty)) break;
            canvas.BackgroundColor = Colors.Transparent;
            canvas = canvas is ScrollView scroll ? scroll.Content :
                canvas is ContentView contentView ? contentView.Content :
                canvas is Layout layout && layout.Children.Count == 1 ? layout.Children[0] as VisualElement : null;
        }
    }

    private void StyleTextTree(Element element, HashSet<Element> visited)
    {
        if (element is Page || (bool)element.GetValue(PreserveColorsProperty) || !visited.Add(element)) return;
        if (element is Label label)
        {
            string? surface = null;
            bool foundSurface = false;
            string? Surface()
            {
                if (!foundSurface) { surface = TextSurface(label); foundSurface = true; }
                return surface;
            }
            if (!label.HasThemeColor(Label.TextColorProperty))
                ApplyTextTheme(label, Label.TextColorProperty, label.TextColor, label.FontSize, label.FontAttributes, Surface());
            if (label.FormattedText != null)
                foreach (var span in label.FormattedText.Spans)
                    if (visited.Add(span) && !span.HasThemeColor(Span.TextColorProperty))
                        ApplyTextTheme(span, Span.TextColorProperty, span.TextColor,
                            span.IsSet(Span.FontSizeProperty) ? span.FontSize : label.FontSize,
                            span.IsSet(Span.FontAttributesProperty) ? span.FontAttributes : label.FontAttributes,
                            !HasBackground(span.BackgroundColor) ? Surface() : null);
        }
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>()) StyleTextTree(child, visited);
    }

    private string? TextSurface(Element element)
    {
        for (Element? ancestor = element; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this)) return "Page";
            if (ancestor is Page || ancestor is Button) return null;
            if (ancestor is VisualElement visual)
            {
                if (visual.UsesThemeColor(BackgroundColorProperty, ThemeKey.CardBackground)) return "Card";
                if (HasBackground(visual.BackgroundColor) || HasBackground(visual.Background)) return null;
            }
        }
        return null;
    }

    private static bool HasBackground(Color? color) => color?.Alpha > 0;
    private static bool HasBackground(Brush? brush) => brush switch
    {
        null => false,
        SolidColorBrush solid => HasBackground(solid.Color),
        GradientBrush gradient => gradient.GradientStops.Any(stop => HasBackground(stop.Color)),
        _ => !brush.IsEmpty
    };

    private void ApplyTextTheme(Element text, BindableProperty property, Color? currentColor,
        double size, FontAttributes attributes, string? surface)
    {
        var appearance = _textAppearance.GetValue(text, _ => new TextAppearance(currentColor));
        var original = appearance.OriginalColor;
        bool saturatedCardText = surface == "Card" && original != null &&
            Math.Max(original.Red, Math.Max(original.Green, original.Blue)) - Math.Min(original.Red, Math.Min(original.Green, original.Blue)) > 0.12;
        if (surface == null || saturatedCardText)
        {
            if (appearance.ResourceKey != null)
            {
                text.RemoveDynamicResource(property);
                text.SetValue(property, original);
                appearance.ResourceKey = null;
            }
            return;
        }
        bool heading = size >= 18 || size >= 13 && attributes.HasFlag(FontAttributes.Bold);
        bool grey = original != null &&
            Math.Max(original.Red, Math.Max(original.Green, original.Blue)) - Math.Min(original.Red, Math.Min(original.Green, original.Blue)) <= 0.10 &&
            original.Red >= 0.25 && original.Red <= 0.85;
        bool secondary = !heading && (size < 13 || grey);
        string resource = surface == "Card" ? (secondary ? "ModeCardText2" : "ModeCardText1") :
            size < 13 && !heading ? "ModePageText3" : secondary ? "ModePageText2" : "ModePageText1";
        if (appearance.ResourceKey == resource) return;
        text.ClearValue(property);
        text.SetDynamicResource(property, resource);
        appearance.ResourceKey = resource;
    }
}
