using Bannister.Models;

namespace Bannister.Services;

public class FeatService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public FeatService(DatabaseService db)
    {
        _db = db;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _db.EnsureTableAsync<Feat>();
        await _db.EnsureTableAsync<DailyFeat>();
        _initialized = true;
    }

    public async Task<List<Feat>> GetFeatsAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Feat>()
            .Where(x => x.Username == username && !x.IsArchived)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
    }

    public async Task<List<Feat>> GetAllFeatsAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Feat>()
            .Where(x => x.Username == username)
            .OrderBy(x => x.IsArchived)
            .ThenBy(x => x.SortOrder)
            .ToListAsync();
    }

    public async Task SaveFeatAsync(Feat feat)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        feat.Username = NormalizeUsername(feat.Username);
        feat.Title = feat.Title?.Trim() ?? "";
        var conn = await _db.GetConnectionAsync();

        if (feat.Id == 0)
        {
            if (feat.CreatedDate == default)
                feat.CreatedDate = DateTime.UtcNow;

            var existing = await conn.Table<Feat>()
                .Where(x => x.Username == feat.Username)
                .ToListAsync();
            feat.SortOrder = existing.Count == 0
                ? 0
                : existing.Max(x => x.SortOrder) + 1;
            await conn.InsertAsync(feat);
        }
        else
        {
            await conn.UpdateAsync(feat);
        }
    }

    public async Task ArchiveFeatAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var feat = await conn.Table<Feat>()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync();
        if (feat == null) return;

        feat.IsArchived = true;
        await conn.UpdateAsync(feat);
    }

    public async Task DeleteFeatAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Feat>(id);
    }

    public async Task ReorderFeatsAsync(List<Feat> feats)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        for (var i = 0; i < feats.Count; i++)
        {
            feats[i].SortOrder = i;
            await conn.UpdateAsync(feats[i]);
        }
    }

    public async Task<List<DailyFeat>> GetDailyFeatsAsync(string username, string date)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DailyFeat>()
            .Where(x => x.Username == username && x.RecordedDate == date)
            .OrderBy(x => x.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveDailyFeatAsync(DailyFeat dailyFeat)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        dailyFeat.Username = NormalizeUsername(dailyFeat.Username);
        dailyFeat.CustomTitle = dailyFeat.CustomTitle?.Trim() ?? "";
        if (dailyFeat.CreatedDate == default)
            dailyFeat.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        if (dailyFeat.Id == 0)
            await conn.InsertAsync(dailyFeat);
        else
            await conn.UpdateAsync(dailyFeat);
    }

    public async Task DeleteDailyFeatAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<DailyFeat>(id);
    }

    public async Task<int> GetDayScoreAsync(string username, string date)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var feats = await GetFeatsAsync(username);
        var totalFeats = feats.Count;
        var orderById = feats.ToDictionary(x => x.Id, x => x.SortOrder);
        var monthDay = ToMonthDayKey(date);
        var conn = await _db.GetConnectionAsync();
        var dailyFeats = (await conn.Table<DailyFeat>()
                .Where(x => x.Username == username)
                .ToListAsync())
            .Where(x => ToMonthDayKey(x.RecordedDate) == monthDay)
            .ToList();

        return dailyFeats.Sum(dailyFeat =>
            dailyFeat.FeatId > 0 && orderById.TryGetValue(dailyFeat.FeatId, out var sortOrder)
                ? totalFeats - sortOrder
                : 0);
    }

    public async Task<Dictionary<string, int>> GetAllDayScoresAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        var dailyFeats = await conn.Table<DailyFeat>()
            .Where(x => x.Username == username)
            .ToListAsync();

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var monthDays = dailyFeats
            .Select(x => ToMonthDayKey(x.RecordedDate))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var monthDay in monthDays)
        {
            result[monthDay] = await GetDayScoreAsync(username, monthDay);
        }

        return result;
    }

    private static string NormalizeUsername(string username) =>
        (username ?? "").Trim().ToLowerInvariant();

    private static string ToMonthDayKey(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return "";

        if (DateTime.TryParseExact(
                date,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var fullDate))
        {
            return fullDate.ToString("MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParseExact(
                date,
                "MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var monthDay))
        {
            return monthDay.ToString("MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return "";
    }
}
