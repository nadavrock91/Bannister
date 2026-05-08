using SQLite;

namespace Bannister.Models;

[Table("streak_target_completions")]
public class StreakTargetCompletion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Game { get; set; } = "";

    [Indexed]
    public int ActivityId { get; set; }

    public string ActivityName { get; set; } = "";

    [Indexed]
    public int StreakAttemptId { get; set; }

    [Indexed]
    public int TargetDays { get; set; }

    [Indexed]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string CompletedAtDisplay => CompletedAt.ToLocalTime().ToString("MMM dd, yyyy h:mm tt");
}
