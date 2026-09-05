using Bannister.Models;

namespace Bannister.Services;

public class HomeQuickAccessService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public HomeQuickAccessService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        if (!_db.IsReadOnly)
            await _db.EnsureTableAsync<HomeQuickAccessSetting>();
    }

    public async Task<HashSet<string>> GetPinnedButtonsAsync(string username)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            var rows = await conn.Table<HomeQuickAccessSetting>()
                .Where(s => s.Username == username)
                .ToListAsync();
            return rows
                .Select(s => s.ButtonId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    public async Task PinButtonAsync(string username, string buttonId)
    {
        await EnsureInitializedAsync();
        if (_db.IsReadOnly) return;
        try
        {
            var conn = await _db.GetConnectionAsync();
            // Avoid duplicates
            var existing = await conn.Table<HomeQuickAccessSetting>()
                .Where(s => s.Username == username && s.ButtonId == buttonId)
                .FirstOrDefaultAsync();
            if (existing != null) return;
            await conn.InsertAsync(new HomeQuickAccessSetting
            {
                Username = username,
                ButtonId = buttonId,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch { }
    }

    public async Task UnpinButtonAsync(string username, string buttonId)
    {
        await EnsureInitializedAsync();
        if (_db.IsReadOnly) return;
        try
        {
            var conn = await _db.GetConnectionAsync();
            var existing = await conn.Table<HomeQuickAccessSetting>()
                .Where(s => s.Username == username && s.ButtonId == buttonId)
                .FirstOrDefaultAsync();
            if (existing != null)
                await conn.DeleteAsync(existing);
        }
        catch { }
    }

    public async Task TogglePinAsync(string username, string buttonId)
    {
        var pinned = await GetPinnedButtonsAsync(username);
        if (pinned.Contains(buttonId))
            await UnpinButtonAsync(username, buttonId);
        else
            await PinButtonAsync(username, buttonId);
    }

    /// <summary>
    /// One-time migration: import existing Preferences value into SQLite.
    /// Safe to call repeatedly — skips if data already exists.
    /// </summary>
    public async Task MigrateFromPreferencesAsync(
        string username, string preferencesValue)
    {
        if (string.IsNullOrWhiteSpace(preferencesValue)) return;
        await EnsureInitializedAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var existing = await conn.Table<HomeQuickAccessSetting>()
            .Where(s => s.Username == username)
            .CountAsync();
        if (existing > 0) return; // Already migrated

        var ids = preferencesValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            await conn.InsertAsync(new HomeQuickAccessSetting
            {
                Username = username,
                ButtonId = id.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
