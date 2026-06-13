using SQLite;

namespace Bannister.Models;

[Table("custom_games")]
public class CustomGame
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public int EndType { get; set; }

    public int? EndValueSeconds { get; set; }

    public DateTime? EndValueDate { get; set; }

    public int? EndValueAmount { get; set; }

    public bool HigherIsBetter { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
