using SQLite;

namespace Bannister.Models;

/// <summary>
/// Replaces Preferences for daily check tracking.
/// Stores key-value pairs of "last checked date" in SQLite
/// so they can be viewed and edited via the Databases page or Run SQL.
/// </summary>
[Table("daily_checks")]
public class DailyCheck
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// The check key (e.g., "StaleActivitiesCheck_nadav_test")
    /// </summary>
    [Indexed]
    public string Key { get; set; } = "";

    /// <summary>
    /// The last date this check ran (yyyy-MM-dd format)
    /// </summary>
    public string LastCheckedDate { get; set; } = "";
}
