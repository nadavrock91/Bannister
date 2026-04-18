using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a streak attempt for an activity (consecutive days of using the activity)
/// </summary>
[Table("streak_attempts")]
public class StreakAttempt
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Game { get; set; } = "";

    /// <summary>
    /// The activity ID this streak is for
    /// </summary>
    [Indexed]
    public int ActivityId { get; set; }

    /// <summary>
    /// The activity name (denormalized for display)
    /// </summary>
    public string ActivityName { get; set; } = "";

    /// <summary>
    /// Sequential attempt number (1, 2, 3, etc.)
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// When this streak started (UTC)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When this streak ended/broke (UTC), null if still active
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// Number of consecutive days achieved in this streak
    /// </summary>
    public int DaysAchieved { get; set; } = 0;

    /// <summary>
    /// True if this is the currently active streak for this activity
    /// </summary>
    [Indexed]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Last date the activity was used (for tracking consecutive days)
    /// </summary>
    public DateTime? LastUsedDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed properties

    [Ignore]
    public string Status
    {
        get
        {
            if (IsActive) return "🔥 Active";
            if (EndedAt.HasValue) return "💔 Broken";
            return "Not Started";
        }
    }

    [Ignore]
    public string DaysDisplay
    {
        get
        {
            if (DaysAchieved == 0) return "0 days";
            if (DaysAchieved == 1) return "1 day";
            return $"{DaysAchieved} days";
        }
    }

    [Ignore]
    public string DateRangeDisplay
    {
        get
        {
            if (!StartedAt.HasValue) return "";
            var start = StartedAt.Value.ToLocalTime().ToString("MMM d");
            if (EndedAt.HasValue)
            {
                var end = EndedAt.Value.ToLocalTime().ToString("MMM d");
                return $"{start} → {end}";
            }
            return $"{start} → ongoing";
        }
    }
}
