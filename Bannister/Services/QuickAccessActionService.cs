using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class QuickAccessActionService
{
    private readonly DatabaseService _db;

    public QuickAccessActionService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTableAsync()
    {
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        try { await conn.CreateTableAsync<QuickAccessAction>(); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE quick_access_actions ADD COLUMN PromptType TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE quick_access_actions ADD COLUMN PromptText TEXT"); } catch { }
    }

    public async Task<List<QuickAccessAction>> GetAllAsync(string username)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<QuickAccessAction>()
                .Where(a => a.Username == username)
                .OrderBy(a => a.SortOrder)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<QuickAccessAction>();
        }
    }

    public async Task<int> CreateAsync(
        string username,
        string title,
        string actionType,
        string filePath,
        string? promptType = null,
        string? promptText = null)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return 0;
        var conn = await _db.GetConnectionAsync();

        var existingMax = 0;
        try
        {
            var last = await conn.Table<QuickAccessAction>()
                .Where(a => a.Username == username)
                .OrderByDescending(a => a.SortOrder)
                .FirstOrDefaultAsync();
            if (last != null) existingMax = last.SortOrder;
        }
        catch { }

        var action = new QuickAccessAction
        {
            Username = username,
            Title = title,
            ActionType = actionType,
            FilePath = filePath,
            PromptType = promptType,
            PromptText = promptText,
            SortOrder = existingMax + 1
        };
        await conn.InsertAsync(action);
        return action.Id;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.DeleteAsync<QuickAccessAction>(id);
        return rows > 0;
    }

    public async Task<bool> MoveUpAsync(int actionId)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();

        var current = await conn.Table<QuickAccessAction>().FirstOrDefaultAsync(a => a.Id == actionId);
        if (current == null) return false;

        var above = await conn.Table<QuickAccessAction>()
            .Where(a => a.Username == current.Username && a.SortOrder < current.SortOrder)
            .OrderByDescending(a => a.SortOrder)
            .FirstOrDefaultAsync();

        if (above == null) return false;

        var tmp = current.SortOrder;
        current.SortOrder = above.SortOrder;
        above.SortOrder = tmp;
        await conn.UpdateAsync(current);
        await conn.UpdateAsync(above);
        return true;
    }

    public async Task<bool> MoveDownAsync(int actionId)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();

        var current = await conn.Table<QuickAccessAction>().FirstOrDefaultAsync(a => a.Id == actionId);
        if (current == null) return false;

        var below = await conn.Table<QuickAccessAction>()
            .Where(a => a.Username == current.Username && a.SortOrder > current.SortOrder)
            .OrderBy(a => a.SortOrder)
            .FirstOrDefaultAsync();

        if (below == null) return false;

        var tmp = current.SortOrder;
        current.SortOrder = below.SortOrder;
        below.SortOrder = tmp;
        await conn.UpdateAsync(current);
        await conn.UpdateAsync(below);
        return true;
    }
}
