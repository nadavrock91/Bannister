using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents an attempt to slay a dragon (reach level 100 in a game)
/// Users can have multiple attempts per dragon, tracking failures and successes
/// </summary>
[Table("dragon_attempts")]
public class Attempt
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Game { get; set; } = "";

    /// <summary>
    /// The dragon's title this attempt is for
    /// </summary>
    [Indexed]
    public string DragonTitle { get; set; } = "";

    /// <summary>
    /// Sequential attempt number (1, 2, 3, etc.)
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// When this attempt started (UTC)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When this attempt failed (UTC), null if still active or not started
    /// </summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>
    /// True if this is the currently active attempt for this dragon
    /// Only one attempt per dragon can be active at a time
    /// </summary>
    [Indexed]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Optional notes about this attempt
    /// </summary>
    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed properties (not stored in DB)

    /// <summary>
    /// Duration of this attempt in days
    /// </summary>
    [Ignore]
    public int DurationDays
    {
        get
        {
            if (!StartedAt.HasValue) return 0;
            var endTime = FailedAt ?? DateTime.UtcNow;
            return (int)(endTime - StartedAt.Value).TotalDays;
        }
    }

    /// <summary>
    /// Status of this attempt
    /// </summary>
    [Ignore]
    public string Status
    {
        get
        {
            if (IsActive && StartedAt.HasValue) return "Active";
            if (FailedAt.HasValue) return "Failed";
            if (StartedAt.HasValue) return "Completed";
            return "Not Started";
        }
    }

    /// <summary>
    /// User-friendly display of attempt duration
    /// </summary>
    [Ignore]
    public string DurationDisplay
    {
        get
        {
            if (!StartedAt.HasValue) return "Not started";
            
            var days = DurationDays;
            if (days == 0) return "Started today";
            if (days == 1) return "1 day";
            return $"{days} days";
        }
    }
}
