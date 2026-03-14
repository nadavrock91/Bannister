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
    
    // Current streak of consecutive days/weeks/months
    public int ConsecutiveDays { get; set; } = 0;
    
    // Last date the positive activity was applied
    public DateTime? LastAppliedDate { get; set; }
    
    // When this habit was added to the active list
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    // Status: "active", "pending", "graduated", "failed"
    public string Status { get; set; } = "active";
    
    // When it graduated or failed
    public DateTime? CompletedAt { get; set; }
    
    // Days required to graduate (7 for daily, 4 for weekly, 3 for monthly)
    public int DaysToGraduate { get; set; } = 7;
    
    // Frequency: "Daily", "Weekly", "Monthly"
    public string Frequency { get; set; } = "Daily";
    
    // Order in pending queue (for pending habits)
    public int PendingOrder { get; set; } = 0;
    
    [Ignore]
    public bool IsGraduated => Status == "graduated";
    
    [Ignore]
    public bool IsFailed => Status == "failed";
    
    [Ignore]
    public bool IsActive => Status == "active";
    
    [Ignore]
    public bool IsPending => Status == "pending";
    
    [Ignore]
    public int DaysRemaining => Math.Max(0, DaysToGraduate - ConsecutiveDays);
    
    [Ignore]
    public int ProgressPercent => (int)(ConsecutiveDays * 100.0 / DaysToGraduate);
    
    [Ignore]
    public string ProgressLabel
    {
        get
        {
            string unit = Frequency switch
            {
                "Daily" => ConsecutiveDays == 1 ? "day" : "days",
                "Weekly" => ConsecutiveDays == 1 ? "week" : "weeks",
                "Monthly" => ConsecutiveDays == 1 ? "month" : "months",
                _ => "days"
            };
            return $"{ConsecutiveDays}/{DaysToGraduate} {unit}";
        }
    }
}

[Table("habit_allowances")]
public class HabitAllowance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string Username { get; set; } = "";
    
    // Frequency this allowance applies to: "Daily", "Weekly", "Monthly"
    public string Frequency { get; set; } = "Daily";
    
    // Current allowance (how many habits of this frequency can be active at once)
    public int CurrentAllowance { get; set; } = 1;
    
    // Total habits graduated (for stats)
    public int TotalGraduated { get; set; } = 0;
    
    // Total habits failed (for stats)
    public int TotalFailed { get; set; } = 0;
    
    // Highest allowance ever reached
    public int HighestAllowance { get; set; } = 1;
}
