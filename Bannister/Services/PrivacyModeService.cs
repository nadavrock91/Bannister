namespace Bannister.Services;

/// <summary>
/// Three display modes stored per user in Preferences.
/// Device-local — never synced with database.
/// </summary>
public enum ActivityDisplayMode
{
    All = 0,      // Show everything (normal)
    PublicOnly = 1, // Show only IsPublic=true (demo mode)
    PrivateOnly = 2  // Show only IsPublic=false (private mode)
}

public class PrivacyModeService
{
    private const string KeyPrefix = "activity_display_mode_";

    public ActivityDisplayMode GetDisplayMode(string username)
    {
        try
        {
            int val = Preferences.Default.Get(
                KeyPrefix + username, 0);
            return val switch
            {
                1 => ActivityDisplayMode.PublicOnly,
                2 => ActivityDisplayMode.PrivateOnly,
                _ => ActivityDisplayMode.All
            };
        }
        catch { return ActivityDisplayMode.All; }
    }

    public void SetDisplayMode(
        string username, ActivityDisplayMode mode)
    {
        try
        {
            Preferences.Default.Set(
                KeyPrefix + username, (int)mode);
        }
        catch { }
    }

    public ActivityDisplayMode CycleMode(string username)
    {
        var current = GetDisplayMode(username);
        var next = current switch
        {
            ActivityDisplayMode.All => ActivityDisplayMode.PublicOnly,
            ActivityDisplayMode.PublicOnly => ActivityDisplayMode.PrivateOnly,
            ActivityDisplayMode.PrivateOnly => ActivityDisplayMode.All,
            _ => ActivityDisplayMode.All
        };
        SetDisplayMode(username, next);
        return next;
    }

    // Legacy bool compatibility for any callers still using bool API
    public bool IsPrivateModeEnabled(string username)
        => GetDisplayMode(username) != ActivityDisplayMode.All;

    public Task<bool> IsPrivateModeEnabledAsync(string username)
        => Task.FromResult(IsPrivateModeEnabled(username));

    public Task SetPrivateModeAsync(string username, bool enabled)
    {
        SetDisplayMode(username, enabled
            ? ActivityDisplayMode.PublicOnly
            : ActivityDisplayMode.All);
        return Task.CompletedTask;
    }
}
