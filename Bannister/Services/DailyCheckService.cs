using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Replaces Preferences.Get/Set for daily check tracking.
/// All data stored in SQLite daily_checks table so it can be
/// viewed and edited via the Databases page or Run SQL.
/// </summary>
public class DailyCheckService
{
    private readonly DatabaseService _db;
    private bool _initialized = false;

    public DailyCheckService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<DailyCheck>();
        _initialized = true;
    }

    /// <summary>
    /// Get the last checked date for a key. Returns "" if never checked.
    /// </summary>
    public async Task<string> GetAsync(string key)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entry = await conn.Table<DailyCheck>()
            .Where(d => d.Key == key)
            .FirstOrDefaultAsync();
        return entry?.LastCheckedDate ?? "";
    }

    /// <summary>
    /// Set the last checked date for a key.
    /// </summary>
    public async Task SetAsync(string key, string date)
    {
        await EnsureInitializedAsync();
        if (_db.IsReadOnly) return; // silently skip on secondary devices
        var conn = await _db.GetConnectionAsync();
        var entry = await conn.Table<DailyCheck>()
            .Where(d => d.Key == key)
            .FirstOrDefaultAsync();

        if (entry != null)
        {
            entry.LastCheckedDate = date;
            await conn.UpdateAsync(entry);
        }
        else
        {
            await conn.InsertAsync(new DailyCheck
            {
                Key = key,
                LastCheckedDate = date
            });
        }
    }

    /// <summary>
    /// Check if a key was already checked today.
    /// </summary>
    public async Task<bool> WasCheckedTodayAsync(string key)
    {
        string lastDate = await GetAsync(key);
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        return lastDate == today;
    }

    /// <summary>
    /// Mark a key as checked today.
    /// </summary>
    public async Task MarkCheckedTodayAsync(string key)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        await SetAsync(key, today);
    }

    /// <summary>
    /// Clear a specific key (reset so it will trigger again).
    /// </summary>
    public async Task ClearAsync(string key)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM daily_checks WHERE Key = ?", key);
    }

    /// <summary>
    /// Clear all checks for a user+game combo (useful for testing).
    /// </summary>
    public async Task ClearAllForGameAsync(string username, string gameId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM daily_checks WHERE Key LIKE ?",
            $"%{username}_{gameId}%");
    }

    /// <summary>
    /// Clear all daily checks (nuclear option for testing).
    /// </summary>
    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM daily_checks");
    }
}
