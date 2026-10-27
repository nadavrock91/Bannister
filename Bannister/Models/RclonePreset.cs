using SQLite;

namespace Bannister.Models;

[Table("rclone_presets")]
public class RclonePreset
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";
    public string Source { get; set; } = "";           // fixed value, or "" if SourceIsInput
    public string Destination { get; set; } = "";      // fixed value, or "" if DestinationIsInput
    public int Transfers { get; set; } = 8;
    public int Checkers { get; set; } = 16;
    public string RclonePath { get; set; } = "";
    public bool SourceIsInput { get; set; } = true;    // true = prompt user at run time
    public bool DestinationIsInput { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
