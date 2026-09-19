using Bannister.Models;

namespace Bannister.Services;

public class SequenceTaskService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public SequenceTaskService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<SequenceTaskGroup>();
        await conn.CreateTableAsync<SequenceTaskItem>();
        _initialized = true;
    }

    public async Task<List<SequenceTaskGroup>> GetActiveGroupsAsync()
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SequenceTaskGroup>()
            .Where(g => !g.IsArchived)
            .OrderBy(g => g.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<SequenceTaskGroup>> GetAllGroupsAsync()
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SequenceTaskGroup>()
            .OrderByDescending(g => g.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveGroupAsync(SequenceTaskGroup group)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (group.Id == 0) await conn.InsertAsync(group);
        else await conn.UpdateAsync(group);
    }

    public async Task ArchiveGroupAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var group = await conn.FindAsync<SequenceTaskGroup>(id);
        if (group == null) return;
        group.IsArchived = true;
        await conn.UpdateAsync(group);
    }

    public async Task<List<SequenceTaskItem>> GetItemsAsync(int groupId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SequenceTaskItem>()
            .Where(i => i.GroupId == groupId)
            .OrderBy(i => i.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveItemAsync(SequenceTaskItem item)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (item.Id == 0) await conn.InsertAsync(item);
        else await conn.UpdateAsync(item);
    }

    public async Task UpdateItemAsync(SequenceTaskItem item)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteItemAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var item = await conn.FindAsync<SequenceTaskItem>(id);
        if (item != null) await conn.DeleteAsync(item);
    }
}
