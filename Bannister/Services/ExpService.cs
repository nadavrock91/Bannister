using Bannister.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bannister.Services
{
    public class ExpService
    {
        private readonly DatabaseService _db;

        public ExpService(DatabaseService db) => _db = db;

        public async Task EnsureUserStateAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var existing = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                // Calculate total EXP from historical exp_log entries
                var logs = await conn.Table<ExpLog>()
                    .Where(x => x.Username == username && x.Game == game)
                    .ToListAsync();
                
                int totalExp = logs.Sum(log => log.DeltaExp);
                
                System.Diagnostics.Debug.WriteLine($"EnsureUserStateAsync: Creating new state for {username}/{game}");
                System.Diagnostics.Debug.WriteLine($"  Found {logs.Count} historical exp_log entries");
                System.Diagnostics.Debug.WriteLine($"  Calculated TotalExp from history: {totalExp}");

                await conn.InsertAsync(new ExpState
                {
                    Username = username,
                    Game = game,
                    TotalExp = totalExp, // Use calculated total from history
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        public async Task<int> GetTotalExpAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var state = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();
            return state?.TotalExp ?? 0;
        }

        public async Task<(int level, int expIntoLevel, int expNeeded)> GetProgressAsync(string username, string game)
        {
            int totalExp = await GetTotalExpAsync(username, game);
            return ExpEngine.GetProgress(totalExp);
        }

        /// <summary>
        /// Check if there's an active level cap blocking EXP gain.
        /// Returns the blocking activity if capped, null otherwise.
        /// </summary>
        public async Task<Activity?> GetBlockingLevelCapAsync(string username, string game, int currentLevel)
        {
            var conn = await _db.GetConnectionAsync();
            
            // Find activities with level caps
            var cappedActivities = await conn.Table<Activity>()
                .Where(a => a.Username == username && a.Game == game && a.HasLevelCap && a.IsActive)
                .ToListAsync();

            foreach (var activity in cappedActivities)
            {
                // Check if we're at or past the cap level AND streak requirement not met
                if (currentLevel >= activity.LevelCapAt && activity.DisplayDayStreak < activity.LevelCapStreakRequired)
                {
                    return activity;
                }
            }

            return null;
        }

        /// <summary>
        /// Get all active level caps for display purposes.
        /// </summary>
        public async Task<List<(Activity activity, bool isBlocking)>> GetAllLevelCapsAsync(string username, string game, int currentLevel)
        {
            var conn = await _db.GetConnectionAsync();
            
            var cappedActivities = await conn.Table<Activity>()
                .Where(a => a.Username == username && a.Game == game && a.HasLevelCap && a.IsActive)
                .ToListAsync();

            var result = new List<(Activity, bool)>();
            foreach (var activity in cappedActivities)
            {
                bool isBlocking = currentLevel >= activity.LevelCapAt && 
                                  activity.DisplayDayStreak < activity.LevelCapStreakRequired;
                result.Add((activity, isBlocking));
            }

            return result.OrderBy(x => x.Item1.LevelCapAt).ToList();
        }

        public async Task<int> ApplyExpAsync(string username, string game, string activityName, int deltaExp, int activityId = 0)
        {
            var conn = await _db.GetConnectionAsync();

            // Get current state
            var state = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();

            int currentExp = state?.TotalExp ?? 0;
            var (currentLevel, _, _) = ExpEngine.GetProgress(currentExp);

            // Check for level cap blocking (only for positive EXP)
            int actualDeltaExp = deltaExp;
            string blockedReason = null;
            
            if (deltaExp > 0)
            {
                var blockingActivity = await GetBlockingLevelCapAsync(username, game, currentLevel);
                if (blockingActivity != null)
                {
                    // Calculate how much EXP would be needed to level up
                    int expForNextLevel = ExpEngine.CumulativeExpForLevel(currentLevel + 1);
                    
                    // If gaining this EXP would push us to next level, cap it
                    if (currentExp + deltaExp >= expForNextLevel)
                    {
                        // Cap EXP so we stay at exactly the threshold minus 1
                        int maxAllowedExp = expForNextLevel - 1;
                        actualDeltaExp = Math.Max(0, maxAllowedExp - currentExp);
                        
                        blockedReason = $"Level capped at {blockingActivity.LevelCapAt} by '{blockingActivity.Name}' " +
                                       $"(need {blockingActivity.LevelCapStreakRequired}-day streak, current: {blockingActivity.DisplayDayStreak})";
                        
                        System.Diagnostics.Debug.WriteLine($"[LEVEL CAP] {blockedReason}");
                        System.Diagnostics.Debug.WriteLine($"[LEVEL CAP] Original EXP: {deltaExp}, Capped to: {actualDeltaExp}");
                    }
                }
            }

            int newTotal = currentExp + actualDeltaExp;

            // Calculate levels before and after
            var (levelBefore, _, _) = ExpEngine.GetProgress(currentExp);
            var (levelAfter, _, _) = ExpEngine.GetProgress(newTotal);

            // Update or insert state
            if (state != null)
            {
                state.TotalExp = newTotal;
                state.UpdatedAt = DateTime.UtcNow;
                await conn.UpdateAsync(state);
            }
            else
            {
                await conn.InsertAsync(new ExpState
                {
                    Username = username,
                    Game = game,
                    TotalExp = newTotal,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Log the change with level tracking
            // Include blocked info in activity name if applicable
            string loggedName = blockedReason != null 
                ? $"{activityName} [CAPPED: {actualDeltaExp}/{deltaExp}]"
                : activityName;

            await conn.InsertAsync(new ExpLog
            {
                Username = username,
                Game = game,
                ActivityId = activityId,
                ActivityName = loggedName,
                DeltaExp = actualDeltaExp,
                TotalExp = newTotal,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                LoggedAt = DateTime.UtcNow
            });

            return newTotal;
        }

        /// <summary>
        /// Handle level down when a streak is broken on a level-cap activity.
        /// Returns true if a level down occurred.
        /// </summary>
        public async Task<(bool leveledDown, int fromLevel, int toLevel, int expLost)> 
            HandleStreakBreakLevelDownAsync(string username, string game, Activity activity)
        {
            if (!activity.HasLevelCap || !activity.LevelDownOnStreakBreak)
            {
                return (false, 0, 0, 0);
            }

            var conn = await _db.GetConnectionAsync();
            
            var state = await conn.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == game)
                .FirstOrDefaultAsync();

            if (state == null) return (false, 0, 0, 0);

            var (currentLevel, _, _) = ExpEngine.GetProgress(state.TotalExp);

            // Only level down if we're above the cap level
            if (currentLevel <= activity.LevelCapAt)
            {
                return (false, 0, 0, 0);
            }

            // Calculate EXP for the cap level (start of that level)
            int targetExp = ExpEngine.CumulativeExpForLevel(activity.LevelCapAt);
            int expLost = state.TotalExp - targetExp;

            // Set EXP to start of cap level
            state.TotalExp = targetExp;
            state.UpdatedAt = DateTime.UtcNow;
            await conn.UpdateAsync(state);

            // Log the level down
            await conn.InsertAsync(new ExpLog
            {
                Username = username,
                Game = game,
                ActivityId = activity.Id,
                ActivityName = $"⚠️ LEVEL DOWN: Broke streak on '{activity.Name}'",
                DeltaExp = -expLost,
                TotalExp = targetExp,
                LevelBefore = currentLevel,
                LevelAfter = activity.LevelCapAt,
                LoggedAt = DateTime.UtcNow
            });

            System.Diagnostics.Debug.WriteLine($"[LEVEL DOWN] {activity.Name} streak broken!");
            System.Diagnostics.Debug.WriteLine($"[LEVEL DOWN] Level {currentLevel} -> {activity.LevelCapAt}, lost {expLost} EXP");

            return (true, currentLevel, activity.LevelCapAt, expLost);
        }

        public async Task<List<ExpLog>> GetRecentLogsAsync(string username, string game, int limit = 20)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.Table<ExpLog>()
                .Where(x => x.Username == username && x.Game == game)
                .OrderByDescending(x => x.Id)
                .Take(limit)
                .ToListAsync();
        }
    }
}
