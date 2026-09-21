using SQLite;

namespace Bannister.Models;

[Table("reset_time_settings")]
public class ResetTimeSetting
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Unique] public string Username { get; set; } = "";
    public int ResetHour { get; set; }
}

[Table("daily_deadline_items")]
public class DailyDeadlineItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Username { get; set; } = "";
    public int ActivityId { get; set; }
    public string GameId { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

[Table("daily_deadline_logs")]
public class DailyDeadlineLog
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Username { get; set; } = "";
    public string LogDate { get; set; } = "";
    public bool AllCompleted { get; set; }
    public string CompletedItemIds { get; set; } = "";
}

[Table("daily_deadline_states")]
public class DailyDeadlineState
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Unique] public string Username { get; set; } = "";
    public int Allowance { get; set; } = 1;
    public int ConsecutiveStreak { get; set; }
    public string LastResetKey { get; set; } = "";
}
