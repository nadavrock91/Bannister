using SQLite;

namespace Bannister.Models;

[Table("backup_locations")]
public class BackupLocation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";        // e.g. "Backblaze", "PC", "OneDrive"
    public string Abbreviation { get; set; } = ""; // e.g. "BB", "PC", "OD"
    public string RcloneRemote { get; set; } = ""; // e.g. "b3ai", "onedrive"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
