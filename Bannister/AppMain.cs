using Bannister.Services;

namespace Bannister;

public class AppMain : Application
{
    public AppMain()
    {
        MainPage = new MainShell();

        // Hook every unhandled-exception channel we can find. SQLite "readonly"
        // errors on secondary devices flow through async void event handlers,
        // and different platforms surface those through different channels.

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;

        // Java-side default uncaught handler. .NET for Android sometimes routes
        // managed exceptions through the Java thread and this is the only place
        // they appear.
        Java.Lang.Thread.DefaultUncaughtExceptionHandler =
            new AndroidUncaughtHandler(this);
#endif
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex && ReadOnlyDatabaseException.IsReadOnlyError(ex))
        {
            TryShowReadOnlyAlert();
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (ReadOnlyDatabaseException.IsReadOnlyError(e.Exception))
        {
            e.SetObserved();
            TryShowReadOnlyAlert();
        }
    }

#if ANDROID
    private void OnAndroidUnhandledException(object? sender, Android.Runtime.RaiseThrowableEventArgs e)
    {
        if (e.Exception != null && ReadOnlyDatabaseException.IsReadOnlyError(e.Exception))
        {
            e.Handled = true;
            TryShowReadOnlyAlert();
        }
    }

    /// <summary>
    /// Java-side uncaught exception handler. Catches anything that escaped the
    /// managed channels. Installed on the Java Thread directly.
    /// </summary>
    private class AndroidUncaughtHandler : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
    {
        private readonly AppMain _app;
        private readonly Java.Lang.Thread.IUncaughtExceptionHandler? _previous;

        public AndroidUncaughtHandler(AppMain app)
        {
            _app = app;
            _previous = Java.Lang.Thread.DefaultUncaughtExceptionHandler;
        }

        public void UncaughtException(Java.Lang.Thread t, Java.Lang.Throwable e)
        {
            string msg = e?.Message?.ToLowerInvariant() ?? "";
            if (msg.Contains("readonly") || msg.Contains("read-only") || msg.Contains("read only"))
            {
                _app.TryShowReadOnlyAlert();
                return; // do NOT delegate — that would crash the process
            }

            // For anything else, defer to the original handler so we don't mask real bugs.
            _previous?.UncaughtException(t, e);
        }
    }
#endif

    internal void TryShowReadOnlyAlert()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var page = Application.Current?.MainPage;
                    if (page != null)
                    {
                        await page.DisplayAlert(
                            "🔒 Read-Only Mode",
                            "This device is in read-only (secondary) mode and cannot make changes. " +
                            "Switch to master device, or change this device's mode in Settings → Sync & Devices.",
                            "OK");
                    }
                }
                catch { /* swallow — already in error path */ }
            });
        }
        catch { /* nothing to do */ }
    }

    protected override void OnStart()
    {
        base.OnStart();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
    }

    protected override void OnResume()
    {
        base.OnResume();
    }
}
