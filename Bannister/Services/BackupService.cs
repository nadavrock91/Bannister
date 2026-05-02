using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class BackupService
{
    private readonly DatabaseService _db;
    private string BackupFolder => Path.Combine(FileSystem.AppDataDirectory, "Backups");
    private string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "bannister.db");

    public BackupService(DatabaseService db)
    {
        _db = db;
        Directory.CreateDirectory(BackupFolder);
    }

    /// <summary>
    /// Create an automatic backup by copying the database file.
    /// The backup file is a copy of the encrypted .db - same password applies.
    /// </summary>
    public async Task<(bool success, string message)> AutoBackupAsync(string reason = "auto")
    {
        try
        {
            if (!File.Exists(DatabasePath))
                return (false, "No database to backup");

            int iteration = GetNextIteration(reason);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"bannister_{reason}_{timestamp}_{iteration}.db";
            string backupPath = Path.Combine(BackupFolder, fileName);

            // Copy the database file (the backup is encrypted with the same password)
            File.Copy(DatabasePath, backupPath, overwrite: true);
            await CleanupOldBackupsAsync(reason, 20);

            return (true, $"Backup created: {fileName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto backup failed: {ex.Message}");
            return (false, $"Backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a manual backup by copying the database file
    /// </summary>
    public async Task<(bool success, string message, string? filePath)> ManualBackupAsync()
    {
        try
        {
            if (!File.Exists(DatabasePath))
                return (false, "No database to backup", null);

            int iteration = GetNextIteration("manual");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"bannister_backup_{timestamp}_{iteration}.db";
            string backupPath = Path.Combine(BackupFolder, fileName);

            // Copy the database file (encrypted with same password)
            File.Copy(DatabasePath, backupPath, overwrite: true);

            string message = $"Database backed up successfully!\n\n" +
                           $"Backup #{iteration}\n" +
                           $"File: {fileName}\n" +
                           $"Location: {BackupFolder}\n\n" +
                           $"🔒 Backup is encrypted with your login password.";

            return (true, message, backupPath);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to backup database:\n{ex.Message}", null);
        }
    }

    /// <summary>
    /// Restore database from a .db file by copying it to the app's database location.
    /// The backup file must be encrypted with the same login password.
    /// WARNING: App must be restarted after this operation.
    /// </summary>
    public async Task<(bool success, string message)> RestoreFromDbFileAsync(string sourceDbPath)
    {
        try
        {
            if (!File.Exists(sourceDbPath))
                return (false, "Database file not found");

            if (File.Exists(DatabasePath))
            {
                return (false, "Database already exists.\n\nGo to Settings → Database Management → Delete Current Database first.");
            }

            File.Copy(sourceDbPath, DatabasePath, overwrite: false);

            // Verify the restored file can be opened with the current password
            var dbPassword = _db.GetDbPassword();
            if (!string.IsNullOrEmpty(dbPassword))
            {
                try
                {
                    var options = new SQLiteConnectionString(DatabasePath, storeDateTimeAsTicks: false, key: dbPassword);
                    var testConn = new SQLiteAsyncConnection(options);
                    var count = await testConn.ExecuteScalarAsync<int>("SELECT count(*) FROM sqlite_master");
                    await testConn.CloseAsync();
                }
                catch
                {
                    // Password doesn't match - remove the file and report
                    File.Delete(DatabasePath);
                    return (false, "Cannot open backup: wrong password.\n\n" +
                        "The backup was created with a different login password.\n" +
                        "Log in with the password you used when this backup was created.");
                }
            }

            return (true, $"Database restored successfully!\n\nFile: {Path.GetFileName(sourceDbPath)}\n\n⚠️ RESTART THE APP to use the restored database.");
        }
        catch (Exception ex)
        {
            return (false, $"Restore failed:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Delete the current database file (for restore purposes)
    /// WARNING: App must be restarted after this operation
    /// </summary>
    public (bool success, string message) DeleteCurrentDatabase()
    {
        try
        {
            if (!File.Exists(DatabasePath))
                return (false, "No database file found");

            File.Delete(DatabasePath);

            return (true, "Database deleted.\n\n⚠️ RESTART THE APP, then you can restore from backup.");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("being used") || ex.Message.Contains("locked"))
            {
                return (false, "Database is in use.\n\n⚠️ CLOSE AND RESTART THE APP, then delete again.");
            }
            return (false, $"Failed to delete database:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Get all backup files sorted by creation time (newest first)
    /// </summary>
    public List<BackupFileInfo> GetBackupFiles()
    {
        try
        {
            if (!Directory.Exists(BackupFolder))
                return new List<BackupFileInfo>();

            // Get both .db and .json files (for backwards compatibility)
            var dbFiles = Directory.GetFiles(BackupFolder, "bannister_*.db");
            var jsonFiles = Directory.GetFiles(BackupFolder, "bannister_*.json");
            var files = dbFiles.Concat(jsonFiles).ToArray();
            
            return files
                .Select(f => new BackupFileInfo
                {
                    FilePath = f,
                    FileName = Path.GetFileName(f),
                    CreatedAt = File.GetCreationTime(f),
                    SizeBytes = new FileInfo(f).Length,
                    Reason = ExtractReasonFromFileName(Path.GetFileName(f))
                })
                .OrderByDescending(b => b.CreatedAt)
                .ToList();
        }
        catch
        {
            return new List<BackupFileInfo>();
        }
    }

    /// <summary>
    /// Delete a specific backup file
    /// </summary>
    public bool DeleteBackup(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clean up old backups, keeping only the most recent N backups for a specific reason
    /// </summary>
    public async Task CleanupOldBackupsAsync(string reason, int keepCount)
    {
        try
        {
            if (!Directory.Exists(BackupFolder))
                return;

            // Clean up both .db and .json files
            var dbBackups = Directory.GetFiles(BackupFolder, $"bannister_{reason}_*.db");
            var jsonBackups = Directory.GetFiles(BackupFolder, $"bannister_{reason}_*.json");
            var backups = dbBackups.Concat(jsonBackups).ToArray();

            if (backups.Length <= keepCount)
                return;

            Array.Sort(backups, (a, b) => 
                File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

            int toDelete = backups.Length - keepCount;
            for (int i = 0; i < toDelete; i++)
            {
                try
                {
                    File.Delete(backups[i]);
                }
                catch { }
            }

            await Task.CompletedTask;
        }
        catch { }
    }

    /// <summary>
    /// Clean up all backup types
    /// </summary>
    public async Task CleanupAllBackupsAsync()
    {
        await CleanupOldBackupsAsync("auto", 30);
        await CleanupOldBackupsAsync("login", 20);
        await CleanupOldBackupsAsync("logout", 20);
        await CleanupOldBackupsAsync("manual", 10);
    }

    /// <summary>
    /// Get total size of all backups
    /// </summary>
    public long GetTotalBackupSize()
    {
        try
        {
            if (!Directory.Exists(BackupFolder))
                return 0;

            var dbSize = Directory.GetFiles(BackupFolder, "*.db")
                .Sum(f => new FileInfo(f).Length);
            var jsonSize = Directory.GetFiles(BackupFolder, "*.json")
                .Sum(f => new FileInfo(f).Length);
                
            return dbSize + jsonSize;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get backup folder path
    /// </summary>
    public string GetBackupFolderPath() => BackupFolder;

    private int GetNextIteration(string reason)
    {
        int iteration = 1;

        if (!Directory.Exists(BackupFolder))
            return iteration;

        // Check both .db and .json files
        var dbBackups = Directory.GetFiles(BackupFolder, $"bannister_{reason}_*.db");
        var jsonBackups = Directory.GetFiles(BackupFolder, $"bannister_{reason}_*.json");
        var existingBackups = dbBackups.Concat(jsonBackups).ToArray();

        foreach (var backup in existingBackups)
        {
            string fileName = Path.GetFileNameWithoutExtension(backup);
            var parts = fileName.Split('_');
            
            if (parts.Length >= 4 && int.TryParse(parts[^1], out int num))
            {
                iteration = Math.Max(iteration, num + 1);
            }
        }

        return iteration;
    }

    private string ExtractReasonFromFileName(string fileName)
    {
        try
        {
            var parts = fileName.Split('_');
            if (parts.Length >= 2)
                return parts[1];
            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

/// <summary>
/// Information about a backup file
/// </summary>
public class BackupFileInfo
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string Reason { get; set; } = "";

    public string SizeDisplay
    {
        get
        {
            if (SizeBytes < 1024)
                return $"{SizeBytes} B";
            else if (SizeBytes < 1024 * 1024)
                return $"{SizeBytes / 1024.0:F1} KB";
            else
                return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    public string ReasonDisplay => Reason switch
    {
        "auto" => "Automatic",
        "login" => "Login",
        "logout" => "Logout",
        "manual" => "Manual",
        _ => "Unknown"
    };
    
    public bool IsDbFile => FilePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
    public bool IsJsonFile => FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
