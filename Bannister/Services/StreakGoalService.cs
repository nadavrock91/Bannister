using Bannister.Models;

namespace Bannister.Services;

public class StreakGoalService
{
    private readonly DatabaseService _db;

    public StreakGoalService(DatabaseService db)
    {
        _db = db;
    }

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
        {
            throw new ReadOnlyDatabaseException("Streak goals are read-only on secondary devices.");
        }
    }

    public async Task<List<StreakGoal>> GetGoalsForActivityAsync(int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StreakGoal>()
            .Where(g => g.ActivityId == activityId)
            .OrderBy(g => g.SetDate)
            .ToListAsync();
    }

    public async Task<StreakGoal> AddGoalAsync(int activityId, int targetDays)
    {
        EnsureWritable();
        var goal = new StreakGoal
        {
            ActivityId = activityId,
            TargetDays = targetDays,
            SetDate = DateTime.UtcNow,
            AchievedDate = null
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(goal);
        return goal;
    }

    public async Task<List<StreakGoal>> MarkAchievedIfReachedAsync(int activityId, int currentStreakDays)
    {
        EnsureWritable();
        var goals = await GetGoalsForActivityAsync(activityId);
        var today = DateTime.Today;

        foreach (var goal in goals.Where(g => !g.AchievedDate.HasValue && currentStreakDays >= g.TargetDays))
        {
            goal.AchievedDate = today;
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(goal);
        }

        return goals;
    }

    public async Task DeleteGoalAsync(int goalId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var goal = await conn.Table<StreakGoal>().FirstOrDefaultAsync(g => g.Id == goalId);
        if (goal != null)
        {
            await conn.DeleteAsync(goal);
        }
    }
}
