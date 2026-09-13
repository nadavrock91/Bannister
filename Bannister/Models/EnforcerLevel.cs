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
    /// Positive = achievement level, Negative = failure/penalty level.
    /// </summary>
    public bool IsPositive { get; set; } = true;

    /// <summary>
    /// Display order within the enforcer's level list.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    // Auto level-up triggers — any set to -1 means not used
    /// <summary>
    /// Auto-advance to this level when DaysInARow reaches this value.
    /// -1 = not used.
    /// </summary>
    public int TriggerDays { get; set; } = -1;

    /// <summary>
    /// Auto-advance to this level when TotalResets reaches this value.
    /// -1 = not used.
    /// </summary>
    public int TriggerResets { get; set; } = -1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
