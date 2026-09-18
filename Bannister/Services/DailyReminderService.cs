using Bannister.Models;

namespace Bannister.Services;

public class DailyReminderService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public DailyReminderService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await _db.EnsureTableAsync<DailyReminder>();
    }

    public async Task<List<DailyReminder>> GetAllAsync(
        string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DailyReminder>()
            .Where(r => r.Username == username
                && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();
    }

    public async Task<DailyReminder> CreateAsync(
        string username, string title, string notes = "")
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        int maxSort = 0;
        var existing = await conn.Table<DailyReminder>()
            .Where(r => r.Username == username)
            .ToListAsync();
        if (existing.Count > 0)
            maxSort = existing.Max(r => r.SortOrder);
        var reminder = new DailyReminder
        {
            Username = username,
            Title = title,
            Notes = notes,
            IsActive = true,
            SortOrder = maxSort + 1,
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(reminder);
        return reminder;
    }

    public async Task UpdateAsync(DailyReminder reminder)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(reminder);
    }

    public async Task DeleteAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<DailyReminder>(id);
    }
}
