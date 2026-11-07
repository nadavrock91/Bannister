using SQLite;
namespace Bannister.Models;

[Table("favorite_source_folders")]
public class FavoriteSourceFolder
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Device { get; set; } = "";  // "PC", "Phone", or "" = all
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
