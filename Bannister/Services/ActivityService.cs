using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bannister.Models;

namespace Bannister.Services
{
    public class ActivityService
    {
        private readonly DatabaseService _db;

        public ActivityService(DatabaseService db) => _db = db;

        public async Task<List<Activity>> GetActivitiesAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var result = await conn.Table<Activity>()
                .Where(x => x.Username == username && x.Game == game && x.IsActive)
                .ToListAsync();
            
            System.Diagnostics.Debug.WriteLine($"[GET ACTIVITIES] username='{username}', game='{game}' returned {result.Count} activities");
            return result;
        }

        public async Task<List<Activity>> GetVisibleActivitiesAsync(string username, string game, int currentLevel, bool showAll = false)
        {
            var activities = await GetActivitiesAsync(username, game);
            var now = DateTime.Now;

            System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] Filtering {activities.Count} activities, now={now}, showAll={showAll}");

            var result = activities.Where(a =>
            {
                // Possible activities can have no image (they show black background)
                // Regular activities must have image to be visible in grid
                if (!a.IsPossible && string.IsNullOrEmpty(a.ImagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' HIDDEN - no image and not possible");
                    return false;
                }

                // Check if within date/time range (StartDate and EndDate now include time)
                if (a.StartDate.HasValue && now < a.StartDate.Value)
                {
                    System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' HIDDEN - not started yet (StartDate={a.StartDate.Value})");
                    return false;
                }
                if (a.EndDate.HasValue && now > a.EndDate.Value)
                {
                    System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' HIDDEN - expired (EndDate={a.EndDate.Value}, now={now})");
                    return false;
                }

                // Filter by category - hide "Expired" and "Stale"
                if (a.Category == "Expired")
                {
                    System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' HIDDEN - category is Expired");
                    return false;
                }
                if (a.Category == "Stale")
                {
                    System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' HIDDEN - category is Stale");
                    return false;
                }

                // Check display day restrictions (skip if showAll is true)
                if (!showAll && !a.ShouldDisplayToday)
                {
                    System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' HIDDEN - not scheduled for today");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] '{a.Name}' VISIBLE");
                return true;
            }).ToList();

            System.Diagnostics.Debug.WriteLine($"[GET VISIBLE] Returning {result.Count} visible activities");
            return result;
        }

        public async Task<Activity?> GetActivityAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.GetAsync<Activity>(id);
        }

        public async Task<Activity> CreateActivityAsync(string username, string game, string name, int expGain,
            int meaningfulUntilLevel = 100, string category = "Misc", string imagePath = "")
        {
            var conn = await _db.GetConnectionAsync();
            var activity = new Activity
            {
                Username = username,
                Game = game,
                Name = name,
                ExpGain = expGain,
                MeaningfulUntilLevel = meaningfulUntilLevel,
                Category = category,
                ImagePath = imagePath,
                Multiplier = 1,
                IsActive = true,
                StartDate = DateTime.Now  // Set creation date
            };
            await conn.InsertAsync(activity);
            return activity;
        }

        public async Task<Activity> CreateActivityAsync(Activity activity)
        {
            var conn = await _db.GetConnectionAsync();
            activity.IsActive = true;
            await conn.InsertAsync(activity);
            return activity;
        }

        public async Task UpdateActivityAsync(Activity activity)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(activity);
        }

        public async Task DeleteActivityAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            var activity = await conn.GetAsync<Activity>(id);
            if (activity != null)
            {
                activity.IsActive = false;
                await conn.UpdateAsync(activity);
            }
        }

        /// <summary>
        /// Get expired activities across ALL games for a user (for global expired check)
        /// </summary>
        public async Task<List<Activity>> GetExpiredActivitiesAsync(string username)
        {
            var conn = await _db.GetConnectionAsync();
            var now = DateTime.Now;
            var activities = await conn.Table<Activity>()
                .Where(x => x.Username == username && x.IsActive && x.Category != "Expired")
                .ToListAsync();

            return activities.Where(a => a.EndDate.HasValue && a.EndDate.Value < now).ToList();
        }

        /// <summary>
        /// Get expired activities for a specific game
        /// </summary>
        public async Task<List<Activity>> GetExpiredActivitiesAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var now = DateTime.Now;
            var activities = await conn.Table<Activity>()
                .Where(x => x.Username == username && x.Game == game && x.IsActive && x.Category != "Expired")
                .ToListAsync();

            System.Diagnostics.Debug.WriteLine($"[GET EXPIRED] Checking {activities.Count} activities for game '{game}'");
            System.Diagnostics.Debug.WriteLine($"[GET EXPIRED] Current time: {now}");

            var expired = activities.Where(a => {
                bool isExpired = a.EndDate.HasValue && a.EndDate.Value < now;
                if (a.EndDate.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[GET EXPIRED] '{a.Name}' EndDate={a.EndDate.Value}, Expired={isExpired}");
                }
                return isExpired;
            }).ToList();

            System.Diagnostics.Debug.WriteLine($"[GET EXPIRED] Found {expired.Count} expired activities");
            return expired;
        }

        /// <summary>
        /// Auto-move all expired activities to "Expired" category and return count
        /// </summary>
        public async Task<int> AutoMoveExpiredActivitiesAsync(string username, string game)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO MOVE] AutoMoveExpiredActivitiesAsync called - username='{username}', game='{game}'");
            
            var expiredActivities = await GetExpiredActivitiesAsync(username, game);
            
            System.Diagnostics.Debug.WriteLine($"[AUTO MOVE] Found {expiredActivities.Count} expired activities to move");
            
            foreach (var activity in expiredActivities)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTO MOVE] Moving activity Id={activity.Id}, Name='{activity.Name}', EndDate={activity.EndDate}, OldCategory='{activity.Category}'");
                activity.Category = "Expired";
                await UpdateActivityAsync(activity);
                System.Diagnostics.Debug.WriteLine($"[AUTO MOVE] Activity '{activity.Name}' moved to Expired category");
            }

            System.Diagnostics.Debug.WriteLine($"[AUTO MOVE] Returning count={expiredActivities.Count}");
            return expiredActivities.Count;
        }

        /// <summary>
        /// Get activities in the Expired category, sorted by EndDate descending
        /// </summary>
        public async Task<List<Activity>> GetExpiredCategoryActivitiesAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var activities = await conn.Table<Activity>()
                .Where(x => x.Username == username && x.Game == game && x.IsActive && x.Category == "Expired")
                .ToListAsync();

            return activities.OrderByDescending(a => a.EndDate).ToList();
        }

        /// <summary>
        /// Get stale activities - activities never clicked for over 2 months.
        /// Requires expLogs to determine last usage.
        /// </summary>
        public async Task<List<Activity>> GetStaleActivitiesAsync(string username, string game, List<ExpLog> expLogs)
        {
            var conn = await _db.GetConnectionAsync();
            var now = DateTime.Now;
            var twoMonthsAgo = now.AddMonths(-2);
            
            var activities = await conn.Table<Activity>()
                .Where(x => x.Username == username && x.Game == game && x.IsActive 
                    && x.Category != "Expired" && x.Category != "Stale" && !x.IsPossible)
                .ToListAsync();

            System.Diagnostics.Debug.WriteLine($"[GET STALE] Checking {activities.Count} activities for staleness");
            System.Diagnostics.Debug.WriteLine($"[GET STALE] Two months ago: {twoMonthsAgo}");

            var staleActivities = new List<Activity>();
            
            foreach (var activity in activities)
            {
                var lastLog = expLogs
                    .Where(log => log.ActivityId == activity.Id)
                    .OrderByDescending(log => log.LoggedAt)
                    .FirstOrDefault();

                if (lastLog == null)
                {
                    // Never clicked - check if created over 2 months ago
                    if (activity.StartDate.HasValue && activity.StartDate.Value < twoMonthsAgo)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GET STALE] '{activity.Name}' is STALE - never clicked, created {activity.StartDate.Value}");
                        staleActivities.Add(activity);
                    }
                }
                else
                {
                    // Has been clicked - check if last click was over 2 months ago
                    if (lastLog.LoggedAt < twoMonthsAgo)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GET STALE] '{activity.Name}' is STALE - last clicked {lastLog.LoggedAt}");
                        staleActivities.Add(activity);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[GET STALE] Found {staleActivities.Count} stale activities");
            return staleActivities;
        }

        /// <summary>
        /// Auto-move all stale activities to "Stale" category and return count
        /// </summary>
        public async Task<int> AutoMoveStaleActivitiesAsync(string username, string game, List<ExpLog> expLogs)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTO MOVE STALE] Called for username='{username}', game='{game}'");
            
            var staleActivities = await GetStaleActivitiesAsync(username, game, expLogs);
            
            System.Diagnostics.Debug.WriteLine($"[AUTO MOVE STALE] Found {staleActivities.Count} stale activities to move");
            
            foreach (var activity in staleActivities)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTO MOVE STALE] Moving activity Id={activity.Id}, Name='{activity.Name}' to Stale");
                activity.Category = "Stale";
                await UpdateActivityAsync(activity);
            }

            return staleActivities.Count;
        }

        public async Task<List<Activity>> GetActivitiesWithoutImagesAsync(string username, string game)
        {
            var conn = await _db.GetConnectionAsync();
            var activities = await conn.Table<Activity>()
                .Where(x => x.Username == username && x.Game == game && x.IsActive)
                .ToListAsync();

            return activities.Where(a => string.IsNullOrEmpty(a.ImagePath)).ToList();
        }

        public async Task<List<string>> GetCategoriesAsync(string username, string game)
        {
            var activities = await GetActivitiesAsync(username, game);
            var categories = activities.Select(a => a.Category).Distinct().OrderBy(c => c).ToList();

            // Ensure common categories exist
            var defaults = new[] { "Misc", "Food", "Exercise", "Learning", "Health" };
            foreach (var cat in defaults)
            {
                if (!categories.Contains(cat))
                    categories.Add(cat);
            }

            return categories.OrderBy(c => c).ToList();
        }

        public async Task MoveToExpiredAsync(int activityId)
        {
            var activity = await GetActivityAsync(activityId);
            if (activity != null)
            {
                activity.Category = "Expired";
                await UpdateActivityAsync(activity);
            }
        }

        public async Task PostponeActivityAsync(int activityId, DateTime newEndDate)
        {
            var activity = await GetActivityAsync(activityId);
            if (activity != null)
            {
                activity.EndDate = newEndDate;
                await UpdateActivityAsync(activity);
            }
        }

        /// <summary>
        /// Backfill HabitTargetFirstSet for activities that have HabitTargetDate but no FirstSet.
        /// This is a one-time migration for existing data.
        /// </summary>
        public async Task MigrateHabitTargetFirstSetAsync()
        {
            var conn = await _db.GetConnectionAsync();
            
            // Ensure new columns exist by creating/updating table
            await conn.CreateTableAsync<Activity>();
            
            var allActivities = await conn.Table<Activity>().ToListAsync();
            
            var needsBackfill = allActivities.Where(a => 
                a.HabitTargetDate.HasValue && 
                !a.HabitTargetFirstSet.HasValue
            ).ToList();

            if (needsBackfill.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[MIGRATION] Backfilling HabitTargetFirstSet for {needsBackfill.Count} activities");
                
                foreach (var activity in needsBackfill)
                {
                    // Set FirstSet to now (we don't know when it was originally set)
                    activity.HabitTargetFirstSet = DateTime.Now;
                    activity.HabitTargetPostponeCount = 0;
                    await conn.UpdateAsync(activity);
                    System.Diagnostics.Debug.WriteLine($"[MIGRATION] Set HabitTargetFirstSet for {activity.Name}");
                }
                
                System.Diagnostics.Debug.WriteLine($"[MIGRATION] Backfill complete");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MIGRATION] No activities need backfill (checked {allActivities.Count} total)");
            }
        }

        /// <summary>
        /// Deactivate an activity (used when habit fails or graduates)
        /// </summary>
        public async Task BlankActivityAsync(int activityId)
        {
            var activity = await GetActivityAsync(activityId);
            if (activity != null)
            {
                activity.IsActive = false;
                await UpdateActivityAsync(activity);
                System.Diagnostics.Debug.WriteLine($"[ACTIVITY] Blanked activity {activityId}: {activity.Name}");
            }
        }

        /// <summary>
        /// Reset an activity to default values (removes all data but keeps the row)
        /// </summary>
        public async Task ResetActivityToDefaultsAsync(int activityId)
        {
            var activity = await GetActivityAsync(activityId);
            if (activity != null)
            {
                string oldName = activity.Name;
                
                // Reset all fields to defaults
                activity.Name = "";
                activity.Category = "";
                activity.ExpGain = 0;
                activity.RewardType = "Fixed";
                activity.PercentOfLevel = 0;
                activity.MeaningfulUntilLevel = 100;
                activity.ImagePath = "";
                activity.StartDate = null;
                activity.EndDate = null;
                activity.IsActive = false;
                activity.IsPossible = false;
                activity.Multiplier = 1;
                activity.IsAutoAward = false;
                activity.AutoAwardFrequency = "None";
                activity.AutoAwardDays = "";
                activity.LastAutoAwarded = null;
                activity.LastHabitDate = null;
                activity.HabitStreak = 0;
                activity.HabitType = "None";
                activity.StreakStartDate = null;
                activity.TimesCompleted = 0;
                activity.Notes = "";
                activity.DisplayDaysOfWeek = "";
                activity.DisplayDayOfMonth = 0;
                activity.DisplayDayStreak = 0;
                activity.LastDisplayDayUsed = null;
                activity.HabitTargetDate = null;
                activity.HabitTargetFirstSet = null;
                activity.ShowTimesCompletedBadge = false;
                
                await UpdateActivityAsync(activity);
                System.Diagnostics.Debug.WriteLine($"[ACTIVITY] Reset activity {activityId} (was: {oldName}) to defaults");
            }
        }

        /// <summary>
        /// Record that a habit activity was completed today
        /// </summary>
        public async Task RecordHabitCompletionAsync(Activity activity)
        {
            if (activity == null) return;
            
            // Update habit tracking
            var today = DateTime.Now.Date;
            
            if (activity.HabitType == "None") return;
            
            // Check if already recorded today
            if (activity.LastHabitDate.HasValue && activity.LastHabitDate.Value.Date == today)
            {
                System.Diagnostics.Debug.WriteLine($"[HABIT] Already recorded today for {activity.Name}");
                return;
            }
            
            // Check if streak continues or resets
            int daysSinceLastHabit = activity.LastHabitDate.HasValue 
                ? (int)(today - activity.LastHabitDate.Value.Date).TotalDays 
                : 0;
            
            bool streakContinues = activity.LastHabitDate.HasValue && activity.HabitType switch
            {
                "Daily" => daysSinceLastHabit <= 1,
                "Weekly" => daysSinceLastHabit <= 7,
                "Monthly" => daysSinceLastHabit <= 31,
                _ => false
            };
            
            if (streakContinues)
            {
                // For Daily: increment by 1
                // For Weekly/Monthly: increment by actual days passed (tracks total days habit not broken)
                int increment = activity.HabitType switch
                {
                    "Daily" => 1,
                    "Weekly" => daysSinceLastHabit > 0 ? daysSinceLastHabit : 1,
                    "Monthly" => daysSinceLastHabit > 0 ? daysSinceLastHabit : 1,
                    _ => 1
                };
                activity.HabitStreak += increment;
            }
            else
            {
                activity.HabitStreak = 1; // Reset to 1 (today counts)
            }
            
            activity.LastHabitDate = today;
            await UpdateActivityAsync(activity);
            
            System.Diagnostics.Debug.WriteLine($"[HABIT] Recorded completion for {activity.Name}, streak: {activity.HabitStreak}, isHabit: {activity.IsHabit}");
        }

        /// <summary>
        /// Stores the ID of the last created activity (for habit flow)
        /// </summary>
        public int? LastCreatedActivityId { get; set; }

        /// <summary>
        /// Get and clear the last created activity
        /// </summary>
        public async Task<Activity?> GetAndClearLastCreatedActivityAsync()
        {
            if (!LastCreatedActivityId.HasValue) return null;
            
            var activity = await GetActivityAsync(LastCreatedActivityId.Value);
            LastCreatedActivityId = null;
            return activity;
        }

        /// <summary>
        /// Record that an activity was used today and update its display day streak.
        /// Call this when EXP is awarded for an activity.
        /// </summary>
        public async Task RecordDisplayDayStreakAsync(Activity activity)
        {
            if (activity == null) return;
            
            // Skip if opted out of streak tracking
            if (activity.OptOutDisplayDayStreak) return;
            
            var today = DateTime.Now.Date;
            
            // Skip if already recorded today
            if (activity.LastDisplayDayUsed.HasValue && activity.LastDisplayDayUsed.Value.Date == today)
            {
                System.Diagnostics.Debug.WriteLine($"[DISPLAY STREAK] Already recorded today for {activity.Name}");
                return;
            }
            
            // Check if today is a scheduled display day
            if (!activity.IsScheduledDisplayDay(today))
            {
                System.Diagnostics.Debug.WriteLine($"[DISPLAY STREAK] Today is not a scheduled day for {activity.Name}");
                return;
            }
            
            // Check if streak continues from previous scheduled day
            bool streakContinues = false;
            
            if (activity.LastDisplayDayUsed.HasValue)
            {
                var previousScheduledDay = activity.GetPreviousScheduledDay(today);
                if (previousScheduledDay.HasValue)
                {
                    // Streak continues if last use was on the previous scheduled day
                    streakContinues = activity.LastDisplayDayUsed.Value.Date == previousScheduledDay.Value.Date;
                    System.Diagnostics.Debug.WriteLine($"[DISPLAY STREAK] {activity.Name}: Previous scheduled={previousScheduledDay.Value:d}, LastUsed={activity.LastDisplayDayUsed.Value.Date:d}, Continues={streakContinues}");
                }
            }
            
            if (streakContinues)
            {
                activity.DisplayDayStreak++;
            }
            else
            {
                activity.DisplayDayStreak = 1; // Start new streak
            }
            
            activity.LastDisplayDayUsed = today;
            await UpdateActivityAsync(activity);
            
            System.Diagnostics.Debug.WriteLine($"[DISPLAY STREAK] {activity.Name}: New streak = {activity.DisplayDayStreak}");
        }

        /// <summary>
        /// Check and break streaks for activities that missed their scheduled day.
        /// Call this on app startup or when entering a game.
        /// Returns list of broken streaks with their penalties.
        /// </summary>
        public async Task<List<(Activity activity, int brokenStreak, int penalty)>> CheckAndBreakMissedStreaksAsync(string username, string game)
        {
            var brokenStreaks = new List<(Activity activity, int brokenStreak, int penalty)>();
            var processedActivityIds = new HashSet<int>(); // Prevent duplicates
            var activities = await GetActivitiesAsync(username, game);
            var today = DateTime.Now.Date;
            
            foreach (var activity in activities)
            {
                // Skip if already processed (prevent duplicates)
                if (processedActivityIds.Contains(activity.Id))
                    continue;
                
                // Skip negative EXP activities (penalties shouldn't have streaks)
                if (activity.ExpGain < 0)
                    continue;
                
                // Skip if opted out or no streak
                if (activity.OptOutDisplayDayStreak || activity.DisplayDayStreak == 0)
                    continue;
                
                // Skip if already used today
                if (activity.LastDisplayDayUsed.HasValue && activity.LastDisplayDayUsed.Value.Date == today)
                    continue;
                
                // Check if any scheduled day was missed since last use
                if (activity.LastDisplayDayUsed.HasValue)
                {
                    var lastUsed = activity.LastDisplayDayUsed.Value.Date;
                    var checkDate = lastUsed.AddDays(1);
                    
                    while (checkDate < today)
                    {
                        if (activity.IsScheduledDisplayDay(checkDate))
                        {
                            // Missed a scheduled day - calculate penalty and break streak
                            int brokenStreak = activity.DisplayDayStreak;
                            int penalty = CalculateStreakBreakPenalty(brokenStreak);
                            
                            System.Diagnostics.Debug.WriteLine($"[DISPLAY STREAK] {activity.Name} (ID:{activity.Id}): Missed scheduled day {checkDate:d}, breaking streak of {brokenStreak}, penalty: {penalty}");
                            
                            brokenStreaks.Add((activity, brokenStreak, penalty));
                            processedActivityIds.Add(activity.Id);
                            
                            activity.DisplayDayStreak = 0;
                            await UpdateActivityAsync(activity);
                            break;
                        }
                        checkDate = checkDate.AddDays(1);
                    }
                }
            }
            
            return brokenStreaks;
        }

        /// <summary>
        /// Calculate bonus EXP for current streak milestone.
        /// Returns bonus EXP to add (0 if not at a milestone).
        /// </summary>
        public static int CalculateStreakBonus(int currentStreak)
        {
            // Milestone bonuses - triggered when reaching these streaks
            // Streak 3: +5 EXP
            // Streak 5: +10 EXP
            // Streak 7: +20 EXP (1 week)
            // Streak 10: +30 EXP
            // Streak 14: +50 EXP (2 weeks)
            // Streak 21: +75 EXP (3 weeks)
            // Streak 30: +100 EXP (1 month)
            // Streak 60: +200 EXP (2 months)
            // Streak 90: +300 EXP (3 months)
            // Streak 180: +500 EXP (6 months)
            // Streak 365: +1000 EXP (1 year)
            
            return currentStreak switch
            {
                3 => 5,
                5 => 10,
                7 => 20,
                10 => 30,
                14 => 50,
                21 => 75,
                30 => 100,
                45 => 150,
                60 => 200,
                90 => 300,
                120 => 400,
                180 => 500,
                270 => 750,
                365 => 1000,
                _ => 0
            };
        }

        /// <summary>
        /// Calculate penalty EXP for breaking a streak.
        /// Returns negative EXP to apply.
        /// </summary>
        public static int CalculateStreakBreakPenalty(int brokenStreak)
        {
            // Penalty scales with how big the streak was
            // Streak 1-2: No penalty (too short)
            // Streak 3-4: -5 EXP
            // Streak 5-6: -10 EXP
            // Streak 7-9: -20 EXP
            // Streak 10-13: -30 EXP
            // Streak 14-20: -50 EXP
            // Streak 21-29: -75 EXP
            // Streak 30-44: -100 EXP
            // Streak 45-59: -150 EXP
            // Streak 60-89: -200 EXP
            // Streak 90-119: -300 EXP
            // Streak 120-179: -400 EXP
            // Streak 180-269: -500 EXP
            // Streak 270-364: -750 EXP
            // Streak 365+: -1000 EXP
            
            if (brokenStreak < 3) return 0;
            if (brokenStreak < 5) return -5;
            if (brokenStreak < 7) return -10;
            if (brokenStreak < 10) return -20;
            if (brokenStreak < 14) return -30;
            if (brokenStreak < 21) return -50;
            if (brokenStreak < 30) return -75;
            if (brokenStreak < 45) return -100;
            if (brokenStreak < 60) return -150;
            if (brokenStreak < 90) return -200;
            if (brokenStreak < 120) return -300;
            if (brokenStreak < 180) return -400;
            if (brokenStreak < 270) return -500;
            if (brokenStreak < 365) return -750;
            return -1000;
        }

        /// <summary>
        /// Get description of next milestone for a streak.
        /// </summary>
        public static string GetNextMilestoneInfo(int currentStreak)
        {
            int[] milestones = { 3, 5, 7, 10, 14, 21, 30, 45, 60, 90, 120, 180, 270, 365 };
            
            foreach (int milestone in milestones)
            {
                if (currentStreak < milestone)
                {
                    int daysLeft = milestone - currentStreak;
                    int bonus = CalculateStreakBonus(milestone);
                    return $"{daysLeft} days to +{bonus} bonus";
                }
            }
            
            return "Max milestone reached!";
        }
    }
}
