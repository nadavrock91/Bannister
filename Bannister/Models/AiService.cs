using SQLite;

namespace Bannister.Models;

[Table("ai_services")]
public class AiService
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal MonthlyCost { get; set; }
    public string Status { get; set; } = "Pending"; // "Pending" | "Approved" | "Cut"
    public string Reason { get; set; } = "";        // why you're trying it
    public string OutcomeNote { get; set; } = "";   // verdict note
    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;
    public int TrialDays { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
