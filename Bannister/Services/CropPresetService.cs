using Bannister.Models;

namespace Bannister.Services;

public class CropPresetService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public CropPresetService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        if (!_db.IsReadOnly)
            await _db.EnsureTableAsync<CropPresetItem>();
    }

    public async Task<List<CropPresetItem>> GetPresetsAsync(string username)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.Table<CropPresetItem>()
                .Where(p => p.Username == username)
                .ToListAsync())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<CropPresetItem> AddPresetAsync(
        string username, string name, int w, int h)
    {
        await EnsureInitializedAsync();
        var item = new CropPresetItem
        {
            Username = username,
            Name = name.Trim(),
            W = w,
            H = h,
            CreatedAt = DateTime.UtcNow
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task DeletePresetAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<CropPresetItem>(id);
    }

    public async Task UpsertPresetAsync(
        string username, string name, int w, int h)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        // Find existing preset with same name for this user
        var existing = (await conn.Table<CropPresetItem>()
            .Where(p => p.Username == username)
            .ToListAsync())
            .FirstOrDefault(p => p.Name.Equals(
                name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.W = w;
            existing.H = h;
            await conn.UpdateAsync(existing);
        }
        else
        {
            await AddPresetAsync(username, name, w, h);
        }
    }
}
