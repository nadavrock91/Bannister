using SQLite;

namespace Bannister.Models;

[Table("reset_conditions")]
public class ResetCondition
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ResetEnforcerId { get; set; }

    public string Text { get; set; } = "";

    /// <summary>
    /// What gets reset when this condition is triggered.
    /// e.g. "Streak", "No junk food count", "Savings progress"
    /// </summary>
    public string WhatGetsReset { get; set; } = "";

    /// <summary>Full start date+time. Null = not set.</summary>
    public DateTime? StartDateTime { get; set; }

    /// <summary>Full end date+time. Null = not set.</summary>
    public DateTime? EndDateTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string StartDisplay =>
        StartDateTime.HasValue
            ? StartDateTime.Value.ToLocalTime()
                .ToString("ddd dd MMM yyyy HH:mm")
            : "";

    [Ignore]
    public string EndDisplay =>
        EndDateTime.HasValue
            ? EndDateTime.Value.ToLocalTime()
                .ToString("ddd dd MMM yyyy HH:mm")
            : "";

    [Ignore]
    public bool HasTimeWindow =>
        StartDateTime.HasValue || EndDateTime.HasValue;
}
