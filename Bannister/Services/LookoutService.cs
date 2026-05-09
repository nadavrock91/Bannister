using Bannister.Models;

namespace Bannister.Services;

public class LookoutService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public LookoutService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LookoutScenario>();
        await conn.CreateTableAsync<LookoutScenarioItem>();
        _initialized = true;
    }

    public async Task<List<LookoutScenario>> GetForDateAsync(string username, DateTime date)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<LookoutScenario>()
            .Where(l => l.Username == username)
            .ToListAsync();

        return rows
            .Where(l => l.DisplayDate.HasValue && l.DisplayDate.Value.Date == date.Date)
            .OrderBy(l => l.Title)
            .ToList();
    }

    public async Task<Dictionary<int, int>> GetCountsByDayAsync(string username, int year, int month)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<LookoutScenario>()
            .Where(l => l.Username == username)
            .ToListAsync();

        return rows
            .Where(l => l.DisplayDate.HasValue && l.DisplayDate.Value.Year == year && l.DisplayDate.Value.Month == month)
            .GroupBy(l => l.DisplayDate!.Value.Day)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<LookoutScenario> CreateAsync(string username, string title, DateTime? displayDate, string notes = "")
    {
        await EnsureInitializedAsync();
        var item = new LookoutScenario
        {
            Username = username,
            Title = title.Trim(),
            Notes = notes.Trim(),
            DisplayDate = displayDate?.Date,
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateAsync(LookoutScenario scenario)
    {
        await EnsureInitializedAsync();
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.DisplayDate = scenario.DisplayDate?.Date;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(scenario);
    }

    public async Task RemoveFromCalendarAsync(LookoutScenario scenario)
    {
        scenario.DisplayDate = null;
        await UpdateAsync(scenario);
    }

    public async Task<List<LookoutScenarioItem>> GetItemsAsync(int lookoutScenarioId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<LookoutScenarioItem>()
            .Where(i => i.LookoutScenarioId == lookoutScenarioId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<LookoutScenarioItem> AddItemAsync(int lookoutScenarioId, string title, string notes = "")
    {
        await EnsureInitializedAsync();
        var existing = await GetItemsAsync(lookoutScenarioId);
        var item = new LookoutScenarioItem
        {
            LookoutScenarioId = lookoutScenarioId,
            Title = title.Trim(),
            Notes = notes.Trim(),
            SortOrder = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder) + 1,
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateItemAsync(LookoutScenarioItem item)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteItemAsync(LookoutScenarioItem item)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(item);
    }
}
