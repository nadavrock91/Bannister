using SQLite;

namespace Bannister.Models;

public class FocusBulletPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Text { get; set; } = "";
    public int SortOrder { get; set; } = 0;
    public string Status { get; set; } = "active"; // active, archived
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}
