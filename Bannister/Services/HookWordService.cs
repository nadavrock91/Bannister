using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class HookWordService
{
    private readonly DatabaseService _db;

    public HookWordService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTableAsync()
    {
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        try { await conn.CreateTableAsync<HookWord>(); } catch { }
    }

    private static string NormalizeWord(string? word) =>
        (word ?? "").Trim().ToLowerInvariant();

    private async Task<List<HookWord>> QueryAsync(Func<AsyncTableQuery<HookWord>, AsyncTableQuery<HookWord>> query)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await query(conn.Table<HookWord>()).ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<HookWord>();
        }
    }

    public async Task<List<HookWord>> GetActiveAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(w => w.Username == username && w.Status == "active"));
        return rows.OrderBy(w => w.Word, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<HookWord>> GetRemovedAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(w => w.Username == username && w.Status == "removed"));
        return rows.OrderBy(w => w.Word, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<HookWord>> GetAllAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(w => w.Username == username));
        return rows.OrderBy(w => w.Word, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<string>> GetActiveWordsAsync(string username)
    {
        var rows = await GetActiveAsync(username);
        return rows.Select(w => w.Word).ToList();
    }

    public async Task<int> CountActiveAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(w => w.Username == username && w.Status == "active"));
        return rows.Count;
    }

    public async Task<int> CountRemovedAsync(string username)
    {
        var rows = await QueryAsync(q => q.Where(w => w.Username == username && w.Status == "removed"));
        return rows.Count;
    }

    public async Task<(int added, int skippedDuplicate, int skippedRemoved)> BulkAddAsync(string username, IEnumerable<string> words)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return (0, 0, 0);

        var normalizedWords = words
            .Select(NormalizeWord)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedWords.Count == 0) return (0, 0, 0);

        var conn = await _db.GetConnectionAsync();
        var existingRows = await GetAllAsync(username);
        var existing = existingRows.ToDictionary(w => w.Word, w => w.Status, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var skippedActive = 0;
        var skippedRemoved = 0;

        foreach (var word in normalizedWords)
        {
            if (existing.TryGetValue(word, out var status))
            {
                if (status == "removed")
                    skippedRemoved++;
                else
                    skippedActive++;
                continue;
            }

            await conn.InsertAsync(new HookWord
            {
                Username = username,
                Word = word,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            });
            existing[word] = "active";
            added++;
        }

        return (added, skippedActive, skippedRemoved);
    }

    public async Task<bool> RemoveWordAsync(int id)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;
        var conn = await _db.GetConnectionAsync();
        var row = await conn.FindAsync<HookWord>(id);
        if (row == null) return false;
        row.Status = "removed";
        await conn.UpdateAsync(row);
        return true;
    }

    public async Task<bool> RestoreWordAsync(int id)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;
        var conn = await _db.GetConnectionAsync();
        var row = await conn.FindAsync<HookWord>(id);
        if (row == null) return false;
        row.Status = "active";
        await conn.UpdateAsync(row);
        return true;
    }

    public async Task<bool> DeletePermanentlyAsync(int id)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.DeleteAsync<HookWord>(id);
        return rows > 0;
    }

    public async Task<bool> BulkRemoveAsync(List<int> ids)
    {
        var changed = false;
        foreach (var id in ids.Distinct())
            changed |= await RemoveWordAsync(id);
        return changed;
    }

    public async Task<bool> BulkRestoreAsync(List<int> ids)
    {
        var changed = false;
        foreach (var id in ids.Distinct())
            changed |= await RestoreWordAsync(id);
        return changed;
    }

    public async Task<bool> BulkDeleteAsync(List<int> ids)
    {
        var changed = false;
        foreach (var id in ids.Distinct())
            changed |= await DeletePermanentlyAsync(id);
        return changed;
    }
}
