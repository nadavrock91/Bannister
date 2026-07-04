using SQLite;

namespace Bannister.Models;

[Table("postponed_tasks")]
public class PostponedTask
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string Notes { get; set; } = "";

    // The date this task is currently scheduled to appear on.
    public DateTime CurrentDate { get; set; }

    // The date it was first created.
    public DateTime OriginalDate { get; set; }

    public int TimesPostponed { get; set; }

    // "active", "completed", "disabled"
    [Indexed]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? DisabledAt { get; set; }
}
