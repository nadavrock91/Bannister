using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a process broken down into sequential steps.
/// </summary>
[Table("sub_activities")]
public class SubActivity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Username { get; set; } = "";
    
    /// <summary>
    /// Name of the process (e.g., "Morning Routine", "Video Production")
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Optional description
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// JSON array of active steps: [{"name":"Step 1","done":false},...]
    /// </summary>
    public string StepsJson { get; set; } = "[]";
    
    /// <summary>
    /// JSON array of pending/possible steps not yet active
    /// </summary>
    public string PendingStepsJson { get; set; } = "[]";
    
    /// <summary>
    /// "daily" = auto-reset on first load each day, "manual" = user clicks reset
    /// </summary>
    public string ResetMode { get; set; } = "manual";
    
    /// <summary>
    /// "unlimited" = can add steps anytime, "locked" = must complete N executions first
    /// </summary>
    public string AdditionMode { get; set; } = "unlimited";
    
    /// <summary>
    /// If AdditionMode is "locked", how many completions needed before adding new steps
    /// </summary>
    public int RequiredCompletionsToUnlock { get; set; } = 3;
    
    /// <summary>
    /// How many times since last step addition (resets when step added)
    /// </summary>
    public int CompletionsSinceLastAddition { get; set; } = 0;
    
    /// <summary>
    /// Total lifetime completions
    /// </summary>
    public int TotalCompletions { get; set; } = 0;
    
    /// <summary>
    /// Last date steps were reset (for daily mode)
    /// </summary>
    public DateTime? LastResetDate { get; set; }
    
    public bool IsArchived { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a single step within a SubActivity
/// </summary>
public class SubActivityStep
{
    public string Name { get; set; } = "";
    public bool Done { get; set; } = false;
}
