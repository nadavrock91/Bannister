using SQLite;

namespace Bannister.Models;

[Table("routines")]
public class Routine
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public int FrequencyDays { get; set; }

    public DateTime StartDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
