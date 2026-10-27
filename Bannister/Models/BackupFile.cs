using SQLite;

namespace Bannister.Models;

[Table("backup_files")]
public class BackupFile
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";         // e.g. "Brand Kit v3.psd"
    public string Category { get; set; } = "";     // e.g. "Design"
    // Comma-separated list of BackupLocation.Id values where this file IS stored
    public string LocationIds { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
