using SQLite;

namespace Bannister.Models;

[Table("opening_clip_image_session_items")]
public class OpeningClipImageSessionItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string ImagePath { get; set; } = "";
    public int SortOrder { get; set; }
    public int CopyCount { get; set; }
    public bool IsActive { get; set; }
    public string CopiedPromptKeys { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
