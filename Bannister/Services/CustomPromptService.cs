using Bannister.Models;

namespace Bannister.Services;

public class CustomPromptService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public CustomPromptService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Custom prompts are read-only on secondary devices.");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
            await conn.CreateTableAsync<CustomPromptItem>();

        _initialized = true;
    }

    public async Task<List<CustomPromptItem>> GetCustomPromptsAsync(string username, string area)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CustomPromptItem>()
            .Where(p => p.Username == username && p.Area == area)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<CustomPromptItem> AddCustomPromptAsync(string username, string area, string title, string text)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var now = DateTime.Now;
        var item = new CustomPromptItem
        {
            Username = username,
            Area = area,
            Title = title,
            Text = text,
            CreatedAt = now,
            UpdatedAt = now
        };
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateCustomPromptAsync(CustomPromptItem item)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        item.UpdatedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteCustomPromptAsync(int id)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var item = await conn.Table<CustomPromptItem>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
        if (item != null)
            await conn.DeleteAsync(item);
    }
}
