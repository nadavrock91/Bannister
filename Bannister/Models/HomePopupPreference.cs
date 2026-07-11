using SQLite;

namespace Bannister.Models;

[Table("home_popup_preferences")]
public class HomePopupPreference
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    // "streak_reset", "streak_escalation", "missed_fragments",
    // "quick_input", "days_since_escalation", "missed_activities"
    [Indexed]
    public string PopupKey { get; set; } = "";

    // "primary" or "secondary"
    [Indexed]
    public string DeviceRole { get; set; } = "primary";

    public bool Enabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
