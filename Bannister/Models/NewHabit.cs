using SQLite;

namespace Bannister.Models;

[Table("new_habits")]
public class NewHabit
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Username { get; set; } = "";
    
    public string Game { get; set; } = "";
    
    // The positive activity (what you do to gain EXP)
    public int PositiveActivityId { get; set; }
    
    // The negative activity (penalty if you don't do it)
    public int NegativeActivityId { get; set; }
    
    // Name for display
    public string HabitName { get; set; } = "";
    
    // Current streak of consecutive days
    public int ConsecutiveDays { get; set; } = 0;
    
    // Last date the positive activity was applied
    public DateTime? LastAppliedDate { get; set; }
    
    // When this habit was added to the active list
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    // Status: "active", "graduated", "failed"
    public string Status { get; set; } = "active";
    
    // When it graduated or failed
    public DateTime? CompletedAt { get; set; }
    
    // Days required to graduate (default 7 for daily)
    public int DaysToGraduate { get; set; } = 7;
    
    [Ignore]
    public bool IsGraduated => Status == "graduated";
    
    [Ignore]
    public bool IsFailed => Status == "failed";
    
    [Ignore]
    public bool IsActive => Status == "active";
    
    [Ignore]
    public int DaysRemaining => Math.Max(0, DaysToGraduate - ConsecutiveDays);
    
    [Ignore]
    public int ProgressPercent => (int)(ConsecutiveDays * 100.0 / DaysToGraduate);
}

[Table("habit_allowances")]
public class HabitAllowance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string Username { get; set; } = "";
    
    // Current allowance (how many new habits can be active at once)
    // This is global per user, not per game
    public int CurrentAllowance { get; set; } = 1;
    
    // Total habits graduated (for stats)
    public int TotalGraduated { get; set; } = 0;
    
    // Total habits failed (for stats)
    public int TotalFailed { get; set; } = 0;
    
    // Highest allowance ever reached
    public int HighestAllowance { get; set; } = 1;
}
