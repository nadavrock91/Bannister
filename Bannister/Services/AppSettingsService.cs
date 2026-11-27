using Bannister.Models;
using System.Text.Json;

namespace Bannister.Services;

public sealed class AppSetting
{
    [SQLite.PrimaryKey, SQLite.AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public partial class AppSettingsService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public AppSettingsService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<AppSetting>();
            await conn.CreateTableAsync<ThemePaletteHistory>();
            await conn.CreateTableAsync<ThemeTemplate>();
        }
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

    public async Task<ThemeMigrationReport> MigrateThemeOverridesAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (_db.IsReadOnly)
        {
            var pending = ThemePaletteMigration.Plan(await GetByPrefixAsync(username, "theme_"), deferred: true);
            if (pending.RetiredKeys.Count > 0) return pending.Report;
            return ReadReport(await GetAsync(username, ThemePaletteMigration.ReportKey));
        }

        ThemeMigrationReport report = new(Array.Empty<ThemeOverrideDecision>());
        await conn.RunInTransactionAsync(db =>
        {
            var rows = db.Table<AppSetting>().Where(x => x.Username == username).ToList();
            var values = rows.OrderBy(x => x.Id).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().Value);
            report = ReadReport(values.GetValueOrDefault(ThemePaletteMigration.ReportKey));
            var plan = ThemePaletteMigration.Plan(values);
            EnsureStartingPoints(db, username, plan.Values);
            if (plan.RetiredKeys.Count == 0) return;
            foreach (var (key, value) in plan.Updates)
                db.Insert(new AppSetting { Username = username, Key = key, Value = value });
            var retired = plan.RetiredKeys.ToHashSet();
            foreach (var row in rows.Where(r => retired.Contains(r.Key))) db.Delete(row);
            report = new(report.Decisions.Concat(plan.Report.Decisions).ToList());
            var saved = rows.OrderBy(x => x.Id).LastOrDefault(r => r.Key == ThemePaletteMigration.ReportKey);
            string json = JsonSerializer.Serialize(report);
            if (saved == null) db.Insert(new AppSetting { Username = username, Key = ThemePaletteMigration.ReportKey, Value = json });
            else { saved.Value = json; db.Update(saved); }
            foreach (var mode in PaletteModes)
                if (plan.RetiredKeys.Any(key => key.StartsWith(ModeTheme.StoragePrefix(mode), StringComparison.Ordinal)))
                    RecordPaletteHistory(db, username, mode, PaletteSnapshot(mode, plan.Values), "Migration",
                        "Consolidated retired color names; details are in the catalog migration report.");
        });
        return report;
    }

    private static ThemeMigrationReport ReadReport(string? json)
    {
        try
        {
            var report = json == null ? null : JsonSerializer.Deserialize<ThemeMigrationReport>(json);
            return report?.Decisions == null ? new(Array.Empty<ThemeOverrideDecision>()) : report;
        }
        catch (JsonException) { return new(Array.Empty<ThemeOverrideDecision>()); }
    }

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
