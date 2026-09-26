using Bannister.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace BannisterTools;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var windowId = Microsoft.UI.Win32Interop
                    .GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow
                    .GetFromWindowId(windowId);
                var display = Microsoft.UI.Windowing.DisplayArea
                    .GetFromWindowId(
                        windowId,
                        Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                var workArea = display.WorkArea;

                appWindow.Resize(new Windows.Graphics.SizeInt32(
                    350, workArea.Height));
                appWindow.Move(new Windows.Graphics.PointInt32(
                    workArea.X + workArea.Width - 350,
                    workArea.Y));

                if (appWindow.Presenter is
                    Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                    presenter.SetBorderAndTitleBar(false, false);
                }
            }));
        });
#endif

        builder.Services.AddSingleton<DeviceModeService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
