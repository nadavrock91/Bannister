using Bannister.Models;

namespace Bannister.Services;

public class DoNotService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public DoNotService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        if (!_db.IsReadOnly)
            await _db.EnsureTableAsync<DoNotItem>();
    }

    public async Task<List<DoNotItem>> GetItemsAsync(
        string username, string area)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.Table<DoNotItem>()
                .Where(i => i.Username == username && i.Area == area)
                .ToListAsync())
                .OrderBy(i => i.CreatedAt)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<DoNotItem> AddItemAsync(
        string username, string area, string text)
    {
        await EnsureInitializedAsync();
        var item = new DoNotItem
        {
            Username = username,
            Area = area,
            Text = text.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateItemAsync(DoNotItem item)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteItemAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<DoNotItem>(id);
    }
}
