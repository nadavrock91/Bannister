using SQLite;
namespace Bannister.Models;

[Table("sort_folders")]
public class SortFolder
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";      // button label
    public string Path { get; set; } = "";      // destination folder path
    public int SortOrder { get; set; } = 0;
    public int? SetId { get; set; } = null;   // null = no set
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
