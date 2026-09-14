namespace Bannister.Services;

/// <summary>
/// Stores per-user Private Mode state in SecureStorage.
/// When Private Mode is on, only IsPublic=true activities show.
/// </summary>
public class PrivacyModeService
{
    private const string KeyPrefix = "privacy_mode_";

    public async Task<bool> IsPrivateModeEnabledAsync(
        string username)
    {
        try
        {
            var val = await SecureStorage.GetAsync(
                KeyPrefix + username);
            return val == "1";
        }
        catch { return false; }
    }

    public async Task SetPrivateModeAsync(
        string username, bool enabled)
    {
        try
        {
            await SecureStorage.SetAsync(
                KeyPrefix + username,
                enabled ? "1" : "0");
        }
        catch { }
    }
}
