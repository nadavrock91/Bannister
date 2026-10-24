using SQLite;

namespace Bannister.Models;

[Table("challenge_week_snapshots")]
public class ChallengeWeekSnapshot
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public int ChallengeId { get; set; }
    public DateTime WeekStart { get; set; }
    public int Allowance { get; set; }
    public int SuccessStreak { get; set; }
    public bool WasSuccessful { get; set; }
    public int CompletedFocus { get; set; }
    public int FocusTarget { get; set; }
    public int CompletedFree { get; set; }
    public int FreeTarget { get; set; }
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
}
