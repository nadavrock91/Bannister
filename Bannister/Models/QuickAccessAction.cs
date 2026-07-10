using SQLite;

namespace Bannister.Models;

[Table("quick_access_actions")]
public class QuickAccessAction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    // "open_video" for now; extensible later.
    public string ActionType { get; set; } = "open_video";

    public string FilePath { get; set; } = "";

    // For "prompt_wrap" actions.
    public string? PromptType { get; set; } // "prefix" or "suffix"

    public string? PromptText { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
