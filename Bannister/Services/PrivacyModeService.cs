namespace Bannister.Services;

/// <summary>
/// Stores Private Mode state in Preferences — device-local,
/// survives app reinstalls, never synced with the database.
/// Each device keeps its own mode independently.
/// </summary>
public class PrivacyModeService
{
    private const string KeyPrefix = "privacy_mode_";

    public bool IsPrivateModeEnabled(string username)
    {
        try
        {
            return Preferences.Default.Get(
                KeyPrefix + username, false);
        }
        catch { return false; }
    }

    public void SetPrivateMode(string username, bool enabled)
    {
        try
        {
            Preferences.Default.Set(
                KeyPrefix + username, enabled);
        }
        catch { }
    }

    // Async wrappers for compatibility with existing callers
    public Task<bool> IsPrivateModeEnabledAsync(string username)
        => Task.FromResult(IsPrivateModeEnabled(username));

    public Task SetPrivateModeAsync(string username, bool enabled)
    {
        SetPrivateMode(username, enabled);
        return Task.CompletedTask;
    }
}
