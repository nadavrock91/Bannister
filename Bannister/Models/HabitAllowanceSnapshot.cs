using SQLite;

namespace Bannister.Models;

[Table("habit_allowance_snapshots")]
public class HabitAllowanceSnapshot
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Frequency { get; set; } = "";

    public DateTime SnapshotAt { get; set; }

    public string PeriodKey { get; set; } = "";

    public int Allowance { get; set; }
}
