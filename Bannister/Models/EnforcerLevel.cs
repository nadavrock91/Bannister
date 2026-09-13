using SQLite;

namespace Bannister.Models;

[Table("enforcer_levels")]
public class EnforcerLevel
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ResetEnforcerId { get; set; }

    public string Name { get; set; } = "";

    public string ImagePath { get; set; } = "";

    /// <summary>
    /// Signed level number. Positive = achievement (+1, +2...),
    /// Negative = penalty (-1, -2...). 0 = neutral/base.
    /// </summary>
    public int LevelNumber { get; set; } = 1;

    /// <summary>
    /// Display order within the enforcer's level list.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    public int TriggerDays { get; set; } = -1;
    public int TriggerResets { get; set; } = -1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string LevelDisplay =>
        LevelNumber > 0 ? $"Level +{LevelNumber}" :
        LevelNumber < 0 ? $"Level {LevelNumber}" :
        "Level 0";

    [Ignore]
    public string LevelIcon =>
        LevelNumber > 0 ? "✅" :
        LevelNumber < 0 ? "⚠️" : "➖";
}
