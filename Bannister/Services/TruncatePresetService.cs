using Bannister.Models;

namespace Bannister.Services;

public class TruncatePresetService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public TruncatePresetService(DatabaseService db)
    {
        _db = db;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        if (!_db.IsReadOnly)
            await _db.EnsureTableAsync<TruncatePreset>();
        _initialized = true;
    }

    public async Task<List<TruncatePreset>> GetPresetsAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return (await conn.Table<TruncatePreset>()
            .Where(p => p.Username == username)
            .ToListAsync())
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SavePresetAsync(TruncatePreset preset)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        preset.Username = NormalizeUsername(preset.Username);
        preset.Title = preset.Title.Trim();
        if (preset.CreatedDate == default)
            preset.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();

        if (preset.IsDefault)
        {
            var existingDefaults = await conn.Table<TruncatePreset>()
                .Where(p => p.Username == preset.Username && p.IsDefault)
                .ToListAsync();

            foreach (var existing in existingDefaults.Where(p => p.Id != preset.Id))
            {
                existing.IsDefault = false;
                await conn.UpdateAsync(existing);
            }
        }

        if (preset.Id == 0)
            await conn.InsertAsync(preset);
        else
            await conn.UpdateAsync(preset);
    }

    public async Task DeletePresetAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<TruncatePreset>(id);
    }

    public async Task<TruncatePreset?> GetDefaultPresetAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<TruncatePreset>()
            .Where(p => p.Username == username && p.IsDefault)
            .FirstOrDefaultAsync();
    }

    private static string NormalizeUsername(string username) =>
        string.IsNullOrWhiteSpace(username) ? "default" : username.Trim();
}
