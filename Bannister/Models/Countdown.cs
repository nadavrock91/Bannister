using SQLite;

namespace Bannister.Models;

public class Countdown
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Username { get; set; } = "";
    
    /// <summary>
    /// Name of the countdown (e.g., "Days till AGI", "Tesla hits $500")
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Category for grouping (e.g., "AI", "Stocks", "Personal", "World Events")
    /// </summary>
    public string Category { get; set; } = "General";
    
    /// <summary>
    /// Optional description or notes about the prediction
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// The target date the user predicts the event will happen (for auto countdowns)
    /// </summary>
    public DateTime TargetDate { get; set; }
    
    /// <summary>
    /// When this countdown was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// How many times the user has postponed their estimate
    /// </summary>
    public int PostponeCount { get; set; } = 0;
    
    /// <summary>
    /// Total days postponed (sum of all postponements)
    /// </summary>
    public int TotalDaysPostponed { get; set; } = 0;
    
    /// <summary>
    /// Status: "Active", "Correct", "Wrong", "Postponed", "Cancelled"
    /// </summary>
    public string Status { get; set; } = "Active";
    
    /// <summary>
    /// When the countdown was resolved (marked correct/wrong)
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
    
    /// <summary>
    /// Optional notes when resolving (e.g., "Actually happened on X date")
    /// </summary>
    public string ResolutionNotes { get; set; } = "";
    
    /// <summary>
    /// The original target date (before any postponements)
    /// </summary>
    public DateTime OriginalTargetDate { get; set; }
    
    /// <summary>
    /// If true, countdown is manual (doesn't auto-decrement based on date)
    /// </summary>
    public bool IsManual { get; set; } = false;
    
    /// <summary>
    /// Current manual count value (only used when IsManual = true)
    /// </summary>
    public int ManualCount { get; set; } = 0;
    
    /// <summary>
    /// Original manual count when created (for history/tracking)
    /// </summary>
    public int OriginalManualCount { get; set; } = 0;
    
    /// <summary>
    /// Subcategory within Overdue (e.g., "General", "Technical", "Personal")
    /// Only used when Status = "Overdue"
    /// </summary>
    public string OverdueCategory { get; set; } = "General";
    
    // Computed properties
    [Ignore]
    public int DaysRemaining => IsManual ? ManualCount : (int)(TargetDate.Date - DateTime.Now.Date).TotalDays;
    
    [Ignore]
    public bool IsExpired => DaysRemaining <= 0;
    
    [Ignore]
    public bool NeedsResolution => IsExpired && Status == "Active";
    
    [Ignore]
    public string DaysRemainingDisplay
    {
        get
        {
            int days = DaysRemaining;
            if (days > 0) return $"{days} day{(days == 1 ? "" : "s")}";
            if (days == 0) return IsManual ? "0" : "Today!";
            return IsManual ? $"{days}" : $"{Math.Abs(days)} day{(Math.Abs(days) == 1 ? "" : "s")} overdue";
        }
    }
    
    [Ignore]
    public Color StatusColor => Status switch
    {
        "Active" => IsExpired ? Color.FromArgb("#FF9800") : Color.FromArgb("#4CAF50"),
        "Overdue" => Color.FromArgb("#F44336"),
        "Correct" => Color.FromArgb("#4CAF50"),
        "Wrong" => Color.FromArgb("#F44336"),
        "Postponed" => Color.FromArgb("#FF9800"),
        "Cancelled" => Color.FromArgb("#9E9E9E"),
        _ => Color.FromArgb("#666666")
    };
    
    [Ignore]
    public string StatusEmoji => Status switch
    {
        "Active" => IsManual ? "🔢" : (IsExpired ? "⏰" : "⏳"),
        "Overdue" => "📛",
        "Correct" => "✅",
        "Wrong" => "❌",
        "Postponed" => "⏸️",
        "Cancelled" => "🚫",
        _ => "❓"
    };
}

/// <summary>
/// History entry for countdown value changes
/// </summary>
public class CountdownHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public int CountdownId { get; set; }
    
    /// <summary>
    /// The value before the change
    /// </summary>
    public int OldValue { get; set; }
    
    /// <summary>
    /// The value after the change
    /// </summary>
    public int NewValue { get; set; }
    
    /// <summary>
    /// Type of change: "Decrement", "Increment", "Edit", "Created"
    /// </summary>
    public string ChangeType { get; set; } = "";
    
    /// <summary>
    /// Optional note about the change
    /// </summary>
    public string Note { get; set; } = "";
    
    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.Now;
}
