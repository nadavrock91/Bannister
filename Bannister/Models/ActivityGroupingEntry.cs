using SQLite;

namespace Bannister.Models;

/// <summary>
/// Junction table linking activities to groupings (many-to-many).
/// An activity can belong to multiple groupings.
/// </summary>
[Table("activity_grouping_entries")]
public class ActivityGroupingEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int GroupingId { get; set; }

    [Indexed]
    public int ActivityId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.Now;
}
