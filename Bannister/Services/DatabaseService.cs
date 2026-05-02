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
    /// Uses SQLCipher for at-rest encryption. The encryption key is the user's login password.
    /// ConversationPractice module manages its own tables separately for modularity.
    /// </summary>
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _db;
        private static readonly object _initLock = new();
        private bool _isInitialized = false;
        private string? _dbPassword;

        /// <summary>
        /// Get the database file path
        /// </summary>
        public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "bannister.db");

        /// <summary>
        /// Path for the old unencrypted database (used during migration)
        /// </summary>
        private static string LegacyDatabasePath => Path.Combine(FileSystem.AppDataDirectory, "bannister_unencrypted.db");

        /// <summary>
        /// Set the database password (call this after successful login, before any DB access).
        /// This is the user's login password.
        /// </summary>
        public void SetPassword(string password)
        {
            _dbPassword = password;
        }

        /// <summary>
        /// Get the current database password (for other services that need it).
        /// </summary>
        public string? GetDbPassword() => _dbPassword;

        /// <summary>
        /// Check if a password has been set (i.e., user has logged in).
        /// </summary>
        public bool HasPassword => !string.IsNullOrEmpty(_dbPassword);

        /// <summary>
        /// True if a migration from unencrypted just happened this session.
        /// Checked by HomePage to prompt user to delete the old file.
        /// </summary>
        public bool JustMigratedFromUnencrypted { get; private set; } = false;

        /// <summary>
        /// Check if the legacy unencrypted database file still exists on disk.
        /// </summary>
        public static bool LegacyUnencryptedFileExists => File.Exists(LegacyDatabasePath);

        /// <summary>
        /// Delete the legacy unencrypted database file.
        /// </summary>
        public static bool DeleteLegacyUnencryptedFile()
        {
            try
            {
                if (File.Exists(LegacyDatabasePath))
                {
                    File.Delete(LegacyDatabasePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task InitAsync()
        {
            if (_isInitialized && _db != null)
                return;

            if (string.IsNullOrEmpty(_dbPassword))
                throw new InvalidOperationException("Database password not set. User must log in first.");

            // Check if we need to migrate from an unencrypted database
            await MigrateFromUnencryptedAsync();

            lock (_initLock)
            {
                if (_isInitialized && _db != null)
                    return;

                // Create encrypted connection with SQLCipher
                var options = new SQLiteConnectionString(
                    DatabasePath,
                    storeDateTimeAsTicks: false,
                    key: _dbPassword);

                _db = new SQLiteAsyncConnection(options);
            }

            await CreateTablesAsync();
            await CreateIndexesAsync();

            _isInitialized = true;
        }

        /// <summary>
        /// Try to open the database with the given password.
        /// Returns true if the password works, false if it doesn't.
        /// Used by AuthService to verify the DB password before full login.
        /// </summary>
        public async Task<bool> TryOpenWithPasswordAsync(string password)
        {
            if (!File.Exists(DatabasePath))
                return true; // No DB yet, any password is fine (will create new encrypted DB)

            // Check if the file is unencrypted (pre-migration)
            if (await IsUnencryptedAsync())
                return true; // Will be migrated on first InitAsync

            try
            {
                var options = new SQLiteConnectionString(
                    DatabasePath,
                    storeDateTimeAsTicks: false,
                    key: password);

                var testConn = new SQLiteAsyncConnection(options);
                // This will throw if password is wrong
                var count = await testConn.ExecuteScalarAsync<int>("SELECT count(*) FROM sqlite_master");
                await testConn.CloseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the existing database file is unencrypted (pre-SQLCipher migration).
        /// </summary>
        private async Task<bool> IsUnencryptedAsync()
        {
            if (!File.Exists(DatabasePath))
                return false;

            try
            {
                var header = new byte[16];
                using (var fs = File.OpenRead(DatabasePath))
                {
                    if (fs.Length >= 16)
                    {
                        await fs.ReadAsync(header, 0, 16);
                        // Unencrypted SQLite files start with "SQLite format 3\0"
                        return header[0] == 0x53 && header[1] == 0x51 && header[2] == 0x4C &&
                               header[3] == 0x69 && header[4] == 0x74 && header[5] == 0x65;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Migrate an existing unencrypted database to SQLCipher encrypted format.
        /// This runs once - on the first launch after the SQLCipher upgrade.
        /// 
        /// Strategy: Open old DB unencrypted, create new encrypted DB, 
        /// read schema + data from old, write to new. Pure sqlite-net-pcl API.
        /// </summary>
        private async Task MigrateFromUnencryptedAsync()
        {
            if (!File.Exists(DatabasePath))
                return;

            if (!await IsUnencryptedAsync())
            {
                System.Diagnostics.Debug.WriteLine("✓ Database is already encrypted, skipping migration");
                return;
            }

            System.Diagnostics.Debug.WriteLine("⚠ Found unencrypted database - migrating to SQLCipher...");

            try
            {
                // Step 1: Rename the old unencrypted file
                string backupPath = LegacyDatabasePath;
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Move(DatabasePath, backupPath);

                // Step 2: Open the old unencrypted DB (with SQLCipher bundle, an unencrypted
                // DB can be opened by providing an empty key via raw PRAGMA)
                var oldConn = new SQLiteAsyncConnection(
                    new SQLiteConnectionString(backupPath, storeDateTimeAsTicks: false));
                // Tell SQLCipher this is a plaintext database
                await oldConn.ExecuteAsync("PRAGMA key = ''");

                // Step 3: Create new encrypted DB
                string encryptedPath = DatabasePath;
                var newConn = new SQLiteAsyncConnection(
                    new SQLiteConnectionString(encryptedPath, storeDateTimeAsTicks: false, key: _dbPassword));

                // Step 4: Get all table names from old DB
                var tables = await oldConn.QueryScalarsAsync<string>(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'");

                System.Diagnostics.Debug.WriteLine($"  Found {tables.Count} tables to migrate");

                foreach (var tableName in tables)
                {
                    // Get the CREATE TABLE statement
                    var createSql = await oldConn.ExecuteScalarAsync<string>(
                        $"SELECT sql FROM sqlite_master WHERE type='table' AND name=?", tableName);

                    if (string.IsNullOrEmpty(createSql))
                        continue;

                    // Create table in new DB
                    await newConn.ExecuteAsync(createSql);

                    // Copy all rows using INSERT INTO ... SELECT via dump approach
                    // Get column count for parameterized insert
                    var colInfoRows = await oldConn.QueryAsync<MigrationColumnInfo>(
                        $"PRAGMA table_info([{tableName}])");
                    var colNames = colInfoRows.Select(c => c.Name).ToList();

                    if (colNames.Count == 0)
                        continue;

                    // Read all rows from old table as raw values
                    string colList = string.Join(", ", colNames.Select(c => $"[{c}]"));
                    string placeholders = string.Join(", ", colNames.Select((_, i) => $"?"));
                    string insertSql = $"INSERT INTO [{tableName}] ({colList}) VALUES ({placeholders})";

                    // Use a query that returns all columns as text for each row
                    var allRows = await oldConn.QueryAsync<MigrationRowData>(
                        $"SELECT * FROM [{tableName}]");

                    // Unfortunately QueryAsync<T> needs a typed class. 
                    // Instead, use ExecuteAsync with raw SQL to copy via ATTACH.
                    // The old DB is unencrypted, so we can attach the new encrypted one TO it.
                    // But we already moved the file... Let's use a different approach:
                    // Just use the connection to read typed data and insert it.
                    
                    // Actually the simplest reliable approach: use SQL dump via the old connection
                    // attaching the new DB. Since the old connection has SQLCipher loaded but 
                    // the old DB is plaintext (we set PRAGMA key=''), we can attach encrypted.
                    break; // Exit the per-table loop, use ATTACH approach below
                }

                // Close the per-table attempt
                await newConn.CloseAsync();
                if (File.Exists(encryptedPath))
                    File.Delete(encryptedPath);

                // Better approach: Use ATTACH from the plaintext connection
                // Since we told SQLCipher "PRAGMA key = ''" the old DB is open as plaintext.
                // We can ATTACH a new encrypted database and copy everything via SQL.
                await oldConn.ExecuteAsync(
                    $"ATTACH DATABASE '{EscapeSql(encryptedPath)}' AS encrypted KEY '{EscapeSql(_dbPassword!)}'");

                // Copy all tables, indexes, triggers from old to new
                foreach (var tableName in tables)
                {
                    var createSql = await oldConn.ExecuteScalarAsync<string>(
                        "SELECT sql FROM sqlite_master WHERE type='table' AND name=?", tableName);
                    
                    if (string.IsNullOrEmpty(createSql))
                        continue;

                    // Modify CREATE TABLE to target the encrypted database
                    string encCreateSql = createSql.Replace(
                        $"CREATE TABLE \"{tableName}\"", 
                        $"CREATE TABLE IF NOT EXISTS \"encrypted\".\"{tableName}\"");
                    
                    // Also handle without quotes
                    if (!encCreateSql.Contains("encrypted"))
                    {
                        encCreateSql = createSql.Replace(
                            $"CREATE TABLE {tableName}",
                            $"CREATE TABLE IF NOT EXISTS encrypted.[{tableName}]");
                    }

                    // Also handle bracket-quoted names
                    if (!encCreateSql.Contains("encrypted"))
                    {
                        encCreateSql = createSql.Replace(
                            $"CREATE TABLE [{tableName}]",
                            $"CREATE TABLE IF NOT EXISTS encrypted.[{tableName}]");
                    }

                    await oldConn.ExecuteAsync(encCreateSql);

                    // Copy data
                    await oldConn.ExecuteAsync(
                        $"INSERT INTO encrypted.[{tableName}] SELECT * FROM [{tableName}]");

                    var rowCount = await oldConn.ExecuteScalarAsync<int>(
                        $"SELECT COUNT(*) FROM encrypted.[{tableName}]");
                    System.Diagnostics.Debug.WriteLine($"  ✓ {tableName}: {rowCount} rows copied");
                }

                // Copy indexes
                var indexes = await oldConn.QueryScalarsAsync<string>(
                    "SELECT sql FROM sqlite_master WHERE type='index' AND sql IS NOT NULL");
                foreach (var indexSql in indexes)
                {
                    try
                    {
                        // Prefix index creation with encrypted schema
                        var encIndexSql = indexSql.Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS encrypted.");
                        await oldConn.ExecuteAsync(encIndexSql);
                    }
                    catch (Exception ixEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"  ⚠ Index skipped: {ixEx.Message}");
                    }
                }

                await oldConn.ExecuteAsync("DETACH DATABASE encrypted");
                await oldConn.CloseAsync();

                // Step 5: Verify the encrypted database works
                var verifyConn = new SQLiteAsyncConnection(
                    new SQLiteConnectionString(encryptedPath, storeDateTimeAsTicks: false, key: _dbPassword));
                var verifyCount = await verifyConn.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM sqlite_master WHERE type='table'");
                await verifyConn.CloseAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"✓ Migration complete! Encrypted DB has {verifyCount} tables. " +
                    $"Old DB kept as backup at: {backupPath}");

                JustMigratedFromUnencrypted = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Migration failed: {ex.Message}");

                // Restore the old file so the app still works
                if (File.Exists(LegacyDatabasePath) && !File.Exists(DatabasePath))
                {
                    File.Move(LegacyDatabasePath, DatabasePath);
                }

                throw new Exception(
                    $"Failed to encrypt database: {ex.Message}\n\n" +
                    "The unencrypted database has been restored. " +
                    "Please report this issue.", ex);
            }
        }

        // Helper classes for migration queries
        private class MigrationColumnInfo { public string Name { get; set; } = ""; }
        private class MigrationRowData { }

        /// <summary>
        /// Escape single quotes in SQL strings to prevent injection
        /// </summary>
        private static string EscapeSql(string value) => value.Replace("'", "''");

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

            _dbPassword = null;
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

        /// <summary>
        /// Change the database encryption password.
        /// Uses PRAGMA rekey to re-encrypt in place.
        /// Call this when the user changes their login password.
        /// </summary>
        public async Task<bool> ChangeDbPasswordAsync(string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
                return false;

            await InitAsync();

            try
            {
                // PRAGMA rekey re-encrypts the entire database with the new key
                await _db!.ExecuteAsync($"PRAGMA rekey = '{EscapeSql(newPassword)}'");
                _dbPassword = newPassword;

                System.Diagnostics.Debug.WriteLine("✓ Database password changed successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to change DB password: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
