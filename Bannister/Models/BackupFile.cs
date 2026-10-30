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
    public string Path { get; set; } = "";
    public string Category { get; set; } = "";     // e.g. "Design"
    // Comma-separated list of BackupLocation.Id values where this file IS stored
    public string LocationIds { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Visibility { get; set; } = 2; // 0=private, 1=public, 2=both
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
