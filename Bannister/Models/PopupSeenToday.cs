using SQLite;

namespace Bannister.Models;

/// <summary>
/// Tracks which popups have been shown today
/// per user. Stored in SQLite so it syncs
/// across devices via the main database sync.
/// </summary>
[Table("popup_seen_today")]
public class PopupSeenToday
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>
    /// The popup key e.g. "pending_prompts",
    /// "life_path_checkin_42"
    /// </summary>
    [Indexed]
    public string PopupKey { get; set; } = "";

    /// <summary>
    /// Date this popup was last shown,
    /// stored as yyyy-MM-dd string for
    /// easy date comparison.
    /// </summary>
    public string SeenDate { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
