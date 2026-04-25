using SQLite;

namespace Bannister.Models;

/// <summary>
/// Persists IdeaLogger settings like favorite categories and linked categories.
/// Simple key-value store: Key = setting name, Value = JSON or comma-separated data.
/// </summary>
[Table("idea_logger_settings")]
public class IdeaLoggerSetting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Username who owns this setting</summary>
    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>Setting key, e.g. "favorites", "link:diary"</summary>
    [Indexed]
    public string Key { get; set; } = "";

    /// <summary>Setting value, e.g. comma-separated category names</summary>
    public string Value { get; set; } = "";
}
