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
    private readonly ConditionalWeakTable<VisualElement, SurfaceAppearance> _surfaces = new();
    private int _themeRequest;

    // Theme editor previews/swatches own their colors and must not be reclassified by the page.
    public static readonly BindableProperty PreserveColorsProperty = BindableProperty.CreateAttached(
        "PreserveColors", typeof(bool), typeof(ModeThemedPage), false);
    public static void SetPreserveColors(BindableObject element, bool value) => element.SetValue(PreserveColorsProperty, value);

    private sealed class SurfaceAppearance(Color? original)
    {
        public Color? Original { get; set; } = original;
        public string? ResourceKey { get; set; }
        public bool WasPrimaryButton { get; set; }
    }

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
        _ = ApplyCurrentModeAsync();
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

    private async Task ApplyCurrentModeAsync()
    {
        int request = ++_themeRequest;
        var auth = Services?.GetService<AuthService>();
        var privacy = Services?.GetService<PrivacyModeService>();
        if (auth?.IsLoggedIn != true || privacy == null) return;
        var mode = privacy.GetDisplayMode(auth.CurrentUsername);
        try
        {
            var settings = Services?.GetService<AppSettingsService>();
            var colors = settings == null ? ModeTheme.Get(mode) : await ModeTheme.GetAsync(mode, auth.CurrentUsername, settings);
            if (request == _themeRequest && _visible && auth.IsLoggedIn && privacy.GetDisplayMode(auth.CurrentUsername) == mode)
                ApplyMode(colors);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[THEME] Could not load colors: {ex}");
            if (request == _themeRequest && _visible) ApplyMode(ModeTheme.Get(mode));
        }
    }

    private void OnModeChanged(object? sender, DisplayModeChangedEventArgs args)
    {
        var username = Services?.GetService<AuthService>()?.CurrentUsername;
        if (!string.Equals(username, args.Username, StringComparison.OrdinalIgnoreCase)) return;
        Dispatcher.Dispatch(() => _ = ApplyCurrentModeAsync());
    }

    private void ApplyMode(ModeThemeColors colors)
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;
        foreach (var token in colors.Tokens) resources["Mode" + token.Key] = token.Value;
        resources["ModePageText1"] = colors.Text1;
        resources["ModePageText2"] = colors.Text2;
        resources["ModePageText3"] = colors.Text3;
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
                if (canvas.HasThemeColor(BackgroundColorProperty)) break;
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
        if (element is Page || (bool)element.GetValue(PreserveColorsProperty) || !visited.Add(element)) return;
        if (_observed.Add(element)) element.PropertyChanged += OnElementChanged;
        ThemeColorExtensions.RefreshThemeColors(element);
        if (element is VisualElement visual && !element.HasThemeColor(BackgroundColorProperty)) ThemeSurface(visual);
        string? textSurface = TextSurface(element);

        if (element is Label label)
        {
            if (!label.HasThemeColor(Label.TextColorProperty))
                ApplyTextTheme(label, Label.TextColorProperty, label.TextColor, label.FontSize, label.FontAttributes, textSurface);
            if (label.FormattedText != null)
                foreach (var span in label.FormattedText.Spans)
                {
                    if (visited.Add(span) && _observed.Add(span)) span.PropertyChanged += OnElementChanged;
                    ThemeColorExtensions.RefreshThemeColors(span);
                    if (!span.HasThemeColor(Span.TextColorProperty)) ApplyTextTheme(span, Span.TextColorProperty, span.TextColor,
                        span.IsSet(Span.FontSizeProperty) ? span.FontSize : label.FontSize,
                        span.IsSet(Span.FontAttributesProperty) ? span.FontAttributes : label.FontAttributes,
                        !HasBackground(span.BackgroundColor) ? textSurface : null);
                }
        }
        else if (element is InputView input && (input.UsesThemeColor(BackgroundColorProperty, ThemeKey.InputBackground) ||
            _surfaces.TryGetValue(input, out var inputSurface) && inputSurface.ResourceKey == "ModeInputBackground"))
        {
            if (!input.HasThemeColor(InputView.TextColorProperty)) BindColor(input, InputView.TextColorProperty, "ModeInputText");
            if (!input.HasThemeColor(InputView.PlaceholderColorProperty)) BindColor(input, InputView.PlaceholderColorProperty, "ModeInputText");
        }
        else if (element is Picker picker && (picker.UsesThemeColor(BackgroundColorProperty, ThemeKey.InputBackground) ||
            _surfaces.TryGetValue(picker, out var pickerSurface) && pickerSurface.ResourceKey == "ModeInputBackground"))
        {
            if (!picker.HasThemeColor(Picker.TextColorProperty)) BindColor(picker, Picker.TextColorProperty, "ModeInputText");
            if (!picker.HasThemeColor(Picker.TitleColorProperty)) BindColor(picker, Picker.TitleColorProperty, "ModeInputText");
            // Windows renders Title as a header inside the requested height.
            if (!string.IsNullOrWhiteSpace(picker.Title) && picker.HeightRequest >= 0)
                picker.HeightRequest = Math.Max(picker.HeightRequest, 72);
        }

        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                StyleTree(child, visited);
    }

    private static void BindColor(Element element, BindableProperty property, string key)
    {
        element.ClearValue(property);
        element.SetDynamicResource(property, key);
    }

    private static bool IsNeutralNearWhite(Color? color) => color != null && color.Alpha >= 0.99 &&
        Math.Min(color.Red, Math.Min(color.Green, color.Blue)) >= 0.94 &&
        Math.Max(color.Red, Math.Max(color.Green, color.Blue)) - Math.Min(color.Red, Math.Min(color.Green, color.Blue)) <= 0.02;

    private void ThemeSurface(VisualElement element)
    {
        var surface = _surfaces.GetValue(element, view => new SurfaceAppearance(view.BackgroundColor));
        string? key = surface.ResourceKey;
        if (key == null && (element.Background == null || element.Background.IsEmpty))
        {
            if ((element is Frame || element is Border) && IsNeutralNearWhite(surface.Original)) key = "ModeCardBackground";
            else if ((element is InputView || element is Picker || element is DatePicker || element is TimePicker) &&
                (!HasBackground(surface.Original) || IsNeutralNearWhite(surface.Original))) key = "ModeInputBackground";
            else if (element is Button button && Equals(surface.Original, Color.FromArgb("#1565C0")) &&
                (Equals(button.TextColor, Colors.White) || surface.WasPrimaryButton))
                key = "ModeButtonPrimaryBackground";
        }
        if (key == null) return;
        surface.ResourceKey = key;
        if (key == "ModeButtonPrimaryBackground") surface.WasPrimaryButton = true;
        BindColor(element, VisualElement.BackgroundColorProperty, key);
        if (element is Button primary && !primary.HasThemeColor(Button.TextColorProperty)) BindColor(primary, Button.TextColorProperty, "ModeButtonPrimaryText");
        if (element is DatePicker date && !date.HasThemeColor(DatePicker.TextColorProperty)) BindColor(date, DatePicker.TextColorProperty, "ModeInputText");
        if (element is TimePicker time && !time.HasThemeColor(TimePicker.TextColorProperty)) BindColor(time, TimePicker.TextColorProperty, "ModeInputText");
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
                if (_surfaces.TryGetValue(visual, out var surface) && surface.ResourceKey == "ModeCardBackground") return "Card";
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
        if (surface != null)
        {
            bool heading = size >= 18 || (size >= 13 && attributes.HasFlag(FontAttributes.Bold));
            var original = appearance.OriginalColor;
            bool grey = original != null &&
                Math.Max(original.Red, Math.Max(original.Green, original.Blue)) -
                Math.Min(original.Red, Math.Min(original.Green, original.Blue)) <= 0.10 &&
                original.Red >= 0.25 && original.Red <= 0.85;
            bool secondary = !heading && (size < 13 || grey);
            // Preserve deliberate status/action colors inside otherwise plain cards.
            if (surface == "Card" && original != null &&
                Math.Max(original.Red, Math.Max(original.Green, original.Blue)) - Math.Min(original.Red, Math.Min(original.Green, original.Blue)) > 0.12)
            {
                if (appearance.IsThemed)
                {
                    text.RemoveDynamicResource(property);
                    text.SetValue(property, original);
                    appearance.IsThemed = false;
                    appearance.ResourceKey = null;
                }
                return;
            }
            string resource = surface == "Card" ? (secondary ? "ModeCardText2" : "ModeCardText1") :
                size < 13 && !heading ? "ModeText3" : secondary ? "ModeText2" : "ModeText1";
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
        if (args.PropertyName == "BackgroundColor" && element is VisualElement visual &&
            _surfaces.TryGetValue(visual, out var surface))
        {
            if (surface.ResourceKey != null && Application.Current?.Resources.TryGetValue(surface.ResourceKey, out var themedBackground) == true &&
                Equals(visual.BackgroundColor, themedBackground)) return;
            surface.Original = visual.BackgroundColor;
            surface.ResourceKey = null;
        }
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

}
