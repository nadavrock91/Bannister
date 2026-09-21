using Bannister.Models;

namespace Bannister.Services;

public sealed class ResetResult
{
    public bool WasReset { get; init; }
    public bool AllowanceIncreased { get; init; }
    public bool AllowanceLost { get; init; }
    public int NewAllowance { get; init; }
}

public class DailyDeadlineService
{
    private readonly DatabaseService _db;
    private readonly ResetTimeService _resetTime;
    private bool _initialized;

    public DailyDeadlineService(DatabaseService db, ResetTimeService resetTime)
    {
        _db = db;
        _resetTime = resetTime;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<DailyDeadlineItem>();
            await conn.CreateTableAsync<DailyDeadlineLog>();
            await conn.CreateTableAsync<DailyDeadlineState>();
        }
        _initialized = true;
    }

    public async Task<DailyDeadlineState> GetStateAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var state = await conn.Table<DailyDeadlineState>()
            .Where(s => s.Username == username).FirstOrDefaultAsync();
        if (state != null) return state;
        state = new DailyDeadlineState { Username = username };
        if (!_db.IsReadOnly) await conn.InsertAsync(state);
        return state;
    }

    public async Task SaveStateAsync(DailyDeadlineState state)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        if (state.Id == 0) await conn.InsertAsync(state);
        else await conn.UpdateAsync(state);
    }

    public async Task<List<DailyDeadlineItem>> GetActiveItemsAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DailyDeadlineItem>()
            .Where(i => i.Username == username && i.IsActive)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync();
    }

    public async Task<List<DailyDeadlineItem>> GetPossibleItemsAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DailyDeadlineItem>()
            .Where(i => i.Username == username && !i.IsActive)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync();
    }

    public async Task SaveItemAsync(DailyDeadlineItem item)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        if (item.Id == 0) await conn.InsertAsync(item);
        else await conn.UpdateAsync(item);
    }

    public async Task DeleteItemAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<DailyDeadlineItem>(id);
    }

    public async Task<DailyDeadlineLog?> GetCurrentLogAsync(string username)
    {
        await InitAsync();
        return await GetLogAsync(username, await _resetTime.GetTodayResetKey(username));
    }

    private async Task<DailyDeadlineLog?> GetLogAsync(string username, string key)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DailyDeadlineLog>()
            .Where(l => l.Username == username && l.LogDate == key)
            .FirstOrDefaultAsync();
    }

    public async Task SaveLogAsync(DailyDeadlineLog log)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        if (log.Id == 0) await conn.InsertAsync(log);
        else await conn.UpdateAsync(log);
    }

    public async Task<ResetResult> CheckAndResetAsync(string username)
    {
        var state = await GetStateAsync(username);
        var currentKey = await _resetTime.GetTodayResetKey(username);
        if (state.LastResetKey == currentKey)
            return new ResetResult { NewAllowance = state.Allowance };

        bool completed = false;
        if (!string.IsNullOrWhiteSpace(state.LastResetKey))
        {
            var previous = await GetLogAsync(username, state.LastResetKey);
            completed = previous?.AllCompleted == true;
        }

        bool increased = false, lost = false;
        if (completed)
        {
            state.ConsecutiveStreak++;
            if (state.ConsecutiveStreak >= 3)
            {
                state.Allowance++;
                state.ConsecutiveStreak = 0;
                increased = true;
            }
        }
        else
        {
            state.ConsecutiveStreak = 0;
            if (state.Allowance > 1)
            {
                state.Allowance--;
                lost = true;
            }
        }
        state.LastResetKey = currentKey;
        await SaveStateAsync(state);
        return new ResetResult
        {
            WasReset = true,
            AllowanceIncreased = increased,
            AllowanceLost = lost,
            NewAllowance = state.Allowance
        };
    }
}
