using SQLite;

namespace Bannister.Models;

[Table("assets")]
public class AssetItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public double Units { get; set; }

    public double ValuePerUnit { get; set; }

    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
