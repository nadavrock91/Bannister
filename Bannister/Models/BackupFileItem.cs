using SQLite;

namespace Bannister.Models;

[Table("backup_file_items")]
public class BackupFileItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ParentFileId { get; set; }

    public int? ParentItemId { get; set; } = null;

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";
    public string LocationIds { get; set; } = ""; // comma-separated BackupLocation.Id values
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
