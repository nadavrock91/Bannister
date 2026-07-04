using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class PostponedTaskService
{
    private readonly DatabaseService _db;

    public PostponedTaskService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTableAsync()
    {
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        try { await conn.CreateTableAsync<PostponedTask>(); } catch { }
    }

    private async Task<List<PostponedTask>> QueryAsync(Func<AsyncTableQuery<PostponedTask>, AsyncTableQuery<PostponedTask>> query)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await query(conn.Table<PostponedTask>()).ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<PostponedTask>();
        }
    }

    public async Task<List<PostponedTask>> GetActiveAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(t => t.Username == username && t.Status == "active"));
        return rows.OrderBy(t => t.CurrentDate).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<PostponedTask>> GetCompletedAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(t => t.Username == username && t.Status == "completed"));
        return rows.OrderByDescending(t => t.CompletedAt ?? t.CurrentDate).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<PostponedTask>> GetDisabledAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(t => t.Username == username && t.Status == "disabled"));
        return rows.OrderByDescending(t => t.DisabledAt ?? t.CurrentDate).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<PostponedTask>> GetActiveForDateAsync(string username, DateTime date)
    {
        var rows = await GetActiveAsync(username);
        return rows.Where(t => t.CurrentDate.Date == date.Date).ToList();
    }

    public async Task<PostponedTask?> GetByIdAsync(int id)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        try { return await conn.FindAsync<PostponedTask>(id); }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)) { return null; }
    }

    public async Task<int> CreateAsync(string username, string title, string description, DateTime scheduledDate)
    {
        if (_db.IsReadOnly) return 0;
        await EnsureTableAsync();

        var item = new PostponedTask
        {
            Username = username,
            Title = title.Trim(),
            Description = description.Trim(),
            CurrentDate = scheduledDate.Date,
            OriginalDate = scheduledDate.Date,
            TimesPostponed = 0,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item.Id;
    }

    public async Task<bool> PostponeAsync(int id, DateTime newDate)
    {
        if (_db.IsReadOnly) return false;
        var item = await GetByIdAsync(id);
        if (item == null || item.Status != "active") return false;

        item.CurrentDate = newDate.Date;
        item.TimesPostponed++;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<bool> MarkDoneAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        var item = await GetByIdAsync(id);
        if (item == null || item.Status != "active") return false;

        item.Status = "completed";
        item.CompletedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<bool> DisableAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        var item = await GetByIdAsync(id);
        if (item == null || item.Status != "active") return false;

        item.Status = "disabled";
        item.DisabledAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<bool> ReactivateAsync(int id, DateTime? newDate = null)
    {
        if (_db.IsReadOnly) return false;
        var item = await GetByIdAsync(id);
        if (item == null || item.Status != "disabled") return false;

        item.Status = "active";
        item.DisabledAt = null;
        if (newDate.HasValue)
            item.CurrentDate = newDate.Value.Date;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<bool> UpdateNotesAsync(int id, string notes)
    {
        if (_db.IsReadOnly) return false;
        var item = await GetByIdAsync(id);
        if (item == null) return false;

        item.Notes = notes.Trim();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        return true;
    }
}
