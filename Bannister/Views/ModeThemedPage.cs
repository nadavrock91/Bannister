using Bannister.Services;

namespace Bannister.Views;

/// <summary>Shared page canvas and surface-aware foreground resources for signed-in pages.</summary>
public class ModeThemedPage : ContentPage
{
    /// <summary>Media viewers can retain a black viewing surface inside the themed page.</summary>
    protected virtual bool PreserveCanvasBackground => false;
    private bool _visible;
    private bool _refreshScheduled;
    private bool _applying;

    public ModeThemedPage()
    {
        SetDynamicResource(BackgroundColorProperty, "ModePageBackground");
        DescendantAdded += (_, _) => ScheduleSurfaceRefresh();
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
            StyleTree(Content, false);
        }
        finally { _applying = false; }
    }

    private static void StyleTree(Element element, bool onSurface)
    {
        if (element is Page) return; // Nested/modal pages own their styling.
        bool surface = onSurface;
        if (element is VisualElement visual &&
            (visual.BackgroundColor?.Alpha > 0.1f || visual.Background is SolidColorBrush { Color.Alpha: > 0.1f }))
            surface = true;

        if (element is Label label && !surface)
        {
            label.SetDynamicResource(Label.TextColorProperty,
                label.FontAttributes.HasFlag(FontAttributes.Bold) ? "ModeForeground" : "ModeMutedForeground");
        }
        else if (element is InputView input && !onSurface)
        {
            if (input.BackgroundColor == null || input.BackgroundColor.Alpha < 0.1f)
                input.BackgroundColor = Color.FromArgb("#FAFAFA");
            if (IsLight(input.BackgroundColor))
            {
                input.TextColor = Color.FromArgb("#222222");
                input.PlaceholderColor = Color.FromArgb("#666666");
            }
        }
        else if (element is Picker picker && !onSurface)
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
                StyleTree(child, surface);
    }

    private static bool IsLight(Color? color) => color != null &&
        color.Alpha > 0.1f && color.Red * 0.2126 + color.Green * 0.7152 + color.Blue * 0.0722 > 0.6;
}
