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

        // Auto-create Level 1 if an image was assigned
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            var level1 = new EnforcerLevel
            {
                ResetEnforcerId = enforcer.Id,
                Name = "Level 1",
                ImagePath = imagePath,
                LevelNumber = 1,
                SortOrder = 0,
                TriggerDays = -1,
                TriggerResets = -1,
                CreatedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(level1);
            enforcer.CurrentLevelIndex = 0;
            await conn.UpdateAsync(enforcer);
        }

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

        // Move one level down from current
        var levels = (await GetLevelsAsync(id))
            .OrderBy(l => l.LevelNumber)
            .ToList();

        if (levels.Count > 0)
        {
            int currentIdx = enforcer.CurrentLevelIndex;
            // Find current level in sorted list by index mapping
            // CurrentLevelIndex is index into SortOrder list;
            // rebuild sorted index
            int sortedCurrentIdx = -1;
            if (currentIdx >= 0 && currentIdx < levels.Count)
            {
                // Find the level that was at CurrentLevelIndex
                // in the original SortOrder list
                var allLevels = await GetLevelsAsync(id);
                var currentLevel = allLevels.Count > currentIdx
                    ? allLevels[currentIdx]
                    : null;
                if (currentLevel != null)
                    sortedCurrentIdx = levels
                        .FindIndex(l => l.Id == currentLevel.Id);
            }

            int newSortedIdx = sortedCurrentIdx > 0
                ? sortedCurrentIdx - 1
                : 0; // Already at lowest, stay there

            // Map back to SortOrder index
            var newLevel = levels[newSortedIdx];
            var allLevelsForMapping = await GetLevelsAsync(id);
            int newOriginalIdx = allLevelsForMapping
                .FindIndex(l => l.Id == newLevel.Id);
            enforcer.CurrentLevelIndex = newOriginalIdx >= 0
                ? newOriginalIdx : 0;
        }

        await conn.UpdateAsync(enforcer);
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

        // Only positive levels can auto-trigger
        // Find the highest positive level whose trigger is met
        var positiveLevels = levels
            .Where(l => l.LevelNumber > 0)
            .OrderBy(l => l.LevelNumber)
            .ToList();

        int newIndex = enforcer.CurrentLevelIndex;
        foreach (var level in positiveLevels)
        {
            bool daysTrigger = level.TriggerDays >= 0
                && days >= level.TriggerDays;
            bool resetsTrigger = level.TriggerResets >= 0
                && resets >= level.TriggerResets;
            if (daysTrigger || resetsTrigger)
            {
                // Map to original SortOrder index
                int idx = levels.FindIndex(l => l.Id == level.Id);
                if (idx >= 0) newIndex = idx;
            }
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

    public async Task UpdateConditionAsync(ResetCondition condition)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(condition);
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
        int levelNumber, int triggerDays, int triggerResets)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var existing = await GetLevelsAsync(enforcerId);
        var level = new EnforcerLevel
        {
            ResetEnforcerId = enforcerId,
            Name = name.Trim(),
            ImagePath = imagePath,
            LevelNumber = levelNumber,
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
