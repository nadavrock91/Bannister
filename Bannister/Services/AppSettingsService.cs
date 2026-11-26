using Bannister.Models;

namespace Bannister.Services;

public sealed class AppSetting
{
    [SQLite.PrimaryKey, SQLite.AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class AppSettingsService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public AppSettingsService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
            await conn.CreateTableAsync<AppSetting>();
        _initialized = true;
    }

    public async Task<string?> GetAsync(string username, string key)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var setting = await conn.Table<AppSetting>()
            .Where(x => x.Username == username && x.Key == key)
            .FirstOrDefaultAsync();
        return setting?.Value;
    }

    public async Task SetAsync(string username, string key, string value)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        var setting = await conn.Table<AppSetting>()
            .Where(x => x.Username == username && x.Key == key)
            .FirstOrDefaultAsync();
        if (setting == null)
        {
            await conn.InsertAsync(new AppSetting
            {
                Username = username,
                Key = key,
                Value = value
            });
        }
        else
        {
            setting.Value = value;
            await conn.UpdateAsync(setting);
        }
    }

    public async Task<Dictionary<string, string>> GetByPrefixAsync(string username, string prefix)
    {
        await InitAsync();
        var rows = await (await _db.GetConnectionAsync()).Table<AppSetting>()
            .Where(x => x.Username == username).ToListAsync();
        return rows.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal))
            .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Last().Value);
    }

    public bool IsReadOnly => _db.IsReadOnly;

    public async Task DeleteByPrefixAsync(string username, string prefix)
    {
        await InitAsync();
        if (_db.IsReadOnly) throw new InvalidOperationException("Colors cannot be saved on a read-only device.");
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<AppSetting>().Where(x => x.Username == username).ToListAsync();
        await conn.RunInTransactionAsync(db =>
        {
            foreach (var row in rows.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)))
                db.Delete(row);
        });
    }

    public async Task DeleteAsync(string username, string key)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM AppSetting WHERE Username = ? AND Key = ?",
            username, key);
    }
}
