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
