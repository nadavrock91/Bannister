using Bannister.Models;
using SQLite;

namespace Bannister.Services;

/// <summary>
/// Service for managing activity streak attempts
/// </summary>
public class StreakService
{
    private readonly DatabaseService _db;

    public StreakService(DatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Get all streak attempts for a specific activity
    /// </summary>
    public async Task<List<StreakAttempt>> GetStreakAttemptsAsync(string username, string game, int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId)
            .OrderByDescending(s => s.AttemptNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Get all activities with active streaks for a user/game
    /// </summary>
    public async Task<List<StreakAttempt>> GetActiveStreaksAsync(string username, string game)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Get all streak-tracked activities with their current streak info
    /// </summary>
    public async Task<List<(Activity activity, StreakAttempt? currentStreak, int totalAttempts)>> GetTrackedActivitiesWithStreaksAsync(
        string username, string game, ActivityService activityService)
    {
        var activities = await activityService.GetActivitiesAsync(username, game);
        var trackedActivities = activities.Where(a => a.IsStreakTracked).ToList();

        var result = new List<(Activity, StreakAttempt?, int)>();

        foreach (var activity in trackedActivities)
        {
            var attempts = await GetStreakAttemptsAsync(username, game, activity.Id);
            var currentStreak = attempts.FirstOrDefault(a => a.IsActive);
            result.Add((activity, currentStreak, attempts.Count));
        }

        return result;
    }

    /// <summary>
    /// Get or create the active streak for an activity
    /// </summary>
    public async Task<StreakAttempt> GetOrCreateActiveStreakAsync(string username, string game, int activityId, string activityName)
    {
        var conn = await _db.GetConnectionAsync();
        
        var activeStreak = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId && s.IsActive)
            .FirstOrDefaultAsync();

        if (activeStreak != null)
            return activeStreak;

        // Get the highest attempt number for this activity
        var allAttempts = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId)
            .ToListAsync();

        int nextAttemptNumber = allAttempts.Count > 0 
            ? allAttempts.Max(a => a.AttemptNumber) + 1 
            : 1;

        // Create new streak attempt
        var newStreak = new StreakAttempt
        {
            Username = username,
            Game = game,
            ActivityId = activityId,
            ActivityName = activityName,
            AttemptNumber = nextAttemptNumber,
            IsActive = true,
            StartedAt = DateTime.UtcNow,
            LastUsedDate = DateTime.UtcNow.Date,
            DaysAchieved = 1
        };

        await conn.InsertAsync(newStreak);
        
        // Log the creation
        await LogStreakChangeAsync(newStreak.Id, 0, 1, "created", "Streak started");
        
        return newStreak;
    }

    /// <summary>
    /// Record activity usage and update streak
    /// Called when an activity is used (EXP awarded)
    /// </summary>
    public async Task RecordActivityUsageAsync(string username, string game, int activityId, string activityName)
    {
        var conn = await _db.GetConnectionAsync();
        
        var activeStreak = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId && s.IsActive)
            .FirstOrDefaultAsync();

        var today = DateTime.UtcNow.Date;

        if (activeStreak == null)
        {
            // Start a new streak
            await GetOrCreateActiveStreakAsync(username, game, activityId, activityName);
            return;
        }

        // Check if already used today
        if (activeStreak.LastUsedDate.HasValue && activeStreak.LastUsedDate.Value.Date == today)
        {
            // Already recorded today, nothing to do
            return;
        }

        // Check if this is a consecutive day
        if (activeStreak.LastUsedDate.HasValue)
        {
            var daysSinceLastUse = (today - activeStreak.LastUsedDate.Value.Date).Days;

            if (daysSinceLastUse == 1)
            {
                // Consecutive day! Extend the streak
                int daysBefore = activeStreak.DaysAchieved;
                activeStreak.DaysAchieved++;
                activeStreak.LastUsedDate = today;
                await conn.UpdateAsync(activeStreak);
                
                // Log the increment
                await LogStreakChangeAsync(activeStreak.Id, daysBefore, activeStreak.DaysAchieved, "increment");
            }
            else if (daysSinceLastUse > 1)
            {
                // Streak broken! End this one and start a new one
                activeStreak.IsActive = false;
                activeStreak.EndedAt = activeStreak.LastUsedDate.Value.AddDays(1); // Day after last use
                await conn.UpdateAsync(activeStreak);

                // Start a new streak
                await GetOrCreateActiveStreakAsync(username, game, activityId, activityName);
            }
            // daysSinceLastUse == 0 means same day, already handled above
        }
        else
        {
            // First use, update last used date
            activeStreak.LastUsedDate = today;
            await conn.UpdateAsync(activeStreak);
        }
    }

    /// <summary>
    /// Check and break any streaks that haven't been used today (for non-auto-increment streaks)
    /// Call this on app startup or game load
    /// </summary>
    public async Task CheckAndBreakExpiredStreaksAsync(string username, string game, ActivityService activityService)
    {
        var conn = await _db.GetConnectionAsync();
        
        var activeStreaks = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.IsActive)
            .ToListAsync();

        var today = DateTime.UtcNow.Date;

        // Get activities to check which are auto-increment
        var activities = await activityService.GetActivitiesAsync(username, game);
        var activityDict = activities.ToDictionary(a => a.Id);

        foreach (var streak in activeStreaks)
        {
            // Skip auto-increment streaks - they don't break from non-use
            if (activityDict.TryGetValue(streak.ActivityId, out var activity) && activity.IsStreakAutoIncrement)
            {
                continue;
            }

            if (streak.LastUsedDate.HasValue)
            {
                var daysSinceLastUse = (today - streak.LastUsedDate.Value.Date).Days;

                if (daysSinceLastUse > 1)
                {
                    // Streak is broken (missed more than 1 day)
                    streak.IsActive = false;
                    streak.EndedAt = streak.LastUsedDate.Value.AddDays(1);
                    await conn.UpdateAsync(streak);
                    
                    System.Diagnostics.Debug.WriteLine($"[STREAK] Broke streak for '{streak.ActivityName}' - {daysSinceLastUse} days since last use");
                }
            }
        }
    }

    /// <summary>
    /// Auto-increment streaks for activities marked as auto-increment
    /// Call this on game load, once per day
    /// Returns the number of streaks incremented
    /// </summary>
    public async Task<int> AutoIncrementStreaksAsync(string username, string game, ActivityService activityService)
    {
        var conn = await _db.GetConnectionAsync();
        var today = DateTime.UtcNow.Date;

        // Check if already ran today
        string prefKey = $"StreakAutoIncrement_{username}_{game}";
        string lastRun = Preferences.Get(prefKey, "");
        string todayStr = today.ToString("yyyy-MM-dd");
        
        if (lastRun == todayStr)
        {
            return 0; // Already ran today
        }

        // Get all active streaks
        var activeStreaks = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.IsActive)
            .ToListAsync();

        // Get activities to check which are auto-increment
        var activities = await activityService.GetActivitiesAsync(username, game);
        var activityDict = activities.ToDictionary(a => a.Id);

        int incrementedCount = 0;

        foreach (var streak in activeStreaks)
        {
            // Only auto-increment if activity has the flag
            if (!activityDict.TryGetValue(streak.ActivityId, out var activity) || !activity.IsStreakAutoIncrement)
            {
                continue;
            }

            // Check if already incremented today
            if (streak.LastUsedDate.HasValue && streak.LastUsedDate.Value.Date == today)
            {
                continue;
            }

            // Increment the streak
            int daysBefore = streak.DaysAchieved;
            streak.DaysAchieved++;
            streak.LastUsedDate = today;
            await conn.UpdateAsync(streak);
            
            // Log the auto-increment
            await LogStreakChangeAsync(streak.Id, daysBefore, streak.DaysAchieved, "auto_increment");
            
            incrementedCount++;
            System.Diagnostics.Debug.WriteLine($"[STREAK] Auto-incremented '{streak.ActivityName}' to {streak.DaysAchieved} days");
        }

        // Mark as ran today
        Preferences.Set(prefKey, todayStr);

        return incrementedCount;
    }

    /// <summary>
    /// End an active streak
    /// </summary>
    public async Task EndStreakAsync(int streakId)
    {
        var conn = await _db.GetConnectionAsync();
        var streak = await conn.GetAsync<StreakAttempt>(streakId);
        
        if (streak != null && streak.IsActive)
        {
            streak.IsActive = false;
            streak.EndedAt = DateTime.UtcNow;
            await conn.UpdateAsync(streak);
        }
    }

    /// <summary>
    /// Reactivate an ended streak, making it the active streak again.
    /// Sets IsActive = true, clears EndedAt, and sets LastUsedDate to yesterday
    /// so the streak continues from where it left off without immediately breaking.
    /// </summary>
    public async Task ReactivateStreakAsync(int streakId)
    {
        var conn = await _db.GetConnectionAsync();
        
        var streak = await conn.Table<StreakAttempt>()
            .FirstOrDefaultAsync(s => s.Id == streakId);
        
        if (streak == null) return;
        
        int daysBefore = streak.DaysAchieved;
        
        // Reactivate the streak
        streak.IsActive = true;
        streak.EndedAt = null;
        
        // Set LastUsedDate to yesterday so it doesn't immediately break
        // (the user can use it today to continue the streak)
        streak.LastUsedDate = DateTime.UtcNow.Date.AddDays(-1);
        
        await conn.UpdateAsync(streak);
        
        // Log the reactivation
        await LogStreakChangeAsync(streakId, daysBefore, daysBefore, "reactivated", 
            "Streak reactivated by user");
        
        System.Diagnostics.Debug.WriteLine(
            $"[STREAK] Reactivated streak #{streak.AttemptNumber} for activity {streak.ActivityId} " +
            $"with {streak.DaysAchieved} days");
    }

    /// <summary>
    /// Update the days count for a streak
    /// </summary>
    public async Task UpdateStreakDaysAsync(int streakId, int newDays)
    {
        var conn = await _db.GetConnectionAsync();
        var streak = await conn.GetAsync<StreakAttempt>(streakId);
        
        if (streak != null)
        {
            int daysBefore = streak.DaysAchieved;
            streak.DaysAchieved = newDays;
            
            // Adjust the start date to match the new days count (keeping last used date as today)
            if (streak.IsActive && streak.LastUsedDate.HasValue)
            {
                streak.StartedAt = streak.LastUsedDate.Value.AddDays(-(newDays - 1));
            }
            
            await conn.UpdateAsync(streak);
            
            // Log the manual edit
            await LogStreakChangeAsync(streakId, daysBefore, newDays, "manual_edit", 
                $"Manual edit: {daysBefore} → {newDays}");
        }
    }

    /// <summary>
    /// Delete a streak attempt. If it's the active one, reactivate the previous attempt.
    /// </summary>
    public async Task<bool> DeleteStreakAttemptAsync(int streakId)
    {
        var conn = await _db.GetConnectionAsync();
        var streak = await conn.GetAsync<StreakAttempt>(streakId);
        
        if (streak == null)
            return false;

        bool wasActive = streak.IsActive;
        int deletedAttemptNumber = streak.AttemptNumber;
        string username = streak.Username;
        string game = streak.Game;
        int activityId = streak.ActivityId;

        // Delete the streak logs first
        await DeleteStreakLogsAsync(streakId);
        
        // Delete the streak attempt
        await conn.DeleteAsync(streak);

        // If the deleted streak was active, reactivate the previous one
        if (wasActive)
        {
            // Find the previous attempt (highest attempt number that's not the deleted one)
            var previousAttempt = await conn.Table<StreakAttempt>()
                .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId)
                .OrderByDescending(s => s.AttemptNumber)
                .FirstOrDefaultAsync();

            if (previousAttempt != null)
            {
                previousAttempt.IsActive = true;
                previousAttempt.EndedAt = null; // Clear the end date
                previousAttempt.LastUsedDate = DateTime.UtcNow.Date; // Set to today so it continues
                await conn.UpdateAsync(previousAttempt);
                
                System.Diagnostics.Debug.WriteLine($"[STREAK] Reactivated attempt #{previousAttempt.AttemptNumber} after deleting #{deletedAttemptNumber}");
            }
        }

        // Renumber remaining attempts to fill the gap
        await RenumberAttemptsAsync(username, game, activityId);

        return true;
    }

    /// <summary>
    /// Renumber streak attempts to be sequential (1, 2, 3, ...)
    /// </summary>
    private async Task RenumberAttemptsAsync(string username, string game, int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        var attempts = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId)
            .OrderBy(s => s.AttemptNumber)
            .ToListAsync();

        int expectedNumber = 1;
        foreach (var attempt in attempts)
        {
            if (attempt.AttemptNumber != expectedNumber)
            {
                attempt.AttemptNumber = expectedNumber;
                await conn.UpdateAsync(attempt);
            }
            expectedNumber++;
        }
    }

    /// <summary>
    /// Start a new streak attempt for an activity
    /// </summary>
    public async Task<StreakAttempt> StartNewStreakAsync(string username, string game, int activityId, string activityName)
    {
        // First, end any existing active streak
        var conn = await _db.GetConnectionAsync();
        var existingActive = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId && s.IsActive)
            .FirstOrDefaultAsync();

        if (existingActive != null)
        {
            existingActive.IsActive = false;
            existingActive.EndedAt = DateTime.UtcNow;
            await conn.UpdateAsync(existingActive);
        }

        // Create new streak
        return await GetOrCreateActiveStreakAsync(username, game, activityId, activityName);
    }

    /// <summary>
    /// Delete all streak attempts for an activity (when untracking)
    /// </summary>
    public async Task DeleteStreaksForActivityAsync(string username, string game, int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM streak_attempts WHERE Username = ? AND Game = ? AND ActivityId = ?",
            username, game, activityId);
    }

    /// <summary>
    /// Manually add a past streak attempt - always creates new
    /// </summary>
    public async Task AddManualStreakAsync(string username, string game, int activityId, string activityName,
        int attemptNumber, int daysAchieved, DateTime? startDate, DateTime? endDate)
    {
        var conn = await _db.GetConnectionAsync();

        // Always create new - don't update existing
        var streak = new StreakAttempt
        {
            Username = username,
            Game = game,
            ActivityId = activityId,
            ActivityName = activityName,
            AttemptNumber = attemptNumber,
            DaysAchieved = daysAchieved,
            StartedAt = startDate?.ToUniversalTime(),
            EndedAt = endDate?.ToUniversalTime(),
            LastUsedDate = endDate?.ToUniversalTime(),
            IsActive = false // Manual entries are always past streaks
        };
        await conn.InsertAsync(streak);
    }

    /// <summary>
    /// Bump attempt numbers up by 1 for all attempts >= fromNumber
    /// Used when inserting a past streak before the active one
    /// </summary>
    public async Task BumpAttemptNumbersFromAsync(string username, string game, int activityId, int fromNumber)
    {
        var conn = await _db.GetConnectionAsync();
        var attempts = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId && s.AttemptNumber >= fromNumber)
            .ToListAsync();

        // Sort descending so we update highest first (avoid conflicts)
        foreach (var attempt in attempts.OrderByDescending(a => a.AttemptNumber))
        {
            attempt.AttemptNumber++;
            await conn.UpdateAsync(attempt);
        }
    }

    /// <summary>
    /// Get the next available attempt number for adding a past streak
    /// </summary>
    public async Task<int> GetNextPastAttemptNumberAsync(string username, string game, int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        var attempts = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId)
            .ToListAsync();

        if (attempts.Count == 0)
            return 1;

        var activeStreak = attempts.FirstOrDefault(a => a.IsActive);
        
        if (activeStreak != null)
        {
            // Return the active streak's number - caller should bump it first
            return activeStreak.AttemptNumber;
        }
        else
        {
            // No active - add after the highest
            return attempts.Max(a => a.AttemptNumber) + 1;
        }
    }

    #region Streak Logging

    /// <summary>
    /// Log a streak change for history tracking
    /// </summary>
    public async Task LogStreakChangeAsync(int streakAttemptId, int daysBefore, int daysAfter, string changeType, string? note = null)
    {
        var conn = await _db.GetConnectionAsync();
        
        // Ensure the table exists
        await conn.CreateTableAsync<StreakLog>();
        
        var log = new StreakLog
        {
            StreakAttemptId = streakAttemptId,
            DaysBefore = daysBefore,
            DaysAfter = daysAfter,
            ChangeType = changeType,
            Note = note,
            LoggedAt = DateTime.UtcNow
        };
        
        await conn.InsertAsync(log);
        System.Diagnostics.Debug.WriteLine($"[STREAK LOG] Logged change for streak {streakAttemptId}: {daysBefore} -> {daysAfter} ({changeType})");
    }

    /// <summary>
    /// Get all logs for a specific streak attempt
    /// </summary>
    public async Task<List<StreakLog>> GetStreakLogsAsync(int streakAttemptId)
    {
        var conn = await _db.GetConnectionAsync();
        
        // Ensure the table exists
        await conn.CreateTableAsync<StreakLog>();
        
        return await conn.Table<StreakLog>()
            .Where(l => l.StreakAttemptId == streakAttemptId)
            .OrderByDescending(l => l.LoggedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get streak logs for a date range (for charts/analysis)
    /// </summary>
    public async Task<List<StreakLog>> GetStreakLogsForDateRangeAsync(int streakAttemptId, DateTime startDate, DateTime endDate)
    {
        var conn = await _db.GetConnectionAsync();
        
        // Ensure the table exists
        await conn.CreateTableAsync<StreakLog>();
        
        return await conn.Table<StreakLog>()
            .Where(l => l.StreakAttemptId == streakAttemptId 
                && l.LoggedAt >= startDate 
                && l.LoggedAt <= endDate)
            .OrderBy(l => l.LoggedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Delete all logs for a streak (when streak is deleted)
    /// </summary>
    public async Task DeleteStreakLogsAsync(int streakAttemptId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM streak_logs WHERE StreakAttemptId = ?",
            streakAttemptId);
    }

    #endregion
}
