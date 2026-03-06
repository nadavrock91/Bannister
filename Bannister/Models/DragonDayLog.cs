using SQLite;

namespace Bannister.Models;

/// <summary>
/// Tracks daily logs for dragon attempts (similar to streak logs)
/// Records each day of battling with optional notes
/// </summary>
[Table("dragon_day_logs")]
public class DragonDayLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Game { get; set; } = "";

    [Indexed]
    public string DragonTitle { get; set; } = "";

    /// <summary>
    /// The attempt number this log belongs to
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// The date of this log entry (date only, no time)
    /// </summary>
    [Indexed]
    public DateTime LogDate { get; set; }

    /// <summary>
    /// Running total of days at the time of this log
    /// </summary>
    public int DayNumber { get; set; }

    /// <summary>
    /// How this day was recorded: "auto" or "manual"
    /// </summary>
    public string Source { get; set; } = "manual";

    /// <summary>
    /// Optional description/note for this day
    /// </summary>
    public string Description { get; set; } = "Daily use";

    /// <summary>
    /// When this log was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
