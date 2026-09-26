using SQLite;

namespace Bannister.Models;

[Table("exp_state")]
public class ExpState
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Game { get; set; } = "";

    public int TotalExp { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("exp_log")]
public class ExpLog
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
    public int DeltaExp { get; set; }
    public int TotalExp { get; set; }
    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }

    [Indexed]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
