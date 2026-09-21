using Bannister.Models;

namespace Bannister.Services;

public class MotivationService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public MotivationService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<MotivationSource>();
        _initialized = true;
    }

    public async Task<List<MotivationSource>> GetActiveSourcesAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<MotivationSource>()
            .Where(s => s.Username == username && !s.IsArchived)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<MotivationSource>> GetAllSourcesAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<MotivationSource>()
            .Where(s => s.Username == username)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveSourceAsync(MotivationSource source)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (source.Id == 0) await conn.InsertAsync(source);
        else await conn.UpdateAsync(source);
    }

    public async Task ArchiveSourceAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var source = await conn.FindAsync<MotivationSource>(id);
        if (source == null) return;
        source.IsArchived = true;
        await conn.UpdateAsync(source);
    }

    public async Task DeleteSourceAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var source = await conn.FindAsync<MotivationSource>(id);
        if (source != null) await conn.DeleteAsync(source);
    }
}
