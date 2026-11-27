using Bannister.Services;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;
using MauiToolbarModel = Microsoft.Maui.Controls.Toolbar;
using NativeButton = Microsoft.UI.Xaml.Controls.Button;
using NativeControl = Microsoft.UI.Xaml.Controls.Control;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using MenuBar = Microsoft.UI.Xaml.Controls.MenuBar;

namespace Bannister.Platforms.Windows;

/// <summary>Bridges the named MAUI bar title color into WinUI navigation glyphs and their visual states.</summary>
internal static class NavigationBarTheme
{
    private static readonly ConditionalWeakTable<MauiToolbar, BarTheme> Bars = new();
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;
        // Own IconColor on Windows: MAUI's mapper replaces native state resources, which
        // conflicts with live, resolved ThemeResource references. Keep our brushes stable.
        ToolbarHandler.Mapper.ModifyMapping(nameof(MauiToolbarModel.IconColor), (handler, view, _) => Apply(handler, view));
        foreach (string property in new[] { nameof(MauiToolbarModel.BarTextColor),
                     nameof(MauiToolbarModel.BarBackground), nameof(MauiToolbarModel.ToolbarItems) })
            ToolbarHandler.Mapper.AppendToMapping(property, (handler, view) =>
                Apply(handler, view));
    }

    private static void Apply(IToolbarHandler handler, IToolbar view)
    {
        if (view is MauiToolbarModel toolbar)
            Bars.GetValue(handler.PlatformView, bar => new BarTheme(bar)).Update(toolbar);
    }

    private sealed class BarTheme
    {
        private readonly MauiToolbar _bar;
        private readonly SolidColorBrush _foreground = new();
        private readonly SolidColorBrush _background = new();
        private readonly SolidColorBrush _hover = new();
        private readonly SolidColorBrush _pressed = new();
        private readonly ConditionalWeakTable<NativeControl, ButtonTheme> _buttons = new();
        private MauiToolbarModel? _toolbar;
        private NavigationView? _navigation;
        private long _templateToken;
        private bool _updating;
        private bool _queued;

        public BarTheme(MauiToolbar bar)
        {
            _bar = bar;
            bar.Loaded += (_, _) => QueueUpdate();
            bar.Unloaded += (_, _) => DetachNavigation();
        }

        public void Update(MauiToolbarModel toolbar)
        {
            _toolbar = toolbar;
            if (_updating) return;
            _updating = true;
            try
            {
                // BarTextColor comes from ModePageText1. IconColor is independent in MAUI;
                // keep it in step so later template creation also receives the right color.
                var color = toolbar.BarTextColor ?? AppColors.Get(ThemeKey.PageText1);
                if (!Equals(toolbar.IconColor, color)) toolbar.IconColor = color;
                _foreground.Color = color.ToWindowsColor();
                _background.Color = (_bar.Background as SolidColorBrush)?.Color ?? AppColors.Get(ThemeKey.PageBackground).ToWindowsColor();
                // Subtle interaction fills derive from the same named foreground; no separate palette or OS colors.
                _hover.Color = InteractionFill(_background.Color, _foreground.Color, 0.08);
                _pressed.Color = InteractionFill(_background.Color, _foreground.Color, 0.16);
                _bar.Foreground = _foreground;
                _bar.Resources["CommandBarOverflowPresenterBackground"] = _background;
                _bar.Resources["CommandBarOverflowPresenterBorderBrush"] = _pressed;
                foreach (var item in toolbar.ToolbarItems ?? Array.Empty<ToolbarItem>())
                    if (item.IconImageSource is FontImageSource font && !Equals(font.Color, color)) font.Color = color;

                foreach (var command in _bar.PrimaryCommands.Concat(_bar.SecondaryCommands).OfType<NativeControl>()) ThemeButton(command);
                ThemeToolbarParts(_bar);
                AttachNavigation();
                if (_navigation != null) ThemeNavigationParts(_navigation);
            }
            finally { _updating = false; }
        }

        private void ThemeButton(NativeControl button) => _buttons.GetValue(button, b => new ButtonTheme(this, b)).Apply();

        private static global::Windows.UI.Color InteractionFill(global::Windows.UI.Color background, global::Windows.UI.Color foreground, double amount)
        {
            double alpha = amount * foreground.A / byte.MaxValue;
            byte Blend(byte back, byte front) => (byte)Math.Round(back + (front - back) * alpha);
            return new() { A = background.A, R = Blend(background.R, foreground.R), G = Blend(background.G, foreground.G), B = Blend(background.B, foreground.B) };
        }

        private void ThemeToolbarParts(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is NativeButton { Name: "MoreButton" } more) ThemeButton(more);
                else if (child is AppBarButton appButton) ThemeButton(appButton);
                else if (child is MenuBar menu)
                {
                    SetButtonResources(menu.Resources);
                    foreach (var item in menu.Items) ThemeButton(item);
                }
                // A custom TitleView owns its contents; don't recolor arbitrary page controls.
                else if (child is FrameworkElement { Name: "titleView" }) continue;
                else ThemeToolbarParts(child);
            }
        }

        private void AttachNavigation()
        {
            NavigationView? navigation = null;
            for (DependencyObject? node = _bar; node != null; node = VisualTreeHelper.GetParent(node))
                if (node is NavigationView candidate) { navigation = candidate; break; }
            if (ReferenceEquals(navigation, _navigation)) return;
            DetachNavigation();
            _navigation = navigation;
            if (navigation == null) return;
            _templateToken = navigation.RegisterPropertyChangedCallback(NativeControl.TemplateProperty, (_, _) => QueueUpdate());
            navigation.Loaded += OnNavigationLoaded;
            navigation.ActualThemeChanged += OnNavigationThemeChanged;
        }

        private void DetachNavigation()
        {
            if (_navigation == null) return;
            _navigation.UnregisterPropertyChangedCallback(NativeControl.TemplateProperty, _templateToken);
            _navigation.Loaded -= OnNavigationLoaded;
            _navigation.ActualThemeChanged -= OnNavigationThemeChanged;
            _navigation = null;
        }

        private void OnNavigationLoaded(object sender, RoutedEventArgs args) => QueueUpdate();
        private void OnNavigationThemeChanged(FrameworkElement sender, object args) => QueueUpdate();

        private void ThemeNavigationParts(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is NativeButton button && button.Name is "NavigationViewBackButton" or "TogglePaneButton" or "NavigationViewCloseButton")
                    ThemeButton(button);
                else if (child is not NavigationView && child is not MauiToolbar &&
                         !ReferenceEquals(child, _navigation?.Content)) ThemeNavigationParts(child);
            }
        }

        private void QueueUpdate()
        {
            if (_queued) return;
            _queued = true;
            if (!_bar.DispatcherQueue.TryEnqueue(() =>
                {
                    _queued = false;
                    if (_bar.IsLoaded && _toolbar != null) Update(_toolbar);
                })) _queued = false;
        }

        private bool SetButtonResources(Microsoft.UI.Xaml.ResourceDictionary resources)
        {
            bool changed = false;
            foreach (string prefix in new[] { "NavigationViewItem", "NavigationViewButton", "Button", "AppBarButton", "CommandBarFlyoutAppBarButton" })
            {
                foreach (string suffix in new[] { "", "PointerOver", "Pressed", "Disabled", "SubMenuOpened" })
                    Put(prefix + "Foreground" + suffix, _foreground);
                Put(prefix + "BackgroundPointerOver", _hover);
                Put(prefix + "BackgroundPressed", _pressed);
            }
            Put("NavigationViewBackButtonBackground", _background);
            void Put(string key, object value)
            {
                if (resources.TryGetValue(key, out var previous) && ReferenceEquals(previous, value)) return;
                resources[key] = value; changed = true;
            }
            return changed;
        }

        private sealed class ButtonTheme
        {
            private readonly BarTheme _owner;
            private readonly NativeControl _button;
            private bool _applying;

            public ButtonTheme(BarTheme owner, NativeControl button)
            {
                _owner = owner; _button = button;
                button.Loaded += (_, _) => Apply();
                button.RegisterPropertyChangedCallback(NativeControl.TemplateProperty, (_, _) => owner.QueueUpdate());
            }

            public void Apply()
            {
                if (_applying) return;
                _applying = true;
                try
                {
                    bool refresh = _owner.SetButtonResources(_button.Resources);
                    _button.Foreground = _owner._foreground;
                    if (_button is NativeButton { Name: "NavigationViewBackButton" or "TogglePaneButton" or "NavigationViewCloseButton" })
                        _button.Background = _owner._background;
                    _button.ApplyTemplate();
                    if (refresh && _button.IsLoaded)
                    {
                        // WinUI keeps resolved ThemeResource references. Refresh once when installing
                        // scoped overrides; later updates mutate the shared brushes in place.
                        var previous = _button.RequestedTheme;
                        _button.RequestedTheme = _button.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                        _button.RequestedTheme = previous;
                    }
                    ColorGlyphs(_button);
                }
                finally { _applying = false; }
            }

            private void ColorGlyphs(DependencyObject parent)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is IconElement icon)
                    {
                        icon.Foreground = _owner._foreground;
                        if (icon is AnimatedIcon animated && animated.FallbackIconSource != null)
                            animated.FallbackIconSource.Foreground = _owner._foreground;
                    }
                    else if (child is TextBlock text) text.Foreground = _owner._foreground;
                    ColorGlyphs(child);
                }
            }
        }
    }
}
