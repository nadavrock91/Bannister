using SQLite;

namespace Bannister.Models;

[Table("pending_activity_ideas")]
public class PendingActivityIdea
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Game { get; set; } = "";

    public string ActivityName { get; set; } = "";

    public string ActivityCategory { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AppliedAt { get; set; }

    public int Status { get; set; } = 0;
}
