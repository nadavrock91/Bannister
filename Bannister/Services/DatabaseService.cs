using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Bannister.Models;

namespace Bannister.Services
{
    /// <summary>
    /// Centralized database service handling all Bannister table creation, migrations, and data access.
    /// ConversationPractice module manages its own tables separately for modularity.
    /// </summary>
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _db;
        private static readonly object _initLock = new();
        private bool _isInitialized = false;

        /// <summary>
        /// Get the database file path
        /// </summary>
        public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "bannister.db");

        private async Task InitAsync()
        {
            if (_isInitialized && _db != null)
                return;

            lock (_initLock)
            {
                if (_isInitialized && _db != null)
                    return;

                // Store DateTime as readable ISO8601 strings instead of ticks
                _db = new SQLiteAsyncConnection(DatabasePath, storeDateTimeAsTicks: false);
            }

            await CreateTablesAsync();
            await CreateIndexesAsync();

            _isInitialized = true;
        }

        #region Schema Creation

        /// <summary>
        /// Create all Bannister core tables. 
        /// SQLite CreateTable is idempotent - safe to call multiple times.
        /// Note: ConversationPractice module manages its own tables for modularity.
        /// </summary>
        private async Task CreateTablesAsync()
        {
            await _db!.CreateTableAsync<User>();
            await _db!.CreateTableAsync<Game>();
            await _db!.CreateTableAsync<Activity>();
            await _db!.CreateTableAsync<Dragon>();
            await _db!.CreateTableAsync<ExpState>();
            await _db!.CreateTableAsync<ExpLog>();
            await _db!.CreateTableAsync<Attempt>();
            await _db!.CreateTableAsync<StreakAttempt>();

            System.Diagnostics.Debug.WriteLine("✓ All Bannister tables created/verified");
        }

        /// <summary>
        /// Create indexes for performance optimization.
        /// Uses IF NOT EXISTS for idempotency.
        /// </summary>
        private async Task CreateIndexesAsync()
        {
            await _db!.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_activity_autoaward 
                ON game_activities(IsAutoAward, AutoAwardFrequency)");

            await _db!.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_explog_user_game_date 
                ON exp_log(Username, Game, LoggedAt)");

            await _db!.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_streak_user_game_active 
                ON streak_attempts(Username, Game, IsActive)");

            await _db!.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_streak_activity 
                ON streak_attempts(Username, Game, ActivityId)");

            System.Diagnostics.Debug.WriteLine("✓ All indexes created/verified");
        }

        #endregion



        #region Public API

        public async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            await InitAsync();
            return _db!;
        }

        #endregion

        #region EXP Logging

        public async Task LogExpAsync(string username, string gameId, int activityId,
            string activityName, int expEarned, int levelBefore, int levelAfter)
        {
            await InitAsync();

            var expState = await _db!.Table<ExpState>()
                .Where(x => x.Username == username && x.Game == gameId)
                .FirstOrDefaultAsync();

            var log = new ExpLog
            {
                Username = username,
                Game = gameId,
                ActivityId = activityId,
                ActivityName = activityName,
                DeltaExp = expEarned,
                TotalExp = expState?.TotalExp ?? 0,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                LoggedAt = DateTime.UtcNow
            };

            await _db!.InsertAsync(log);
        }

        public async Task<List<ExpLog>> GetExpLogsForGameAsync(string username, string gameId)
        {
            await InitAsync();
            return await _db!.Table<ExpLog>()
                .Where(x => x.Username == username && x.Game == gameId)
                .OrderByDescending(x => x.LoggedAt)
                .ToListAsync();
        }

        public async Task<List<ExpLog>> GetExpLogsForDateRangeAsync(string username, string gameId,
            DateTime startDate, DateTime endDate)
        {
            await InitAsync();
            return await _db!.Table<ExpLog>()
                .Where(x => x.Username == username
                    && x.Game == gameId
                    && x.LoggedAt >= startDate
                    && x.LoggedAt <= endDate)
                .OrderByDescending(x => x.LoggedAt)
                .ToListAsync();
        }

        public async Task<int> GetTotalExpForGameAsync(string username, string gameId)
        {
            await InitAsync();
            var logs = await _db!.Table<ExpLog>()
                .Where(x => x.Username == username && x.Game == gameId)
                .ToListAsync();

            return logs.Where(x => x.DeltaExp > 0).Sum(x => x.DeltaExp);
        }

        public async Task<int> GetActivityCompletionCountAsync(string username, string gameId, int activityId)
        {
            await InitAsync();
            return await _db!.Table<ExpLog>()
                .Where(x => x.Username == username && x.Game == gameId && x.ActivityId == activityId)
                .CountAsync();
        }

        public async Task<int> GetExpEarnedTodayAsync(string username, string gameId)
        {
            await InitAsync();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var logs = await _db!.Table<ExpLog>()
                .Where(x => x.Username == username
                    && x.Game == gameId
                    && x.LoggedAt >= today
                    && x.LoggedAt < tomorrow)
                .ToListAsync();

            return logs.Where(x => x.DeltaExp > 0).Sum(x => x.DeltaExp);
        }

        public async Task<int> GetExpEarnedThisWeekAsync(string username, string gameId)
        {
            await InitAsync();
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            var logs = await _db!.Table<ExpLog>()
                .Where(x => x.Username == username
                    && x.Game == gameId
                    && x.LoggedAt >= startOfWeek)
                .ToListAsync();

            return logs.Where(x => x.DeltaExp > 0).Sum(x => x.DeltaExp);
        }

        #endregion

        #region Database Management

        public async Task DeleteDatabaseAsync()
        {
            if (_db != null)
            {
                await _db.CloseAsync();
                _db = null;
                _isInitialized = false;
            }

            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }
        }

        public async Task<string> ExportDataAsync()
        {
            await InitAsync();

            var data = new
            {
                ExportedAt = DateTime.UtcNow,
                Users = await _db!.Table<User>().ToListAsync(),
                Games = await _db!.Table<Game>().ToListAsync(),
                Activities = await _db!.Table<Activity>().ToListAsync(),
                Dragons = await _db!.Table<Dragon>().ToListAsync(),
                DragonAttempts = await _db!.Table<Attempt>().ToListAsync(),
                StreakAttempts = await _db!.Table<StreakAttempt>().ToListAsync(),
                ExpStates = await _db!.Table<ExpState>().ToListAsync(),
                ExpLogs = await _db!.Table<ExpLog>().ToListAsync()
            };

            return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Force reinitialization of the database connection.
        /// Useful after restoring from backup.
        /// </summary>
        public async Task ReinitializeAsync()
        {
            if (_db != null)
            {
                await _db.CloseAsync();
                _db = null;
            }
            _isInitialized = false;
            await InitAsync();
        }

        #endregion
    }
}
