using Bannister.Models;

namespace Bannister.Services;

public class LifePathService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public LifePathService(DatabaseService db) => _db = db;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await _db.EnsureTableAsync<LifePathBlock>();
    }

    public async Task<List<LifePathBlock>> GetBlocksForGameAsync(
        string username, string gameId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<LifePathBlock>()
            .Where(b => b.Username == username && b.GameId == gameId)
            .OrderBy(b => b.SortOrder).ToListAsync();
    }

    public async Task<List<LifePathBlock>> GetAllBlocksAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<LifePathBlock>()
            .Where(b => b.Username == username)
            .OrderBy(b => b.SortOrder).ToListAsync();
    }

    public async Task<LifePathBlock> CreateAsync(string username,
        string gameId, string label, DateTime startDate,
        DateTime? endDate = null, string reason = "",
        int? parentBlockId = null, int level = 1, string colorHex = "")
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var existing = await conn.Table<LifePathBlock>()
            .Where(b => b.Username == username && b.GameId == gameId)
            .ToListAsync();
        var block = new LifePathBlock
        {
            Username = username, GameId = gameId, Label = label,
            StartDate = startDate, EndDate = endDate, Reason = reason,
            ParentBlockId = parentBlockId, Level = level, ColorHex = colorHex,
            SortOrder = existing.Count == 0 ? 1 : existing.Max(b => b.SortOrder) + 1,
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(block);
        return block;
    }

    public async Task UpdateAsync(LifePathBlock block)
    {
        await EnsureInitializedAsync();
        await (await _db.GetConnectionAsync()).UpdateAsync(block);
    }

    public async Task DeleteAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var children = await conn.Table<LifePathBlock>()
            .Where(b => b.ParentBlockId == id).ToListAsync();
        foreach (var child in children)
            await conn.DeleteAsync<LifePathBlock>(child.Id);
        await conn.DeleteAsync<LifePathBlock>(id);
    }
}
