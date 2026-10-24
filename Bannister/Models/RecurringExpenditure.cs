using SQLite;

namespace Bannister.Models;

[Table("recurring_expenditures")]
public class RecurringExpenditure
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal MonthlyCost { get; set; }
    public string Status { get; set; } = "Pending"; // "Pending" | "Approved" | "Cut"
    public string Reason { get; set; } = "";        // why you're trying it
    public string OutcomeNote { get; set; } = "";   // verdict note
    public string JustificationTarget { get; set; } = ""; // what result would justify keeping this
    public string CurrencyCode { get; set; } = "USD";
    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;
    public DateTime TrialEndDate { get; set; } = DateTime.UtcNow.AddDays(30);
    public int TrialDays { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
