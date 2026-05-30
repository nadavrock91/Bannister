using Bannister.Models;
using SQLite;

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

    public async Task SetCurrentAsync(int id, int current)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.Current = Math.Clamp(current, 0, allowance.Total);
        });
    }

    public async Task IncrementAsync(int id)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.Current = Math.Min(allowance.Current + 1, allowance.Total);
        });
    }

    public async Task DecrementAsync(int id)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.Current = Math.Max(allowance.Current - 1, 0);
        });
    }

    public async Task ResetAsync(int id)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.Current = 0;
        });
    }

    public async Task SetTotalAsync(int id, int total)
    {
        await UpdateAllowanceAsync(id, allowance =>
        {
            allowance.Total = Math.Max(1, total);
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
        allowance.Total = Math.Max(1, allowance.Total);
        allowance.Current = Math.Clamp(allowance.Current, 0, allowance.Total);
        allowance.UpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(allowance);
    }
}
