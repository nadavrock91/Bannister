using Bannister.Models;

namespace Bannister.Services;

public class ResetEnforcerService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ResetEnforcerService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        if (!_db.IsReadOnly)
        {
            await _db.EnsureTableAsync<ResetEnforcer>();
            await _db.EnsureTableAsync<ResetCondition>();
        }
    }

    // ── Enforcers ────────────────────────────────────────────────────
    public async Task<List<ResetEnforcer>> GetEnforcersAsync(string username)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.Table<ResetEnforcer>()
                .Where(e => e.Username == username)
                .ToListAsync())
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<ResetEnforcer> AddEnforcerAsync(
        string username, string name, string imagePath)
    {
        await EnsureInitializedAsync();
        var enforcer = new ResetEnforcer
        {
            Username = username,
            Name = name.Trim(),
            ImagePath = imagePath,
            TotalResets = 0,
            CreatedAt = DateTime.UtcNow
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(enforcer);
        return enforcer;
    }

    public async Task DeleteEnforcerAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<ResetEnforcer>(id);
        // Also delete associated conditions
        var conditions = await conn.Table<ResetCondition>()
            .Where(c => c.ResetEnforcerId == id)
            .ToListAsync();
        foreach (var c in conditions)
            await conn.DeleteAsync(c);
    }

    public async Task IncrementResetAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var enforcer = await conn.FindAsync<ResetEnforcer>(id);
        if (enforcer == null) return;
        enforcer.TotalResets++;
        await conn.UpdateAsync(enforcer);
    }

    public async Task<ResetEnforcer?> GetEnforcerAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<ResetEnforcer>(id);
    }

    // ── Conditions ───────────────────────────────────────────────────
    public async Task<List<ResetCondition>> GetConditionsAsync(
        int enforcerId)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.Table<ResetCondition>()
                .Where(c => c.ResetEnforcerId == enforcerId)
                .ToListAsync())
                .OrderBy(c => c.CreatedAt)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<ResetCondition> AddConditionAsync(
        int enforcerId, string text)
    {
        await EnsureInitializedAsync();
        var condition = new ResetCondition
        {
            ResetEnforcerId = enforcerId,
            Text = text.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(condition);
        return condition;
    }

    public async Task DeleteConditionAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<ResetCondition>(id);
    }
}
