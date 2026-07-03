using SQLite;

namespace Bannister.Models;

[Table("asset_library_items")]
public class AssetLibraryItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string FilePath { get; set; } = "";

    public string FileName { get; set; } = "";

    public string FileType { get; set; } = "";

    public long FileSizeBytes { get; set; }

    public string Category { get; set; } = "";

    public string Categories { get; set; } = "";

    public string DescriptiveName { get; set; } = "";

    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

    public DateTime? MissingSince { get; set; }
}
