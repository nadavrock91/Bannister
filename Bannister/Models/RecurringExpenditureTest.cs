using SQLite;

namespace Bannister.Models;

[Table("recurring_expenditure_tests")]
public class RecurringExpenditureTest
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int ExpenditureId { get; set; }
    public string Note { get; set; } = "";
    public string ActualResult { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
