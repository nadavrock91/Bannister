using Bannister.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bannister.Views;

/// <summary>Shared page canvas and surface-aware foreground resources for signed-in pages.</summary>
public class ModeThemedPage : ContentPage
{
    /// <summary>Media viewers can retain a black viewing surface inside the themed page.</summary>
    protected virtual bool PreserveCanvasBackground => false;
    private bool _visible;
    private bool _refreshScheduled;
    private bool _applying;
    private readonly HashSet<Element> _observed = new();
    private readonly ConditionalWeakTable<Element, TextAppearance> _textAppearance = new();

    private sealed class TextAppearance(Color? color)
    {
        public Color? OriginalColor { get; set; } = color;
        public bool IsThemed { get; set; }
        public string? ResourceKey { get; set; }
    }

    public ModeThemedPage()
    {
        SetDynamicResource(BackgroundColorProperty, "ModePageBackground");
        DescendantAdded += (_, _) => ScheduleSurfaceRefresh();
        DescendantRemoved += (_, _) => ScheduleSurfaceRefresh();
        Loaded += (_, _) => ScheduleSurfaceRefresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_visible) PrivacyModeService.ModeChanged += OnModeChanged;
        _visible = true;
        ApplyCurrentMode();
        ScheduleSurfaceRefresh();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        PrivacyModeService.ModeChanged -= OnModeChanged;
        foreach (var element in _observed) element.PropertyChanged -= OnElementChanged;
        _observed.Clear();
        base.OnDisappearing();
    }

    private IServiceProvider? Services => Handler?.MauiContext?.Services ??
        Application.Current?.Handler?.MauiContext?.Services;

    private void ApplyCurrentMode()
    {
        var auth = Services?.GetService<AuthService>();
        var privacy = Services?.GetService<PrivacyModeService>();
        if (auth?.IsLoggedIn != true || privacy == null) return;
        ApplyMode(privacy.GetDisplayMode(auth.CurrentUsername));
    }

    private void OnModeChanged(object? sender, DisplayModeChangedEventArgs args)
    {
        var username = Services?.GetService<AuthService>()?.CurrentUsername;
        if (!string.Equals(username, args.Username, StringComparison.OrdinalIgnoreCase)) return;
        Dispatcher.Dispatch(() => ApplyMode(args.Mode));
    }

    private void ApplyMode(ActivityDisplayMode mode)
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;
        var colors = ModeTheme.Get(mode);
        resources["ModePageBackground"] = colors.Background;
        resources["ModeForeground"] = colors.Foreground;
        resources["ModeMutedForeground"] = colors.MutedForeground;
        resources["ModeAccent"] = colors.Accent;
        RefreshSurfaces();
    }

    private void ScheduleSurfaceRefresh()
    {
        if (!_visible || _refreshScheduled) return;
        _refreshScheduled = true;
        Dispatcher.Dispatch(() =>
        {
            _refreshScheduled = false;
            if (_visible) RefreshSurfaces();
        });
    }

    private void RefreshSurfaces()
    {
        if (_applying || Content == null) return;
        _applying = true;
        try
        {
            // These are page canvases, not card surfaces. Stop at the first branching layout.
            VisualElement? canvas = Content;
            while (!PreserveCanvasBackground && (canvas is Layout || canvas is ScrollView || canvas is ContentView))
            {
                canvas.BackgroundColor = Colors.Transparent;
                canvas = canvas is ScrollView scroll ? scroll.Content :
                    canvas is ContentView contentView ? contentView.Content :
                    canvas is Layout layout && layout.Children.Count == 1 ? layout.Children[0] as VisualElement : null;
            }
            var visited = new HashSet<Element>();
            StyleTree(Content, visited);
            foreach (var detached in _observed.Except(visited).ToArray())
            {
                detached.PropertyChanged -= OnElementChanged;
                _observed.Remove(detached);
            }
        }
        finally { _applying = false; }
    }

    private void StyleTree(Element element, HashSet<Element> visited)
    {
        if (element is Page || !visited.Add(element)) return;
        if (_observed.Add(element)) element.PropertyChanged += OnElementChanged;
        bool onPage = IsOnPageBackground(element);

        if (element is Label label)
        {
            ApplyTextTheme(label, Label.TextColorProperty, label.TextColor, label.FontSize, label.FontAttributes, onPage);
            if (label.FormattedText != null)
                foreach (var span in label.FormattedText.Spans)
                {
                    if (visited.Add(span) && _observed.Add(span)) span.PropertyChanged += OnElementChanged;
                    ApplyTextTheme(span, Span.TextColorProperty, span.TextColor,
                        span.IsSet(Span.FontSizeProperty) ? span.FontSize : label.FontSize,
                        span.IsSet(Span.FontAttributesProperty) ? span.FontAttributes : label.FontAttributes,
                        onPage && !HasBackground(span.BackgroundColor));
                }
        }
        else if (element is InputView input && onPage)
        {
            if (input.BackgroundColor == null || input.BackgroundColor.Alpha < 0.1f)
                input.BackgroundColor = Color.FromArgb("#FAFAFA");
            if (IsLight(input.BackgroundColor))
            {
                input.TextColor = Color.FromArgb("#222222");
                input.PlaceholderColor = Color.FromArgb("#666666");
            }
        }
        else if (element is Picker picker && onPage)
        {
            if (picker.BackgroundColor == null || picker.BackgroundColor.Alpha < 0.1f)
                picker.BackgroundColor = Color.FromArgb("#FAFAFA");
            if (IsLight(picker.BackgroundColor))
            {
                picker.TextColor = Color.FromArgb("#222222");
                picker.TitleColor = Color.FromArgb("#666666");
            }
            // Windows renders Title as a header inside the requested height.
            if (!string.IsNullOrWhiteSpace(picker.Title) && picker.HeightRequest >= 0)
                picker.HeightRequest = Math.Max(picker.HeightRequest, 72);
        }

        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                StyleTree(child, visited);
    }

    private bool IsOnPageBackground(Element element)
    {
        for (Element? ancestor = element; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this)) return true;
            if (ancestor is Page) return false;
            if (ancestor is Button) return false;
            if (ancestor is VisualElement visual &&
                (HasBackground(visual.BackgroundColor) || HasBackground(visual.Background))) return false;
        }
        return false; // Detached elements are reconsidered after insertion.
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
        double size, FontAttributes attributes, bool onPage)
    {
        var appearance = _textAppearance.GetValue(text, _ => new TextAppearance(currentColor));
        if (onPage)
        {
            bool heading = size >= 18 || (size >= 13 && attributes.HasFlag(FontAttributes.Bold));
            var original = appearance.OriginalColor;
            bool grey = original != null &&
                Math.Max(original.Red, Math.Max(original.Green, original.Blue)) -
                Math.Min(original.Red, Math.Min(original.Green, original.Blue)) <= 0.10 &&
                original.Red >= 0.25 && original.Red <= 0.85;
            string resource = !heading && (size < 13 || grey) ? "ModeMutedForeground" : "ModeForeground";
            // MAUI 8 manual setters outrank dynamic resources. Release the local value first.
            text.ClearValue(property);
            text.SetDynamicResource(property, resource);
            appearance.IsThemed = true;
            appearance.ResourceKey = resource;
        }
        else if (appearance.IsThemed)
        {
            // A label moved into a card must regain its original surface-specific color.
            text.RemoveDynamicResource(property);
            text.SetValue(property, appearance.OriginalColor);
            appearance.IsThemed = false;
        }
    }

    private void OnElementChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_applying || !_visible || sender is not Element element) return;
        if (args.PropertyName == "TextColor" && _textAppearance.TryGetValue(element, out var appearance))
        {
            Color? color = element switch { Label label => label.TextColor, Span span => span.TextColor, _ => appearance.OriginalColor };
            // Resource notifications are not new author-specified colors. Preserve the initial hint classification.
            if (appearance.ResourceKey != null && Application.Current?.Resources.TryGetValue(appearance.ResourceKey, out var themedColor) == true &&
                Equals(color, themedColor)) return;
            appearance.OriginalColor = color;
        }
        if (args.PropertyName is "TextColor" or "FontSize" or "FontAttributes" or "FormattedText" or
            "BackgroundColor" or "Background" or "Parent" or "Content")
            ScheduleSurfaceRefresh();
    }

    private static bool IsLight(Color? color) => color != null &&
        color.Alpha > 0.1f && color.Red * 0.2126 + color.Green * 0.7152 + color.Blue * 0.0722 > 0.6;
}
