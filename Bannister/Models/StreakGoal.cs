using SQLite;

namespace Bannister.Models;

[Table("streak_goals")]
public class StreakGoal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ActivityId { get; set; }

    public int TargetDays { get; set; }

    public DateTime SetDate { get; set; } = DateTime.UtcNow;

    public DateTime? AchievedDate { get; set; }
}
