using System.Security.Cryptography;
using System.Text;

namespace Bannister.Services;

public class OwnerModeService
{
    private const string OwnerPassphraseSha256 = "cd88c18112a0abbf9a4de4247571597d011f36941b07c456d443f40703ae18fc";
    private const string UnlockedStorageKey = "owner_mode_unlocked_marker";
    private const string UnlockedMarkerValue = "c6d55a30-4b83-4e7a-8d7d-4fd0f752db0a";

    public event EventHandler? StateChanged;

    public async Task<bool> IsUnlockedAsync()
    {
        try
        {
            var marker = await SecureStorage.GetAsync(UnlockedStorageKey);
            return string.Equals(marker, UnlockedMarkerValue, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryUnlockAsync(string passphrase)
    {
        var hash = ComputeSha256Hex(passphrase ?? string.Empty);
        if (!string.Equals(hash, OwnerPassphraseSha256, StringComparison.OrdinalIgnoreCase))
            return false;

        await SecureStorage.SetAsync(UnlockedStorageKey, UnlockedMarkerValue);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public Task LockAsync()
    {
        try
        {
            SecureStorage.Remove(UnlockedStorageKey);
        }
        catch
        {
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            builder.Append(b.ToString("x2"));
        return builder.ToString();
    }
}
