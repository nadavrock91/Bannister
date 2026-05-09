using SQLite;

namespace Bannister.Models;

[Table("monthly_expenses")]
public class MonthlyExpense
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string Category { get; set; } = "General";

    public decimal Amount { get; set; }

    public int DueDay { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
