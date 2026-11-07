using SQLite;
namespace Bannister.Models;

[Table("sort_sets")]
public class SortSet
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
