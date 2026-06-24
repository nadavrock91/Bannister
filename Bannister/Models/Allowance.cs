using SQLite;

namespace Bannister.Models;

[Table("allowances")]
public class Allowance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public int Total { get; set; } = 1;

    public int Current { get; set; }

    public int SuccessStreak { get; set; }

    public string RecentHistory { get; set; } = "";

    public DateTime? LastOutcomeDate { get; set; }

    public int CapFloor { get; set; } = 1;

    public bool PromptDailyOnHome { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
