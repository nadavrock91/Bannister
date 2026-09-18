using Bannister.Models;

namespace Bannister.Services;

public class JournalService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public JournalService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await _db.EnsureTableAsync<JournalEntry>();
    }

    public async Task<List<JournalEntry>> GetEntriesAsync(
        string username, DateTime? date = null)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var all = await conn.Table<JournalEntry>()
            .Where(e => e.Username == username)
            .ToListAsync();

        if (date.HasValue)
            all = all.Where(e =>
                e.CreatedAt.ToLocalTime().Date ==
                date.Value.Date).ToList();

        return all
            .OrderByDescending(e => e.CreatedAt)
            .ToList();
    }

    public async Task<JournalEntry> CreateAsync(
        string username, string text)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entry = new JournalEntry
        {
            Username = username,
            Text = text,
            CreatedAt = DateTime.Now
        };
        await conn.InsertAsync(entry);
        return entry;
    }

    public async Task UpdateAsync(JournalEntry entry)
    {
        await EnsureInitializedAsync();
        entry.UpdatedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(entry);
    }

    public async Task DeleteAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<JournalEntry>(id);
    }
}
