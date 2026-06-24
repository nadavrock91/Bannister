using Bannister.Models;
using SQLite;
using System.Globalization;

namespace Bannister.Services;

public class AllowanceService
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private bool _initialized;

    public AllowanceService(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Allowances are read-only on secondary devices.");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<Allowance>();
            try { await conn.ExecuteAsync("ALTER TABLE allowances ADD COLUMN PromptDailyOnHome INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE allowances ADD COLUMN SuccessStreak INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE allowances ADD COLUMN RecentHistory TEXT DEFAULT ''"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE allowances ADD COLUMN LastOutcomeDate TEXT"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE allowances ADD COLUMN CapFloor INTEGER DEFAULT 1"); } catch { }
        }

        _initialized = true;
    }

    public async Task<List<Allowance>> GetAllowancesAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            var allowances = await conn.Table<Allowance>()
                .Where(a => a.Username == username)
                .ToListAsync();

            return allowances.OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Allowance>();
        }
    }

    public async Task<Allowance> AddAllowanceAsync(string username, string title, int total)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var now = DateTime.UtcNow;
        var allowance = new Allowance
        {
            Username = username,
            Title = title.Trim(),
            Total = Math.Max(1, total),
            Current = 0,
            SuccessStreak = 0,
            RecentHistory = "",
            LastOutcomeDate = null,
            CapFloor = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(allowance);
        return allowance;
    }

    public async Task<List<Allowance>> GetDailyPromptAllowancesAsync(string username)
    {
        var allowances = await GetAllowancesAsync(username);
        return allowances.Where(a => a.PromptDailyOnHome).OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<bool> RecordOutcomeAsync(int id, bool success)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var allowance = await conn.FindAsync<Allowance>(id);
        if (allowance == null) return false;

        var now = DateTime.UtcNow;
        allowance.LastOutcomeDate = now;
        allowance.RecentHistory = AppendHistory(allowance.RecentHistory, $"{now:yyyy-MM-dd}:{(success ? "S" : "F")}");

        allowance.CapFloor = Math.Max(1, allowance.CapFloor);
        allowance.Total = Math.Max(allowance.CapFloor, allowance.Total);

        if (success)
        {
            allowance.SuccessStreak++;
            if (allowance.SuccessStreak >= 3)
            {
                allowance.Total++;
                allowance.SuccessStreak = 0;
            }
        }
        else
        {
            allowance.SuccessStreak = 0;
            allowance.Total = Math.Max(allowance.CapFloor, allowance.Total - 1);
        }

        allowance.UpdatedAt = now;
        await conn.UpdateAsync(allowance);
        return true;
    }

    public async Task<List<(DateTime date, bool success)>> GetRecentHistoryEntriesAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var allowance = await conn.FindAsync<Allowance>(id);
        return allowance == null
            ? new List<(DateTime date, bool success)>()
            : ParseHistory(allowance.RecentHistory);
    }

    public async Task SetTotalAsync(int id, int total)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.CapFloor = Math.Max(1, allowance.CapFloor);
            allowance.Total = Math.Max(allowance.CapFloor, total);
            allowance.Current = Math.Clamp(allowance.Current, 0, allowance.Total);
        });
    }

    public async Task UpdateTitleAsync(int id, string title)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.Title = title.Trim();
        });
    }

    public async Task SetPromptDailyOnHomeAsync(int id, bool enabled)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.PromptDailyOnHome = enabled;
        });
    }

    public async Task DeleteAllowanceAsync(int id)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Allowance>(id);
    }

    private async Task UpdateAllowanceAsync(int id, Action<Allowance> update)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var allowance = await conn.FindAsync<Allowance>(id);
        if (allowance == null) return;

        update(allowance);
        allowance.CapFloor = Math.Max(1, allowance.CapFloor);
        allowance.Total = Math.Max(allowance.CapFloor, allowance.Total);
        allowance.Current = Math.Clamp(allowance.Current, 0, allowance.Total);
        allowance.UpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(allowance);
    }

    private static string AppendHistory(string? history, string entry)
    {
        var entries = (history ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        entries.Add(entry);

        if (entries.Count > 14)
            entries = entries.Skip(entries.Count - 14).ToList();

        return string.Join(",", entries);
    }

    private static List<(DateTime date, bool success)> ParseHistory(string? history)
    {
        var entries = new List<(DateTime date, bool success)>();
        foreach (var token in (history ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', 2);
            if (parts.Length != 2)
                continue;

            if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            if (parts[1] == "S")
                entries.Add((date, true));
            else if (parts[1] == "F")
                entries.Add((date, false));
        }

        return entries;
    }
}
