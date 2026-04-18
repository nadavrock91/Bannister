using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class CountdownService
{
    private readonly DatabaseService _db;
    
    public CountdownService(DatabaseService db)
    {
        _db = db;
        InitializeAsync().ConfigureAwait(false);
    }
    
    private async Task InitializeAsync()
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<Countdown>();
        await conn.CreateTableAsync<CountdownHistory>();
    }
    
    public async Task<List<Countdown>> GetCountdownsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Countdown>()
            .Where(c => c.Username == username)
            .OrderBy(c => c.TargetDate)
            .ToListAsync();
    }
    
    public async Task<List<Countdown>> GetCountdownsByCategoryAsync(string username, string category)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Countdown>()
            .Where(c => c.Username == username && c.Category == category)
            .OrderBy(c => c.TargetDate)
            .ToListAsync();
    }
    
    public async Task<List<Countdown>> GetActiveCountdownsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Countdown>()
            .Where(c => c.Username == username && c.Status == "Active")
            .OrderBy(c => c.TargetDate)
            .ToListAsync();
    }
    
    public async Task<List<Countdown>> GetExpiredCountdownsAsync(string username)
    {
        var all = await GetActiveCountdownsAsync(username);
        return all.Where(c => c.NeedsResolution).ToList();
    }
    
    public async Task<List<string>> GetCategoriesAsync(string username)
    {
        var countdowns = await GetCountdownsAsync(username);
        return countdowns
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }
    
    public async Task<Countdown> CreateCountdownAsync(string username, string name, string category, DateTime targetDate, string description = "", bool isManual = false, int manualCount = 0)
    {
        var countdown = new Countdown
        {
            Username = username,
            Name = name,
            Category = category,
            Description = description,
            TargetDate = targetDate,
            OriginalTargetDate = targetDate,
            CreatedAt = DateTime.Now,
            Status = "Active",
            IsManual = isManual,
            ManualCount = manualCount,
            OriginalManualCount = manualCount
        };
        
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(countdown);
        
        // Record creation in history
        if (isManual)
        {
            await AddHistoryAsync(countdown.Id, 0, manualCount, "Created", "Initial value");
        }
        
        return countdown;
    }
    
    public async Task UpdateCountdownAsync(Countdown countdown)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(countdown);
    }
    
    public async Task DeleteCountdownAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Countdown>(id);
        // Also delete history
        await conn.ExecuteAsync("DELETE FROM CountdownHistory WHERE CountdownId = ?", id);
    }
    
    public async Task PostponeCountdownAsync(Countdown countdown, int additionalDays)
    {
        countdown.PostponeCount++;
        countdown.TotalDaysPostponed += additionalDays;
        countdown.TargetDate = countdown.TargetDate.AddDays(additionalDays);
        countdown.Status = "Active"; // Reset to active after postponing
        
        await UpdateCountdownAsync(countdown);
    }
    
    public async Task ResolveCountdownAsync(Countdown countdown, bool wasCorrect, string notes = "")
    {
        countdown.Status = wasCorrect ? "Correct" : "Wrong";
        countdown.ResolvedAt = DateTime.Now;
        countdown.ResolutionNotes = notes;
        
        await UpdateCountdownAsync(countdown);
    }
    
    public async Task CancelCountdownAsync(Countdown countdown)
    {
        countdown.Status = "Cancelled";
        countdown.ResolvedAt = DateTime.Now;
        
        await UpdateCountdownAsync(countdown);
    }
    
    // Manual countdown operations
    public async Task DecrementManualCountAsync(Countdown countdown, string note = "")
    {
        if (!countdown.IsManual) return;
        
        int oldValue = countdown.ManualCount;
        countdown.ManualCount--;
        
        await UpdateCountdownAsync(countdown);
        await AddHistoryAsync(countdown.Id, oldValue, countdown.ManualCount, "Decrement", note);
    }
    
    public async Task IncrementManualCountAsync(Countdown countdown, string note = "")
    {
        if (!countdown.IsManual) return;
        
        int oldValue = countdown.ManualCount;
        countdown.ManualCount++;
        
        await UpdateCountdownAsync(countdown);
        await AddHistoryAsync(countdown.Id, oldValue, countdown.ManualCount, "Increment", note);
    }
    
    public async Task SetManualCountAsync(Countdown countdown, int newValue, string note = "")
    {
        if (!countdown.IsManual) return;
        
        int oldValue = countdown.ManualCount;
        countdown.ManualCount = newValue;
        
        await UpdateCountdownAsync(countdown);
        await AddHistoryAsync(countdown.Id, oldValue, newValue, "Edit", note);
    }
    
    // History operations
    public async Task AddHistoryAsync(int countdownId, int oldValue, int newValue, string changeType, string note = "")
    {
        var history = new CountdownHistory
        {
            CountdownId = countdownId,
            OldValue = oldValue,
            NewValue = newValue,
            ChangeType = changeType,
            Note = note,
            ChangedAt = DateTime.Now
        };
        
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(history);
    }
    
    public async Task<List<CountdownHistory>> GetHistoryAsync(int countdownId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CountdownHistory>()
            .Where(h => h.CountdownId == countdownId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }
    
    // Statistics
    public async Task<(int total, int correct, int wrong, int active)> GetStatsAsync(string username)
    {
        var all = await GetCountdownsAsync(username);
        return (
            total: all.Count,
            correct: all.Count(c => c.Status == "Correct"),
            wrong: all.Count(c => c.Status == "Wrong"),
            active: all.Count(c => c.Status == "Active")
        );
    }
    
    public async Task<double> GetAccuracyAsync(string username)
    {
        var all = await GetCountdownsAsync(username);
        var resolved = all.Where(c => c.Status == "Correct" || c.Status == "Wrong").ToList();
        
        if (resolved.Count == 0) return 0;
        
        return (double)resolved.Count(c => c.Status == "Correct") / resolved.Count * 100;
    }
    
    // Overdue methods
    public async Task<List<Countdown>> GetOverdueCountdownsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Countdown>()
            .Where(c => c.Username == username && c.Status == "Overdue")
            .OrderBy(c => c.ManualCount)
            .ToListAsync();
    }
    
    public async Task<List<Countdown>> GetOverdueBySubcategoryAsync(string username, string overdueCategory)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Countdown>()
            .Where(c => c.Username == username && c.Status == "Overdue" && c.OverdueCategory == overdueCategory)
            .OrderBy(c => c.ManualCount)
            .ToListAsync();
    }
    
    public async Task<List<string>> GetOverdueCategoriesAsync(string username)
    {
        var overdue = await GetOverdueCountdownsAsync(username);
        var categories = overdue
            .Select(c => c.OverdueCategory ?? "General")
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        
        // Always include "General" 
        if (!categories.Contains("General"))
        {
            categories.Insert(0, "General");
        }
        
        return categories;
    }
    
    public async Task MoveToOverdueAsync(Countdown countdown, int overdueDays, string overdueCategory = "General")
    {
        int negativeValue = -Math.Abs(overdueDays);
        int oldValue = countdown.IsManual ? countdown.ManualCount : countdown.DaysRemaining;
        
        countdown.IsManual = true;
        countdown.ManualCount = negativeValue;
        countdown.OriginalManualCount = oldValue;
        countdown.Status = "Overdue";
        countdown.OverdueCategory = overdueCategory;
        
        await UpdateCountdownAsync(countdown);
        await AddHistoryAsync(countdown.Id, oldValue, negativeValue, "Overdue", $"Moved to overdue ({overdueCategory}) at {negativeValue}");
    }
}
