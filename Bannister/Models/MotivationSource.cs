using SQLite;

namespace Bannister.Models;

[Table("motivation_sources")]
public class MotivationSource
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsArchived { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
