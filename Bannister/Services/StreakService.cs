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
    public async Task<StreakAttempt> GetOrCreateActiveStreakAsync(string username, string game, int activityId, string activityName,
        DateTime? usedAt = null)
    {
        var conn = await _db.GetConnectionAsync();
        var usedDate = (usedAt ?? DateTime.UtcNow).ToUniversalTime().Date;
        
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
            StartedAt = usedDate,
            LastUsedDate = usedDate,
            DaysAchieved = 0
        };

        await conn.InsertAsync(newStreak);
        
        // Log the creation
        await LogStreakChangeAsync(newStreak.Id, 0, 1, "created", "Streak started");
        
        return newStreak;
    }

    /// <summary>
    /// Check how many days since the active streak was last used.
    /// Returns 0 if used today, 1 if yesterday (consecutive), >1 if gap, -1 if no active streak.
    /// </summary>
    public async Task<int> GetActiveStreakGapDaysAsync(string username, string game, int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        var activeStreak = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId && s.IsActive)
            .FirstOrDefaultAsync();

        if (activeStreak == null) return -1;

        var today = DateTime.UtcNow.Date;
        if (!activeStreak.LastUsedDate.HasValue) return -1;
        if (activeStreak.LastUsedDate.Value.Date == today) return 0;

        return (today - activeStreak.LastUsedDate.Value.Date).Days;
    }

    /// <summary>
    /// Record activity usage and update streak
    /// Called when an activity is used (EXP awarded)
    /// </summary>
    public async Task RecordActivityUsageAsync(string username, string game, int activityId, string activityName, Activity? activity = null,
        DateTime? usedAt = null)
    {
        var conn = await _db.GetConnectionAsync();
        
        var activeStreak = await conn.Table<StreakAttempt>()
            .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId && s.IsActive)
            .FirstOrDefaultAsync();

        var today = (usedAt ?? DateTime.UtcNow).ToUniversalTime().Date;

        if (activeStreak == null)
        {
            // Start a new streak
            await GetOrCreateActiveStreakAsync(username, game, activityId, activityName, usedAt);
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
                activeStreak.DaysAchieved = GetNextDaysAchieved(activeStreak, activity, today);
                activeStreak.LastUsedDate = today;
                await conn.UpdateAsync(activeStreak);
                
                // Log the increment
                await LogStreakChangeAsync(activeStreak.Id, daysBefore, activeStreak.DaysAchieved, "increment");
            }
            else if (daysSinceLastUse > 1)
            {
                // ShowStreakAsDaysSinceStarted activities do not reset on missed days —
                // the streak is calendar days since StartedAt, not consecutive-use days.
                if (activity?.ShowStreakAsDaysSinceStarted == true)
                {
                    int daysBefore = activeStreak.DaysAchieved;
                    activeStreak.DaysAchieved = GetNextDaysAchieved(activeStreak, activity, today);
                    activeStreak.LastUsedDate = today;
                    await conn.UpdateAsync(activeStreak);
                    await LogStreakChangeAsync(activeStreak.Id, daysBefore, activeStreak.DaysAchieved, "increment",
                        "Days-since-started update after gap");
                }
                else
                {
                    // Gap detected but UI has already decided whether to break or not.
                    // If the streak was broken by the UI, no active streak exists here (we returned early above).
                    // If we reach here, the user chose NOT to break — increment by 1 for regular streaks.
                    int daysBefore = activeStreak.DaysAchieved;
                    activeStreak.DaysAchieved++;
                    activeStreak.LastUsedDate = today;
                    await conn.UpdateAsync(activeStreak);
                    await LogStreakChangeAsync(activeStreak.Id, daysBefore, activeStreak.DaysAchieved, "increment",
                        $"Incremented after {daysSinceLastUse}-day gap (user confirmed no break)");
                }
            }
            // daysSinceLastUse == 0 means same day, already handled above
        }
        else
        {
            // First use, update last used date
            int daysBefore = activeStreak.DaysAchieved;
            activeStreak.DaysAchieved = GetNextDaysAchieved(activeStreak, activity, today);
            activeStreak.LastUsedDate = today;
            await conn.UpdateAsync(activeStreak);
            await LogStreakChangeAsync(activeStreak.Id, daysBefore, activeStreak.DaysAchieved, "increment");
        }
    }

    private static int GetNextDaysAchieved(StreakAttempt streak, Activity? activity, DateTime? usedDate = null)
    {
        if (activity?.ShowStreakAsDaysSinceStarted == true && streak.StartedAt.HasValue)
        {
            var startDate = streak.StartedAt.Value.ToLocalTime().Date;
            var endDate = (usedDate ?? DateTime.Today).Date;
            return Math.Max(0, (endDate - startDate).Days);
        }

        return streak.DaysAchieved + 1;
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
            // Also skip ShowStreakAsDaysSinceStarted - those track calendar days from StartedAt, not consecutive-use
            if (activityDict.TryGetValue(streak.ActivityId, out var activity)
                && (activity.IsStreakAutoIncrement || activity.ShowStreakAsDaysSinceStarted))
            {
                continue;
            }

            if (streak.LastUsedDate.HasValue)
            {
                var daysSinceLastUse = (today - streak.LastUsedDate.Value.Date).Days;

                if (daysSinceLastUse > 1)
                {
                    // Streak is broken (missed more than 1 day)
                    await LogTargetStatResetForAttemptAsync(conn, streak, "reset", "Streak reset after missed day check");

                    streak.IsActive = false;
                    streak.EndedAt = streak.LastUsedDate.Value.AddDays(1);
                    await conn.UpdateAsync(streak);

                    await LogStreakChangeAsync(streak.Id, streak.DaysAchieved, streak.DaysAchieved, "reset",
                        "Streak reset after missed day check");
                    
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
            streak.DaysAchieved = GetNextDaysAchieved(streak, activity);
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
            await LogTargetStatResetForAttemptAsync(conn, streak, "ended", "Streak ended by user");

            streak.IsActive = false;
            streak.EndedAt = DateTime.UtcNow;
            await conn.UpdateAsync(streak);

            await LogStreakChangeAsync(streak.Id, streak.DaysAchieved, streak.DaysAchieved, "ended",
                "Streak ended by user");
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
        var countBeforeByTarget = await GetTargetCountsForAttemptReactivationAsync(conn, streak);
        
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
        await LogTargetStatReactivationAsync(conn, streak, countBeforeByTarget);
        
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
            await LogTargetStatResetForAttemptAsync(conn, streak, "deleted", "Active streak attempt deleted");
        }

        await conn.ExecuteAsync("DELETE FROM streak_target_completions WHERE StreakAttemptId = ?", streakId);

        if (wasActive)
        {
            // Find the previous attempt (highest attempt number that's not the deleted one)
            var previousAttempt = await conn.Table<StreakAttempt>()
                .Where(s => s.Username == username && s.Game == game && s.ActivityId == activityId)
                .OrderByDescending(s => s.AttemptNumber)
                .FirstOrDefaultAsync();

            if (previousAttempt != null)
            {
                var countBeforeByTarget = await GetTargetCountsForAttemptReactivationAsync(conn, previousAttempt);
                previousAttempt.IsActive = true;
                previousAttempt.EndedAt = null; // Clear the end date
                previousAttempt.LastUsedDate = DateTime.UtcNow.Date; // Set to today so it continues
                await conn.UpdateAsync(previousAttempt);
                await LogTargetStatReactivationAsync(conn, previousAttempt, countBeforeByTarget);
                
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
            await LogTargetStatResetForAttemptAsync(conn, existingActive, "ended", "Active streak replaced by new attempt");

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

    public async Task LogTargetCompletionAsync(string username, string game, Activity activity, StreakAttempt attempt, int targetDays)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        var existing = await conn.Table<StreakTargetCompletion>()
            .Where(c => c.Username == username
                     && c.Game == game
                     && c.ActivityId == activity.Id
                     && c.StreakAttemptId == attempt.Id
                     && c.TargetDays == targetDays)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return;
        }

        int countBefore = await GetCurrentTargetCompletionCountAsync(conn, username, targetDays);

        await conn.InsertAsync(new StreakTargetCompletion
        {
            Username = username,
            Game = game,
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            StreakAttemptId = attempt.Id,
            TargetDays = targetDays,
            CompletedAt = DateTime.UtcNow
        });

        await LogTargetStatChangeAsync(
            conn,
            username,
            targetDays,
            countBefore,
            countBefore + 1,
            "completion",
            activity.Name,
            attempt.Id,
            $"{activity.Name} reached {targetDays} in a row");
    }

    public async Task<List<StreakTargetCompletion>> GetTargetCompletionsAsync(string username, string game, int activityId)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();

        return await conn.Table<StreakTargetCompletion>()
            .Where(c => c.Username == username && c.Game == game && c.ActivityId == activityId)
            .OrderByDescending(c => c.CompletedAt)
            .ToListAsync();
    }

    public async Task<List<StreakTargetCompletion>> GetTargetCompletionsForAttemptAsync(int streakAttemptId)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();

        return await conn.Table<StreakTargetCompletion>()
            .Where(c => c.StreakAttemptId == streakAttemptId)
            .OrderByDescending(c => c.CompletedAt)
            .ToListAsync();
    }

    public async Task<bool> HasTargetCompletionAsync(int streakAttemptId, int targetDays)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();

        var existing = await conn.Table<StreakTargetCompletion>()
            .Where(c => c.StreakAttemptId == streakAttemptId && c.TargetDays == targetDays)
            .FirstOrDefaultAsync();

        return existing != null;
    }

    public async Task DeleteTargetCompletionAsync(int completionId)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        var completion = await conn.Table<StreakTargetCompletion>()
            .FirstOrDefaultAsync(c => c.Id == completionId);

        if (completion != null)
        {
            var attempt = await conn.Table<StreakAttempt>()
                .FirstOrDefaultAsync(a => a.Id == completion.StreakAttemptId);
            if (attempt?.IsActive == true)
            {
                int countBefore = await GetCurrentTargetCompletionCountAsync(conn, completion.Username, completion.TargetDays);
                await LogTargetStatChangeAsync(
                    conn,
                    completion.Username,
                    completion.TargetDays,
                    countBefore,
                    Math.Max(0, countBefore - 1),
                    "deleted",
                    completion.ActivityName,
                    completion.StreakAttemptId,
                    "Target completion row deleted");
            }

            await conn.DeleteAsync(completion);
        }
    }

    public async Task<int> GetCurrentTargetCompletionCountAsync(string username, int targetDays)
    {
        var conn = await _db.GetConnectionAsync();
        return await GetCurrentTargetCompletionCountAsync(conn, username, targetDays);
    }

    public async Task<List<StreakTargetStatLog>> GetTargetStatLogsAsync(string username, int targetDays)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        return await conn.Table<StreakTargetStatLog>()
            .Where(l => l.Username == username && l.TargetDays == targetDays)
            .OrderByDescending(l => l.LoggedAt)
            .ToListAsync();
    }

    public async Task DeleteTargetStatLogAsync(int logId)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        var log = await conn.Table<StreakTargetStatLog>()
            .FirstOrDefaultAsync(l => l.Id == logId);

        if (log != null)
        {
            await conn.DeleteAsync(log);
        }
    }

    public async Task UpdateTargetStatLogAsync(int logId, int countBefore, int countAfter, string? note)
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        var log = await conn.Table<StreakTargetStatLog>()
            .FirstOrDefaultAsync(l => l.Id == logId);

        if (log == null)
        {
            return;
        }

        log.CountBefore = countBefore;
        log.CountAfter = countAfter;
        log.ChangeType = "manual_edit";
        log.Note = note;
        await conn.UpdateAsync(log);
    }

    private async Task<int> GetCurrentTargetCompletionCountAsync(ISQLiteAsyncConnection conn, string username, int targetDays)
    {
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();

        var activeAttemptIds = (await conn.Table<StreakAttempt>()
                .Where(a => a.Username == username && a.IsActive)
                .ToListAsync())
            .Select(a => a.Id)
            .ToHashSet();

        if (activeAttemptIds.Count == 0)
        {
            return 0;
        }

        var completions = await conn.Table<StreakTargetCompletion>()
            .Where(c => c.Username == username && c.TargetDays == targetDays)
            .ToListAsync();

        return completions.Count(c => activeAttemptIds.Contains(c.StreakAttemptId));
    }

    private async Task<Dictionary<int, int>> GetTargetCountsForAttemptReactivationAsync(ISQLiteAsyncConnection conn, StreakAttempt attempt)
    {
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();

        var completions = await conn.Table<StreakTargetCompletion>()
            .Where(c => c.StreakAttemptId == attempt.Id)
            .ToListAsync();

        var counts = new Dictionary<int, int>();
        foreach (int targetDays in completions.Select(c => c.TargetDays).Distinct())
        {
            counts[targetDays] = await GetCurrentTargetCompletionCountAsync(conn, attempt.Username, targetDays);
        }

        return counts;
    }

    private async Task LogTargetStatResetForAttemptAsync(ISQLiteAsyncConnection conn, StreakAttempt attempt, string changeType, string note)
    {
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        var completions = await conn.Table<StreakTargetCompletion>()
            .Where(c => c.StreakAttemptId == attempt.Id)
            .ToListAsync();

        foreach (var group in completions.GroupBy(c => c.TargetDays))
        {
            int countBefore = await GetCurrentTargetCompletionCountAsync(conn, attempt.Username, group.Key);
            int countAfter = Math.Max(0, countBefore - group.Count());
            await LogTargetStatChangeAsync(
                conn,
                attempt.Username,
                group.Key,
                countBefore,
                countAfter,
                changeType,
                attempt.ActivityName,
                attempt.Id,
                note);
        }
    }

    private async Task LogTargetStatReactivationAsync(ISQLiteAsyncConnection conn, StreakAttempt attempt, Dictionary<int, int> countBeforeByTarget)
    {
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetCompletion>();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();

        var completions = await conn.Table<StreakTargetCompletion>()
            .Where(c => c.StreakAttemptId == attempt.Id)
            .ToListAsync();

        foreach (var group in completions.GroupBy(c => c.TargetDays))
        {
            int countBefore = countBeforeByTarget.TryGetValue(group.Key, out var value) ? value : 0;
            int countAfter = countBefore + group.Count();
            await LogTargetStatChangeAsync(
                conn,
                attempt.Username,
                group.Key,
                countBefore,
                countAfter,
                "reactivated",
                attempt.ActivityName,
                attempt.Id,
                "Streak attempt reactivated");
        }
    }

    private async Task LogTargetStatChangeAsync(
        ISQLiteAsyncConnection conn,
        string username,
        int targetDays,
        int countBefore,
        int countAfter,
        string changeType,
        string activityName,
        int streakAttemptId,
        string? note)
    {
        if (countBefore == countAfter)
        {
            return;
        }

        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakTargetStatLog>();
        await conn.InsertAsync(new StreakTargetStatLog
        {
            Username = username,
            TargetDays = targetDays,
            CountBefore = countBefore,
            CountAfter = countAfter,
            ChangeType = changeType,
            ActivityName = activityName,
            StreakAttemptId = streakAttemptId,
            Note = note,
            LoggedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteStreakLogAsync(int logId)
    {
        var conn = await _db.GetConnectionAsync();
        var log = await conn.Table<StreakLog>()
            .FirstOrDefaultAsync(l => l.Id == logId);

        if (log != null)
        {
            await conn.DeleteAsync(log);
        }
    }

    /// <summary>
    /// Log a streak change for history tracking
    /// </summary>
    public async Task LogStreakChangeAsync(int streakAttemptId, int daysBefore, int daysAfter, string changeType, string? note = null)
    {
        var conn = await _db.GetConnectionAsync();
        
        // Ensure the table exists
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakLog>();
        
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
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakLog>();
        
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
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StreakLog>();
        
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
