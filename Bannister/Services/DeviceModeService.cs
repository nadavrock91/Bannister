namespace Bannister.Services;

/// <summary>
/// Tracks whether this device is the "master" (read/write) or "secondary" (read-only).
/// The mode is stored in Preferences (per-device) — NOT in the database — so that the
/// flag survives across logins and so that switching a device between roles never
/// depends on the encrypted DB being openable.
///
/// Server URL is stored in Preferences too; server credentials live in SecureStorage.
/// </summary>
public class DeviceModeService
{
    public enum Mode { Master, Secondary }

    private const string PrefMode = "device_mode";
    private const string PrefServerUrl = "sync_server_url";
    private const string PrefLastSyncUtc = "sync_last_utc";
    private const string SecureSyncUser = "sync_username";
    private const string SecureSyncPasswordHash = "sync_password_hash";

    /// <summary>
    /// Fired when the mode changes. UI can subscribe to refresh banners, etc.
    /// </summary>
    public event EventHandler<Mode>? ModeChanged;

    /// <summary>
    /// Current mode for this device. Defaults to Master on first run.
    /// </summary>
    public Mode CurrentMode
    {
        get
        {
            var raw = Preferences.Default.Get(PrefMode, nameof(Mode.Master));
            return Enum.TryParse<Mode>(raw, out var m) ? m : Mode.Master;
        }
        private set
        {
            Preferences.Default.Set(PrefMode, value.ToString());
        }
    }

    /// <summary>
    /// True when this device is read-only. Single source of truth for the rest of the app.
    /// </summary>
    public bool IsReadOnly => CurrentMode == Mode.Secondary;

    /// <summary>
    /// Server URL for syncing. Empty string if unset.
    /// </summary>
    public string ServerUrl
    {
        get => Preferences.Default.Get(PrefServerUrl, "");
        set => Preferences.Default.Set(PrefServerUrl, value ?? "");
    }

    /// <summary>
    /// Last successful sync time (UTC). Null if never synced.
    /// </summary>
    public DateTime? LastSyncUtc
    {
        get
        {
            var ticks = Preferences.Default.Get(PrefLastSyncUtc, 0L);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
        set
        {
            Preferences.Default.Set(PrefLastSyncUtc, value?.Ticks ?? 0L);
        }
    }

    /// <summary>
    /// Set the sync server credentials. The password hash is stored — NOT the password
    /// itself — and it's separate from the SQLCipher key. The server never sees plaintext
    /// data because the .db file is already SQLCipher-encrypted before upload.
    /// </summary>
    public async Task SetSyncCredentialsAsync(string username, string password)
    {
        await SecureStorage.SetAsync(SecureSyncUser, username ?? "");
        // Simple deterministic hash for transport. The .db itself is SQLCipher-encrypted,
        // so this hash is only protecting access to the storage slot, not the data.
        var hash = HashForTransport(password ?? "");
        await SecureStorage.SetAsync(SecureSyncPasswordHash, hash);
    }

    public async Task<(string username, string passwordHash)> GetSyncCredentialsAsync()
    {
        var u = await SecureStorage.GetAsync(SecureSyncUser) ?? "";
        var h = await SecureStorage.GetAsync(SecureSyncPasswordHash) ?? "";
        return (u, h);
    }

    public async Task SetSyncCredentialHashAsync(string username, string passwordHash)
    {
        await SecureStorage.SetAsync(SecureSyncUser, username ?? "");
        await SecureStorage.SetAsync(SecureSyncPasswordHash, passwordHash ?? "");
    }

    public void ClearSyncCredentials()
    {
        SecureStorage.Remove(SecureSyncUser);
        SecureStorage.Remove(SecureSyncPasswordHash);
    }

    public bool HasSyncCredentials
    {
        get
        {
            // SecureStorage doesn't have a synchronous "exists" check; we cache an async-set marker.
            // For simplicity, callers should use GetSyncCredentialsAsync and check for empty.
            return !string.IsNullOrEmpty(ServerUrl);
        }
    }

    public static string GetDeviceId()
    {
        var id = Preferences.Default.Get("bannister_device_id", "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("D");
            Preferences.Default.Set("bannister_device_id", id);
        }
        return id;
    }

    /// <summary>
    /// Switch this device's mode. Caller is responsible for reinitializing DatabaseService
    /// afterwards (the connection must be reopened with the new flags).
    /// </summary>
    public void SetMode(Mode newMode)
    {
        if (CurrentMode == newMode) return;
        CurrentMode = newMode;
        ModeChanged?.Invoke(this, newMode);
    }

    /// <summary>
    /// Hash a credential for transport. Uses PBKDF2 with a fixed app-wide salt — the goal
    /// is not to protect the password (the DB itself is SQLCipher-encrypted) but to avoid
    /// sending the plaintext over the wire. Use HTTPS to actually protect this.
    /// </summary>
    public static string HashForTransport(string password)
    {
        // Fixed salt: this is a deterministic transport hash, not a credential store.
        var salt = System.Text.Encoding.UTF8.GetBytes("bannister-sync-v1");
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            password, salt, 50_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }
}
