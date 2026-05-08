using SQLite;

namespace Bannister.Models;

[Table("streak_target_stat_logs")]
public class StreakTargetStatLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public int TargetDays { get; set; }

    public int CountBefore { get; set; }

    public int CountAfter { get; set; }

    public string ChangeType { get; set; } = "completion";

    public string ActivityName { get; set; } = "";

    [Indexed]
    public int StreakAttemptId { get; set; }

    public string? Note { get; set; }

    [Indexed]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public int Change => CountAfter - CountBefore;

    [Ignore]
    public string DateDisplay => LoggedAt.ToLocalTime().ToString("MMM dd, yyyy");

    [Ignore]
    public string TimeDisplay => LoggedAt.ToLocalTime().ToString("h:mm tt");

    [Ignore]
    public string ChangeDisplay => Change > 0 ? $"+{Change}" : Change.ToString();

    [Ignore]
    public string ChangeTypeDisplay => ChangeType switch
    {
        "completion" => "Completed",
        "reset" => "Reset",
        "ended" => "Ended",
        "reactivated" => "Reactivated",
        "deleted" => "Deleted row",
        "manual_edit" => "Manual edit",
        _ => ChangeType
    };
}
