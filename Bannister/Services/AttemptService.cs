using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class AttemptService
{
    private readonly DatabaseService _db;

    public AttemptService(DatabaseService db) => _db = db;

    /// <summary>
    /// Get the currently active attempt for a specific dragon
    /// </summary>
    public async Task<Attempt?> GetActiveAttemptAsync(string username, string game, string dragonTitle)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Attempt>()
            .Where(a => a.Username == username && 
                       a.Game == game && 
                       a.DragonTitle == dragonTitle && 
                       a.IsActive)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all attempts for a specific dragon, ordered by attempt number descending
    /// </summary>
    public async Task<List<Attempt>> GetAttemptsForDragonAsync(string username, string game, string dragonTitle)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Attempt>()
            .Where(a => a.Username == username && 
                       a.Game == game && 
                       a.DragonTitle == dragonTitle)
            .OrderByDescending(a => a.AttemptNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Get the latest attempt for each dragon (for overview screen)
    /// </summary>
    public async Task<List<Attempt>> GetLatestAttemptPerDragonAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        var allAttempts = await conn.Table<Attempt>()
            .Where(a => a.Username == username)
            .ToListAsync();

        // Group by dragon and get the latest attempt for each
        return allAttempts
            .GroupBy(a => new { a.Game, a.DragonTitle })
            .Select(g => g.OrderByDescending(a => a.AttemptNumber).First())
            .OrderByDescending(a => a.StartedAt ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>
    /// Get all active attempts across all dragons for a user
    /// </summary>
    public async Task<List<Attempt>> GetActiveAttemptsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Attempt>()
            .Where(a => a.Username == username && a.IsActive)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get the next attempt number for a dragon
    /// </summary>
    public async Task<int> GetNextAttemptNumberAsync(string username, string game, string dragonTitle)
    {
        var conn = await _db.GetConnectionAsync();
        var attempts = await conn.Table<Attempt>()
            .Where(a => a.Username == username && 
                       a.Game == game && 
                       a.DragonTitle == dragonTitle)
            .ToListAsync();

        if (attempts.Count == 0)
            return 1;

        return attempts.Max(a => a.AttemptNumber) + 1;
    }

    /// <summary>
    /// Start a new attempt for a dragon
    /// This automatically deactivates any existing active attempt
    /// </summary>
    public async Task<Attempt> StartNewAttemptAsync(string username, string game, string dragonTitle, string notes = "")
    {
        var conn = await _db.GetConnectionAsync();

        // Deactivate any existing active attempt (but don't mark as failed)
        var existingActive = await GetActiveAttemptAsync(username, game, dragonTitle);
        if (existingActive != null)
        {
            existingActive.IsActive = false;
            await conn.UpdateAsync(existingActive);
        }

        // Get next attempt number
        int nextNumber = await GetNextAttemptNumberAsync(username, game, dragonTitle);

        // Create new attempt
        var attempt = new Attempt
        {
            Username = username,
            Game = game,
            DragonTitle = dragonTitle,
            AttemptNumber = nextNumber,
            StartedAt = DateTime.UtcNow,
            IsActive = true,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(attempt);
        return attempt;
    }

    /// <summary>
    /// Mark the current active attempt as failed
    /// </summary>
    public async Task<bool> MarkAttemptFailedAsync(string username, string game, string dragonTitle, string notes = "")
    {
        var attempt = await GetActiveAttemptAsync(username, game, dragonTitle);
        if (attempt == null)
            return false;

        var conn = await _db.GetConnectionAsync();
        attempt.IsActive = false;
        attempt.FailedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(notes))
            attempt.Notes = notes;

        await conn.UpdateAsync(attempt);
        return true;
    }

    /// <summary>
    /// Mark an attempt as successful (when dragon is slain)
    /// This is called automatically when the user reaches level 100
    /// </summary>
    public async Task<bool> MarkAttemptSuccessfulAsync(string username, string game, string dragonTitle)
    {
        var attempt = await GetActiveAttemptAsync(username, game, dragonTitle);
        if (attempt == null)
            return false;

        var conn = await _db.GetConnectionAsync();
        attempt.IsActive = false;
        // Don't set FailedAt - absence of FailedAt means success
        await conn.UpdateAsync(attempt);
        return true;
    }

    /// <summary>
    /// Update attempt notes
    /// </summary>
    public async Task<bool> UpdateAttemptNotesAsync(int attemptId, string notes)
    {
        var conn = await _db.GetConnectionAsync();
        var attempt = await conn.GetAsync<Attempt>(attemptId);
        if (attempt == null)
            return false;

        attempt.Notes = notes;
        await conn.UpdateAsync(attempt);
        return true;
    }

    /// <summary>
    /// Delete an attempt (soft delete by making it inactive)
    /// </summary>
    public async Task<bool> DeleteAttemptAsync(int attemptId)
    {
        var conn = await _db.GetConnectionAsync();
        var attempt = await conn.GetAsync<Attempt>(attemptId);
        if (attempt == null)
            return false;

        // For now, just delete it
        // You could add an IsDeleted flag if you want soft deletes
        await conn.DeleteAsync(attempt);
        return true;
    }

    /// <summary>
    /// Get statistics for a dragon's attempts
    /// </summary>
    public async Task<(int total, int failed, int active, int avgDays)> GetDragonStatsAsync(
        string username, string game, string dragonTitle)
    {
        var attempts = await GetAttemptsForDragonAsync(username, game, dragonTitle);
        
        int total = attempts.Count;
        int failed = attempts.Count(a => a.FailedAt.HasValue);
        int active = attempts.Count(a => a.IsActive);
        
        var completedAttempts = attempts.Where(a => a.StartedAt.HasValue).ToList();
        int avgDays = completedAttempts.Count > 0
            ? (int)completedAttempts.Average(a => a.DurationDays)
            : 0;

        return (total, failed, active, avgDays);
    }

    /// <summary>
    /// Format duration in a human-readable way
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 365)
        {
            int years = (int)(duration.TotalDays / 365);
            int months = (int)((duration.TotalDays % 365) / 30);
            return months > 0 ? $"{years}y {months}mo" : $"{years}y";
        }
        else if (duration.TotalDays >= 30)
        {
            int months = (int)(duration.TotalDays / 30);
            int days = (int)(duration.TotalDays % 30);
            return days > 0 ? $"{months}mo {days}d" : $"{months}mo";
        }
        else if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d";
        }
        else if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h";
        }
        else
        {
            return $"{(int)duration.TotalMinutes}m";
        }
    }
}
