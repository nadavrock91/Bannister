using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a user's game rule/clarification for the EXP system.
/// These are global rules that apply across all games.
/// </summary>
[Table("game_rules")]
public class GameRule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>
    /// The rule text
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Display order (lower = higher in list)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// When the rule was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the rule was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
