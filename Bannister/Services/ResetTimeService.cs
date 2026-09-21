using Bannister.Models;

namespace Bannister.Services;

public class ResetTimeService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ResetTimeService(DatabaseService db)
    {
        _db = db;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
            await conn.CreateTableAsync<ResetTimeSetting>();
        _initialized = true;
    }

    public async Task<int> GetResetHour(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var setting = await conn.Table<ResetTimeSetting>()
            .Where(s => s.Username == username)
            .FirstOrDefaultAsync();
        return setting == null ? 0 : Math.Clamp(setting.ResetHour, 0, 23);
    }

    public async Task SetResetHour(string username, int hour)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        var setting = await conn.Table<ResetTimeSetting>()
            .Where(s => s.Username == username)
            .FirstOrDefaultAsync();
        hour = Math.Clamp(hour, 0, 23);
        if (setting == null)
            await conn.InsertAsync(new ResetTimeSetting { Username = username, ResetHour = hour });
        else
        {
            setting.ResetHour = hour;
            await conn.UpdateAsync(setting);
        }
    }

    public async Task<DateTime> GetNextResetDateTime(string username)
    {
        var now = DateTime.Now;
        var reset = now.Date.AddHours(await GetResetHour(username));
        return reset > now ? reset : reset.AddDays(1);
    }

    public async Task<string> GetTodayResetKey(string username)
    {
        var now = DateTime.Now;
        int hour = await GetResetHour(username);
        var date = now.Hour < hour ? now.Date.AddDays(-1) : now.Date;
        return date.ToString("yyyy-MM-dd");
    }
}
