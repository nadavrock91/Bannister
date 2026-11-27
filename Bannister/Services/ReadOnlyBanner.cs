using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Drop-in banner that shows "🔒 Read-Only Mode" when the device is in secondary mode,
/// and hides itself otherwise. Pages can place this at the top of their layout.
///
/// Usage:
///     var banner = new ReadOnlyBanner(_deviceMode);
///     mainStack.Children.Insert(0, banner);
/// </summary>
public class ReadOnlyBanner : ContentView
{
    private readonly DeviceModeService _deviceMode;
    private readonly Frame _frame;

    public ReadOnlyBanner(DeviceModeService deviceMode)
    {
        _deviceMode = deviceMode;

        _frame = (new Frame
        {
            Padding = new Thickness(12, 6),
            CornerRadius = 6,
            HasShadow = false,
            Margin = new Thickness(0, 0, 0, 8),
            Content = (new Label
            {
                Text = "🔒 Read-Only Mode — this device is in Secondary mode. Tap Settings → Sync & Devices to change.",
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningButtonBackground);

        Content = _frame;
        UpdateVisibility();

        _deviceMode.ModeChanged += (_, __) => UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        // Dispatch in case ModeChanged fires off the UI thread.
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsVisible = _deviceMode.IsReadOnly;
            });
        }
        catch
        {
            IsVisible = _deviceMode.IsReadOnly;
        }
    }
}
