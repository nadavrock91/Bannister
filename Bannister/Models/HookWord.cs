using SQLite;

namespace Bannister.Models;

[Table("hook_word_pool")]
public class HookWord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    // Normalized to lowercase + trimmed for dedup consistency.
    [Indexed]
    public string Word { get; set; } = "";

    // "active" or "removed"
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
