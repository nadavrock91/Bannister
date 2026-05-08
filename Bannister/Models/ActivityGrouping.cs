using SQLite;

namespace Bannister.Models;

/// <summary>
/// A custom grouping that can contain activities from any game.
/// E.g., "Morning Routine", "Most Important", "Daily Essentials"
/// </summary>
[Table("activity_groupings")]
public class ActivityGrouping
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Number of activities in this grouping (populated at query time, not stored)
    /// </summary>
    [Ignore]
    public int ActivityCount { get; set; } = 0;
}
