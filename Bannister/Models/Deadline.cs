using SQLite;

namespace Bannister.Models;

[Table("deadlines")]
public class Deadline
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public int Bucket { get; set; }

    public int State { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime StateChangedAt { get; set; } = DateTime.UtcNow;
}
