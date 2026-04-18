using Bannister.Models;

namespace Bannister.Services;

public class WeeklyChallengeService
{
    private readonly DatabaseService _db;
    private readonly TaskService _tasks;
    private bool _initialized = false;

    public WeeklyChallengeService(DatabaseService db, TaskService tasks)
    {
        _db = db;
        _tasks = tasks;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<WeeklyChallenge>();
        await conn.CreateTableAsync<WeeklyCommitment>();
        _initialized = true;
    }

    /// <summary>
    /// Get the current Sunday of the week
    /// </summary>
    public static DateTime GetWeekStart(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>
    /// Get the active challenge for a user
    /// </summary>
    public async Task<WeeklyChallenge?> GetActiveChallengeAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WeeklyChallenge>()
            .Where(c => c.Username == username && c.IsActive)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Start a new challenge
    /// </summary>
    public async Task<WeeklyChallenge> StartChallengeAsync(string username, string focusCategory, int targetTasks)
    {
        await EnsureInitializedAsync();
        
        // End any existing active challenge
        var existing = await GetActiveChallengeAsync(username);
        if (existing != null)
        {
            existing.IsActive = false;
            var conn1 = await _db.GetConnectionAsync();
            await conn1.UpdateAsync(existing);
        }

        var challenge = new WeeklyChallenge
        {
            Username = username,
            FocusCategory = focusCategory,
            TargetTaskCount = targetTasks,
            CurrentAllowance = 1,
            SuccessStreak = 0,
            StartedAt = DateTime.UtcNow,
            IsActive = true
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(challenge);
        return challenge;
    }

    /// <summary>
    /// Get commitments for the current week
    /// </summary>
    public async Task<List<WeeklyCommitment>> GetCurrentWeekCommitmentsAsync(int challengeId)
    {
        await EnsureInitializedAsync();
        var weekStart = GetWeekStart(DateTime.Today);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WeeklyCommitment>()
            .Where(c => c.ChallengeId == challengeId && c.WeekStart == weekStart)
            .ToListAsync();
    }

    /// <summary>
    /// Add a task commitment for this week
    /// </summary>
    public async Task<WeeklyCommitment?> AddCommitmentAsync(int challengeId, int taskId, bool isFocusTask)
    {
        await EnsureInitializedAsync();
        var weekStart = GetWeekStart(DateTime.Today);
        
        // Check if already committed
        var conn = await _db.GetConnectionAsync();
        var existing = await conn.Table<WeeklyCommitment>()
            .Where(c => c.ChallengeId == challengeId && c.TaskId == taskId && c.WeekStart == weekStart)
            .FirstOrDefaultAsync();
        
        if (existing != null) return null;

        var commitment = new WeeklyCommitment
        {
            ChallengeId = challengeId,
            TaskId = taskId,
            WeekStart = weekStart,
            IsFocusTask = isFocusTask,
            IsCompleted = false
        };

        await conn.InsertAsync(commitment);
        return commitment;
    }

    /// <summary>
    /// Remove a commitment
    /// </summary>
    public async Task RemoveCommitmentAsync(int commitmentId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<WeeklyCommitment>(commitmentId);
    }

    /// <summary>
    /// Mark a commitment as completed (called when task is completed)
    /// </summary>
    public async Task MarkCommitmentCompletedAsync(int taskId)
    {
        await EnsureInitializedAsync();
        var weekStart = GetWeekStart(DateTime.Today);
        var conn = await _db.GetConnectionAsync();
        
        var commitment = await conn.Table<WeeklyCommitment>()
            .Where(c => c.TaskId == taskId && c.WeekStart == weekStart && !c.IsCompleted)
            .FirstOrDefaultAsync();

        if (commitment != null)
        {
            commitment.IsCompleted = true;
            commitment.CompletedAt = DateTime.UtcNow;
            await conn.UpdateAsync(commitment);
            
            // Update challenge focus task count if it's a focus task
            if (commitment.IsFocusTask)
            {
                var challenge = await conn.Table<WeeklyChallenge>()
                    .Where(c => c.Id == commitment.ChallengeId)
                    .FirstOrDefaultAsync();
                
                if (challenge != null)
                {
                    challenge.CompletedFocusTaskCount++;
                    
                    // Check if challenge is complete
                    if (challenge.CompletedFocusTaskCount >= challenge.TargetTaskCount)
                    {
                        challenge.IsActive = false;
                        challenge.CompletedAt = DateTime.UtcNow;
                    }
                    
                    await conn.UpdateAsync(challenge);
                }
            }
        }
    }

    /// <summary>
    /// Process end of week - call this on Sunday or when checking
    /// </summary>
    public async Task ProcessWeekEndAsync(string username)
    {
        await EnsureInitializedAsync();
        var challenge = await GetActiveChallengeAsync(username);
        if (challenge == null) return;

        var lastWeekStart = GetWeekStart(DateTime.Today.AddDays(-7));
        var conn = await _db.GetConnectionAsync();
        
        var lastWeekCommitments = await conn.Table<WeeklyCommitment>()
            .Where(c => c.ChallengeId == challenge.Id && c.WeekStart == lastWeekStart)
            .ToListAsync();

        // Check if last week had commitments
        if (lastWeekCommitments.Count == 0)
        {
            // No commitments picked = fail
            challenge.SuccessStreak = 0;
            challenge.CurrentAllowance = Math.Max(1, challenge.CurrentAllowance - 1);
        }
        else
        {
            int completed = lastWeekCommitments.Count(c => c.IsCompleted);
            
            if (completed >= challenge.CurrentAllowance)
            {
                // Success!
                challenge.SuccessStreak++;
                
                // Every 3 successful weeks, increase allowance
                if (challenge.SuccessStreak > 0 && challenge.SuccessStreak % 3 == 0)
                {
                    challenge.CurrentAllowance++;
                }
            }
            else
            {
                // Failed
                challenge.SuccessStreak = 0;
                challenge.CurrentAllowance = Math.Max(1, challenge.CurrentAllowance - 1);
            }
        }

        await conn.UpdateAsync(challenge);
    }

    /// <summary>
    /// Get tasks available for commitment (from focus category, not already committed this week)
    /// </summary>
    public async Task<List<TaskItem>> GetAvailableFocusTasksAsync(string username, string focusCategory)
    {
        var allTasks = await _tasks.GetTasksByCategoryAsync(username, focusCategory);
        var challenge = await GetActiveChallengeAsync(username);
        
        if (challenge == null) return allTasks;
        
        var currentCommitments = await GetCurrentWeekCommitmentsAsync(challenge.Id);
        var committedTaskIds = currentCommitments.Select(c => c.TaskId).ToHashSet();
        
        return allTasks.Where(t => !committedTaskIds.Contains(t.Id)).ToList();
    }

    /// <summary>
    /// Get tasks available for non-focus commitment
    /// </summary>
    public async Task<List<TaskItem>> GetAvailableNonFocusTasksAsync(string username, string focusCategory)
    {
        var allTasks = await _tasks.GetActiveTasksAsync(username);
        var challenge = await GetActiveChallengeAsync(username);
        
        // Filter out focus category tasks
        var nonFocusTasks = allTasks.Where(t => t.Category != focusCategory).ToList();
        
        if (challenge == null) return nonFocusTasks;
        
        var currentCommitments = await GetCurrentWeekCommitmentsAsync(challenge.Id);
        var committedTaskIds = currentCommitments.Select(c => c.TaskId).ToHashSet();
        
        return nonFocusTasks.Where(t => !committedTaskIds.Contains(t.Id)).ToList();
    }

    /// <summary>
    /// End the current challenge
    /// </summary>
    public async Task EndChallengeAsync(string username)
    {
        var challenge = await GetActiveChallengeAsync(username);
        if (challenge == null) return;

        challenge.IsActive = false;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(challenge);
    }

    /// <summary>
    /// Get completed challenges
    /// </summary>
    public async Task<List<WeeklyChallenge>> GetCompletedChallengesAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WeeklyChallenge>()
            .Where(c => c.Username == username && !c.IsActive)
            .OrderByDescending(c => c.CompletedAt)
            .ToListAsync();
    }
}
