using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Service for managing custom activity groupings.
/// Groupings allow users to see activities from any game in a single view.
/// </summary>
public class ActivityGroupingService
{
    private readonly DatabaseService _db;
    private bool _initialized = false;

    public ActivityGroupingService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<ActivityGrouping>();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<ActivityGroupingEntry>();
        _initialized = true;
    }

    #region Grouping CRUD

    public async Task<List<ActivityGrouping>> GetGroupingsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var groupings = await conn.Table<ActivityGrouping>()
            .Where(g => g.Username == username)
            .OrderBy(g => g.Name)
            .ToListAsync();

        // Populate activity counts
        foreach (var g in groupings)
        {
            g.ActivityCount = await conn.Table<ActivityGroupingEntry>()
                .Where(e => e.GroupingId == g.Id)
                .CountAsync();
        }

        return groupings;
    }

    public async Task<ActivityGrouping?> GetGroupingAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ActivityGrouping>()
            .Where(g => g.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<ActivityGrouping> CreateGroupingAsync(string username, string name)
    {
        await EnsureInitializedAsync();
        var grouping = new ActivityGrouping
        {
            Username = username,
            Name = name.Trim(),
            CreatedAt = DateTime.Now
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(grouping);
        return grouping;
    }

    public async Task RenameGroupingAsync(int id, string newName)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var grouping = await conn.GetAsync<ActivityGrouping>(id);
        if (grouping != null)
        {
            grouping.Name = newName.Trim();
            await conn.UpdateAsync(grouping);
        }
    }

    public async Task DeleteGroupingAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        // Delete all entries first
        await conn.ExecuteAsync("DELETE FROM activity_grouping_entries WHERE GroupingId = ?", id);
        await conn.DeleteAsync<ActivityGrouping>(id);
    }

    #endregion

    #region Activity <-> Grouping Management

    /// <summary>
    /// Add an activity to a grouping. No-op if already in the grouping.
    /// </summary>
    public async Task AddActivityToGroupingAsync(int groupingId, int activityId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();

        // Check if already exists
        var existing = await conn.Table<ActivityGroupingEntry>()
            .Where(e => e.GroupingId == groupingId && e.ActivityId == activityId)
            .FirstOrDefaultAsync();

        if (existing != null) return;

        await conn.InsertAsync(new ActivityGroupingEntry
        {
            GroupingId = groupingId,
            ActivityId = activityId,
            AddedAt = DateTime.Now
        });
    }

    /// <summary>
    /// Remove an activity from a grouping.
    /// </summary>
    public async Task RemoveActivityFromGroupingAsync(int groupingId, int activityId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM activity_grouping_entries WHERE GroupingId = ? AND ActivityId = ?",
            groupingId, activityId);
    }

    /// <summary>
    /// Get all activity IDs in a grouping.
    /// </summary>
    public async Task<List<int>> GetActivityIdsInGroupingAsync(int groupingId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entries = await conn.Table<ActivityGroupingEntry>()
            .Where(e => e.GroupingId == groupingId)
            .ToListAsync();
        return entries.Select(e => e.ActivityId).ToList();
    }

    /// <summary>
    /// Get all activities in a grouping (full Activity objects).
    /// </summary>
    public async Task<List<Activity>> GetActivitiesInGroupingAsync(int groupingId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var activityIds = await GetActivityIdsInGroupingAsync(groupingId);

        if (activityIds.Count == 0) return new List<Activity>();

        // Load each activity
        var activities = new List<Activity>();
        foreach (var id in activityIds)
        {
            var activity = await conn.Table<Activity>()
                .Where(a => a.Id == id && a.IsActive)
                .FirstOrDefaultAsync();
            if (activity != null)
                activities.Add(activity);
        }

        return activities;
    }

    /// <summary>
    /// Get all groupings that an activity belongs to.
    /// </summary>
    public async Task<List<ActivityGrouping>> GetGroupingsForActivityAsync(string username, int activityId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entries = await conn.Table<ActivityGroupingEntry>()
            .Where(e => e.ActivityId == activityId)
            .ToListAsync();

        var groupingIds = entries.Select(e => e.GroupingId).ToList();
        if (groupingIds.Count == 0) return new List<ActivityGrouping>();

        var allGroupings = await conn.Table<ActivityGrouping>()
            .Where(g => g.Username == username)
            .ToListAsync();

        return allGroupings.Where(g => groupingIds.Contains(g.Id)).OrderBy(g => g.Name).ToList();
    }

    /// <summary>
    /// Check if an activity is in a specific grouping.
    /// </summary>
    public async Task<bool> IsActivityInGroupingAsync(int groupingId, int activityId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var count = await conn.Table<ActivityGroupingEntry>()
            .Where(e => e.GroupingId == groupingId && e.ActivityId == activityId)
            .CountAsync();
        return count > 0;
    }

    #endregion
}
