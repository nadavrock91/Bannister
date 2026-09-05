using SQLite;

namespace Bannister.Models;

[Table("games")]
public class Game
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string GameId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time meaningful escalation was recorded
    /// </summary>
    public DateTime? LastMeaningfulEscalation { get; set; }

    /// <summary>
    /// Whether the escalation timer is disabled for this game
    /// </summary>
    public bool IsEscalationTimerDisabled { get; set; }

    /// <summary>
    /// Duration in days for the escalation timer. Default 30.
    /// </summary>
    public int EscalationDurationDays { get; set; } = 30;

    /// <summary>
    /// Last time this game was visited/entered
    /// </summary>
    public DateTime? LastVisitedAt { get; set; }

    /// <summary>
    /// Computed property: Days remaining until 30-day deadline
    /// </summary>
    [Ignore]
    public int DaysRemaining
    {
        get
        {
            if (IsEscalationTimerDisabled)
                return EscalationDurationDays;

            if (!LastMeaningfulEscalation.HasValue)
                return EscalationDurationDays;

            var now = DateTime.Now;
            var escalation = LastMeaningfulEscalation.Value;
            var elapsed = now - escalation;

            System.Diagnostics.Debug.WriteLine($">>> NOW: {now}");
            System.Diagnostics.Debug.WriteLine($">>> ESCALATION: {escalation}");
            System.Diagnostics.Debug.WriteLine($">>> ELAPSED DAYS: {elapsed.TotalDays}");

            var remaining = EscalationDurationDays - (int)elapsed.TotalDays;
            return Math.Max(0, remaining);
        }
    }

    /// <summary>
    /// Computed property: Progress percentage (0.0 to 1.0)
    /// </summary>
    [Ignore]
    public double EscalationProgress
    {
        get
        {
            return EscalationDurationDays > 0
                ? DaysRemaining / (double)EscalationDurationDays
                : 1.0;
        }
    }
}
