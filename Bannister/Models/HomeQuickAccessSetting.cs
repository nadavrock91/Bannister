using SQLite;

namespace Bannister.Models;

/// <summary>
/// Stores which HomePage navigation buttons are pinned to
/// the Quick Access section, per user. Syncs across devices.
/// </summary>
[Table("home_quick_access_settings")]
public class HomeQuickAccessSetting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>
    /// The button ID (e.g. "Games", "Tasks", "Calendar")
    /// </summary>
    [Indexed]
    public string ButtonId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
