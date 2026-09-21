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
        await conn.CreateTableAsync<MotivationNote>();
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

    public async Task UpdateStrengthAsync(int sourceId, int strength)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var source = await conn.FindAsync<MotivationSource>(sourceId);
        if (source == null) return;
        source.StrengthScore = Math.Clamp(strength, 1, 100);
        source.StrengthRatedDate = DateTime.Now;
        await conn.UpdateAsync(source);
    }

    public async Task<List<MotivationNote>> GetNotesAsync(int sourceId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<MotivationNote>()
            .Where(n => n.SourceId == sourceId)
            .OrderByDescending(n => n.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveNoteAsync(MotivationNote note)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (note.Id == 0) await conn.InsertAsync(note);
        else await conn.UpdateAsync(note);
    }

    public async Task DeleteNoteAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var note = await conn.FindAsync<MotivationNote>(id);
        if (note != null) await conn.DeleteAsync(note);
    }
}
