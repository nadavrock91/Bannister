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
            await _db.EnsureTableAsync<EnforcerLevel>();
        }
    }

    // ── Enforcers ────────────────────────────────────────────────────
    public async Task<List<ResetEnforcer>> GetEnforcersAsync(
        string username)
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

    public async Task<ResetEnforcer?> GetEnforcerAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<ResetEnforcer>(id);
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
            LastResetDate = null,
            StreakStartDate = DateTime.UtcNow,
            CurrentLevelIndex = -1,
            CreatedAt = DateTime.UtcNow
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(enforcer);
        return enforcer;
    }

    public async Task UpdateEnforcerAsync(ResetEnforcer enforcer)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(enforcer);
    }

    public async Task ArchiveEnforcerAsync(int id, bool archived)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var enforcer = await conn.FindAsync<ResetEnforcer>(id);
        if (enforcer == null) return;
        enforcer.IsArchived = archived;
        await conn.UpdateAsync(enforcer);
    }

    public async Task DeleteEnforcerAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<ResetEnforcer>(id);
        var conditions = await conn.Table<ResetCondition>()
            .Where(c => c.ResetEnforcerId == id)
            .ToListAsync();
        foreach (var c in conditions)
            await conn.DeleteAsync(c);
        var levels = await conn.Table<EnforcerLevel>()
            .Where(l => l.ResetEnforcerId == id)
            .ToListAsync();
        foreach (var l in levels)
            await conn.DeleteAsync(l);
    }

    /// <summary>
    /// Records a reset: increments TotalResets, resets streak,
    /// stores LastResetDate, then checks auto level triggers.
    /// </summary>
    public async Task IncrementResetAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var enforcer = await conn.FindAsync<ResetEnforcer>(id);
        if (enforcer == null) return;

        enforcer.TotalResets++;
        enforcer.LastResetDate = DateTime.UtcNow;
        enforcer.StreakStartDate = DateTime.UtcNow;
        await conn.UpdateAsync(enforcer);

        await CheckAutoLevelAsync(enforcer, conn);
    }

    /// <summary>
    /// Call on page load to check auto level triggers based on
    /// current streak and total resets.
    /// </summary>
    public async Task CheckAutoLevelAsync(ResetEnforcer enforcer)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await CheckAutoLevelAsync(enforcer, conn);
    }

    private async Task CheckAutoLevelAsync(
        ResetEnforcer enforcer,
        SQLite.ISQLiteAsyncConnection conn)
    {
        var levels = await GetLevelsAsync(enforcer.Id);
        if (levels.Count == 0) return;

        int days = enforcer.DaysInARow;
        int resets = enforcer.TotalResets;

        // Find highest-priority level whose trigger is satisfied
        // Check all levels in SortOrder; last matching wins
        int newIndex = enforcer.CurrentLevelIndex;
        for (int i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            bool daysTrigger = level.TriggerDays >= 0
                && days >= level.TriggerDays;
            bool resetsTrigger = level.TriggerResets >= 0
                && resets >= level.TriggerResets;
            if (daysTrigger || resetsTrigger)
                newIndex = i;
        }

        if (newIndex != enforcer.CurrentLevelIndex)
        {
            enforcer.CurrentLevelIndex = newIndex;
            await conn.UpdateAsync(enforcer);
        }
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

    // ── Levels ───────────────────────────────────────────────────────
    public async Task<List<EnforcerLevel>> GetLevelsAsync(
        int enforcerId)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.Table<EnforcerLevel>()
                .Where(l => l.ResetEnforcerId == enforcerId)
                .ToListAsync())
                .OrderBy(l => l.SortOrder)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<EnforcerLevel> AddLevelAsync(
        int enforcerId, string name, string imagePath,
        bool isPositive, int triggerDays, int triggerResets)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var existing = await GetLevelsAsync(enforcerId);
        var level = new EnforcerLevel
        {
            ResetEnforcerId = enforcerId,
            Name = name.Trim(),
            ImagePath = imagePath,
            IsPositive = isPositive,
            SortOrder = existing.Count,
            TriggerDays = triggerDays,
            TriggerResets = triggerResets,
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(level);
        return level;
    }

    public async Task UpdateLevelAsync(EnforcerLevel level)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(level);
    }

    public async Task DeleteLevelAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<EnforcerLevel>(id);
    }
}
