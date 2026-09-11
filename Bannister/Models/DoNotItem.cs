using SQLite;

namespace Bannister.Models;

/// <summary>
/// A "do not do this" exclusion item for LLM prompt generation.
/// Scoped per user and area so different pages can have their own lists.
/// </summary>
[Table("do_not_items")]
public class DoNotItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>
    /// Which feature this belongs to e.g. "OpeningClipPrompts"
    /// </summary>
    [Indexed]
    public string Area { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
