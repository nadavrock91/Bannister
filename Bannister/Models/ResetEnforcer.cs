using SQLite;

namespace Bannister.Models;

[Table("reset_enforcers")]
public class ResetEnforcer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string ImagePath { get; set; } = "";

    public int TotalResets { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
