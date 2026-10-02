using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class SyncCounterService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public SyncCounterService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _db.EnsureTableAsync<SyncCounter>();
        _initialized = true;
    }

    public async Task<int> GetCounterAsync(string username)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username)) return 0;

        await InitAsync();
        var conn = await _db.GetConnectionAsync();

        try
        {
            var counter = await conn.Table<SyncCounter>()
                .Where(x => x.Username == username)
                .FirstOrDefaultAsync();
            return counter?.Counter ?? 0;
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return 0;
        }
    }

    public async Task<int> IncrementAsync(string username)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username)) return 0;

        await InitAsync();
        if (_db.IsReadOnly)
        {
            ReadOnlyModeNotifier.ShowBlockedWriteMessage();
            return await GetCounterAsync(username);
        }

        var conn = await _db.GetConnectionAsync();
        var counter = await conn.Table<SyncCounter>()
            .Where(x => x.Username == username)
            .FirstOrDefaultAsync();

        if (counter == null)
        {
            counter = new SyncCounter
            {
                Username = username,
                Counter = 1,
                LastIncrementedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(counter);
            return counter.Counter;
        }

        counter.Counter++;
        counter.LastIncrementedAt = DateTime.UtcNow;
        await conn.UpdateAsync(counter);
        return counter.Counter;
    }

    private static string NormalizeUsername(string username) =>
        (username ?? "").Trim().ToLowerInvariant();

    private static bool IsMissingTable(SQLiteException ex) =>
        ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
}
