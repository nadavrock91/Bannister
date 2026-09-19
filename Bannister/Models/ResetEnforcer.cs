using SQLite;

namespace Bannister.Models;

[Table("reset_enforcers")]
public class ResetEnforcer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string ImagePath { get; set; } = "";

    public int ImageAspect { get; set; } = 0;

    public int TotalResets { get; set; } = 0;

    /// <summary>
    /// Date of the last reset press. Null = never reset.
    /// </summary>
    public DateTime? LastResetDate { get; set; }

    /// <summary>
    /// Date the current streak started (day after last reset,
    /// or CreatedAt if never reset).
    /// </summary>
    public DateTime StreakStartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current level index into the enforcer's EnforcerLevel list.
    /// 0 = first level. -1 = no levels defined.
    /// </summary>
    public int CurrentLevelIndex { get; set; } = -1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// 0 = Private (PrivateOnly + All)
    /// 1 = Public (PublicOnly + All)
    /// 2 = Both (all three modes)
    /// Default 1 = Public.
    /// </summary>
    public int Visibility { get; set; } = 1;

    // Computed — not stored
    [Ignore]
    public int DaysInARow
    {
        get
        {
            var start = StreakStartDate.Date;
            var today = DateTime.UtcNow.Date;
            var days = (today - start).Days;
            return Math.Max(0, days);
        }
    }
}
