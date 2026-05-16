namespace Bannister.Services;

/// <summary>
/// Central user-facing notification for blocked writes on secondary devices.
/// This is intentionally fire-and-forget so callers do not need to catch
/// exceptions at every async void UI event boundary.
/// </summary>
public static class ReadOnlyModeNotifier
{
    private static readonly object LockObj = new();
    private static DateTime _lastShownUtc = DateTime.MinValue;

    public static void ShowBlockedWriteMessage()
    {
        lock (LockObj)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastShownUtc).TotalSeconds < 2)
                return;

            _lastShownUtc = now;
        }

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var page = Application.Current?.MainPage;
                    if (page == null) return;

                    await page.DisplayAlert(
                        "Read-Only Mode",
                        "This device is in secondary read-only mode and cannot make changes. " +
                        "Make edits on the master device, then sync this device again.",
                        "OK");
                }
                catch
                {
                    // Already handling an error path; there is nothing useful to do here.
                }
            });
        }
        catch
        {
            // Ignore failures while attempting to display the friendly message.
        }
    }
}
