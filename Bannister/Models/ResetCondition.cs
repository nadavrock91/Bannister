using SQLite;

namespace Bannister.Models;

[Table("reset_conditions")]
public class ResetCondition
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ResetEnforcerId { get; set; }

    public string Text { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
