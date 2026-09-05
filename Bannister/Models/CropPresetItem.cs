using SQLite;

namespace Bannister.Models;

[Table("crop_presets")]
public class CropPresetItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public int W { get; set; }

    public int H { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
