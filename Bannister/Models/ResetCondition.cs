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

    // Time window — all nullable, null means not set
    /// <summary>Day of week 0=Sun..6=Sat. -1 = any day.</summary>
    public int StartDay { get; set; } = -1;

    /// <summary>Time of day in minutes from midnight. -1 = not set.</summary>
    public int StartTime { get; set; } = -1;

    /// <summary>Day of week 0=Sun..6=Sat. -1 = any day.</summary>
    public int EndDay { get; set; } = -1;

    /// <summary>Time of day in minutes from midnight. -1 = not set.</summary>
    public int EndTime { get; set; } = -1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed display helpers
    [Ignore]
    public string StartDisplay => FormatDayTime(StartDay, StartTime);

    [Ignore]
    public string EndDisplay => FormatDayTime(EndDay, EndTime);

    [Ignore]
    public bool HasTimeWindow =>
        StartDay >= 0 || StartTime >= 0 ||
        EndDay >= 0 || EndTime >= 0;

    private static string FormatDayTime(int day, int time)
    {
        var parts = new List<string>();
        if (day >= 0)
            parts.Add(new[]
            {
                "Sun","Mon","Tue","Wed","Thu","Fri","Sat"
            }[day % 7]);
        if (time >= 0)
        {
            int h = time / 60;
            int m = time % 60;
            string ampm = h >= 12 ? "PM" : "AM";
            int h12 = h % 12 == 0 ? 12 : h % 12;
            parts.Add($"{h12}:{m:D2} {ampm}");
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "";
    }
}
