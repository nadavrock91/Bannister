using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a log entry for a streak change (increment, decrement, manual edit)
/// Used to track the history of a streak's days count over time
/// </summary>
[Table("streak_logs")]
public class StreakLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// FK to the streak attempt this log belongs to
    /// </summary>
    [Indexed]
    public int StreakAttemptId { get; set; }

    /// <summary>
    /// Days count before this change
    /// </summary>
    public int DaysBefore { get; set; }

    /// <summary>
    /// Days count after this change
    /// </summary>
    public int DaysAfter { get; set; }

    /// <summary>
    /// The change amount (positive for increment, negative for decrement)
    /// </summary>
    public int Change => DaysAfter - DaysBefore;

    /// <summary>
    /// Type of change: "increment", "auto_increment", "manual_edit", "reset", "created"
    /// </summary>
    public string ChangeType { get; set; } = "increment";

    /// <summary>
    /// Optional note about the change
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// When this change occurred
    /// </summary>
    [Indexed]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    // Computed properties for display

    [Ignore]
    public string ChangeDisplay
    {
        get
        {
            if (ChangeType == "created")
                return $"Started at {DaysAfter} days";
            
            if (Change > 0)
                return $"+{Change}";
            else if (Change < 0)
                return Change.ToString();
            else
                return "0";
        }
    }

    [Ignore]
    public string DateDisplay => LoggedAt.ToLocalTime().ToString("MMM dd, yyyy");

    [Ignore]
    public string TimeDisplay => LoggedAt.ToLocalTime().ToString("h:mm tt");

    [Ignore]
    public string ChangeTypeDisplay => ChangeType switch
    {
        "increment" => "Daily use",
        "auto_increment" => "Auto-increment",
        "manual_edit" => "Manual edit",
        "reset" => "Reset",
        "created" => "Started",
        _ => ChangeType
    };
}
