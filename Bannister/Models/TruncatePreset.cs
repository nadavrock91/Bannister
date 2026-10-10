using SQLite;

namespace Bannister.Models;

[Table("truncate_presets")]
public class TruncatePreset
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public double ThresholdDb { get; set; }

    public double MinSilenceDuration { get; set; }

    public double TruncateTo { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
