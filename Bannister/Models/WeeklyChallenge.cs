using SQLite;

namespace Bannister.Models;

[Table("weekly_challenges")]
public class WeeklyChallenge
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string Username { get; set; } = "";
    
    /// <summary>
    /// The focus category for this challenge
    /// </summary>
    public string FocusCategory { get; set; } = "";
    
    /// <summary>
    /// Total tasks to complete in focus category before challenge ends
    /// </summary>
    public int TargetTaskCount { get; set; } = 100;
    
    /// <summary>
    /// Tasks completed in focus category so far
    /// </summary>
    public int CompletedFocusTaskCount { get; set; } = 0;
    
    /// <summary>
    /// Current weekly allowance (tasks required per week)
    /// </summary>
    public int CurrentAllowance { get; set; } = 1;
    
    /// <summary>
    /// Consecutive successful weeks
    /// </summary>
    public int SuccessStreak { get; set; } = 0;
    
    /// <summary>
    /// When the challenge started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Is the challenge active?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When completed (if finished)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    [Ignore]
    public int RemainingFocusTasks => TargetTaskCount - CompletedFocusTaskCount;
    
    [Ignore]
    public int WeeksUntilAllowanceIncrease => 3 - (SuccessStreak % 3);
    
    /// <summary>
    /// How many of the allowance slots can be non-focus tasks
    /// Every 3rd slot can be non-focus
    /// </summary>
    [Ignore]
    public int NonFocusSlotsAllowed => CurrentAllowance / 3;
    
    [Ignore]
    public int FocusSlotsRequired => CurrentAllowance - NonFocusSlotsAllowed;
}

[Table("weekly_commitments")]
public class WeeklyCommitment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public int ChallengeId { get; set; }
    
    /// <summary>
    /// The task committed to
    /// </summary>
    public int TaskId { get; set; }
    
    /// <summary>
    /// Week start date (Sunday)
    /// </summary>
    public DateTime WeekStart { get; set; }
    
    /// <summary>
    /// Is this a focus category task or non-focus?
    /// </summary>
    public bool IsFocusTask { get; set; } = true;
    
    /// <summary>
    /// Was this commitment completed?
    /// </summary>
    public bool IsCompleted { get; set; } = false;
    
    /// <summary>
    /// When completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
