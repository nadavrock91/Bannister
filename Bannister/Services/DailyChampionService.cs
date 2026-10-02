using Bannister.Models;

namespace Bannister.Services;

public class DailyChampionService
{
    private readonly DatabaseService _db;

    public DailyChampionService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<List<DailyTotal>> GetDailyTotalsAsync(string username)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username)) return new List<DailyTotal>();

        var conn = await _db.GetConnectionAsync();
        var logs = await conn.Table<ExpLog>()
            .Where(x => x.Username == username)
            .ToListAsync();

        return logs
            .GroupBy(x => x.LoggedAt.Date)
            .Select(g => new DailyTotal
            {
                Date = g.Key,
                TotalExp = g.Sum(x => x.DeltaExp)
            })
            .OrderByDescending(x => x.TotalExp)
            .ThenByDescending(x => x.Date)
            .ToList();
    }

    public async Task<int> GetTodayTotalAsync(string username)
    {
        var today = DateTime.Today;
        var totals = await GetDailyTotalsAsync(username);
        return totals.FirstOrDefault(x => x.Date.Date == today)?.TotalExp ?? 0;
    }

    public async Task<DailyTotal?> GetChampionAsync(string username)
    {
        var totals = await GetDailyTotalsAsync(username);
        return totals.FirstOrDefault();
    }

    public async Task<List<DailyTotal>> GetSameDayHistoryAsync(string username, DateTime date)
    {
        var targetDate = date.Date;
        var totals = await GetDailyTotalsAsync(username);

        return totals
            .Where(x =>
                x.Date.Date < targetDate &&
                x.Date.Month == targetDate.Month &&
                x.Date.Day == targetDate.Day)
            .OrderByDescending(x => x.TotalExp)
            .ThenByDescending(x => x.Date)
            .ToList();
    }

    private static string NormalizeUsername(string username) =>
        (username ?? "").Trim().ToLowerInvariant();
}
